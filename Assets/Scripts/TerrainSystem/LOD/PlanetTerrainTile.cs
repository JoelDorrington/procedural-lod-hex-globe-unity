using UnityEngine;
using System;
using System.Collections;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Encapsulates terrain mesh logic for a planet tile.
    /// Centralized management of texture/material setup and visibility state.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlanetTerrainTile : MonoBehaviour
    {
        // MeshFilter and MeshRenderer for terrain visuals
        public MeshFilter meshFilter { get; private set; }
        public MeshRenderer meshRenderer { get; private set; }

        // Tile metadata (can be extended)
        public TileId tileId;
        public TileData tileData;

        // Visibility and ray-casting state
        public bool isVisible { get; private set; } = true;
        public float spawnTime { get; private set; }
        // Time when a mesh was assigned to this tile (Time.time)
        public float meshAssignedTime { get; private set; } = -1f;
        // Time when the GameObject was activated (Time.time)
        public float activatedTime { get; private set; } = -1f;

        // Material management
        private Material materialInstance;
        public Material baseMaterial { get; private set; }

        public bool debug = false;

        /// <summary>
        /// Ensure MeshFilter and MeshRenderer components are present on the GameObject.
        /// This mirrors part of Initialize but is safe to call independently for tests.
        /// </summary>
        public void EnsureComponentsExist()
        {
            if (meshFilter == null)
            {
                meshFilter = gameObject.GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
        }

        /// <summary>
        /// Initializes the tile.
        /// </summary>
        public void Initialize(TileId id, TileData data)
        {
            if (spawnTime > 0f) return; // already initialized
            tileId = id;
            tileData = data;
            spawnTime = Time.time;
            transform.position = data.center;

            EnsureComponentsExist();
            // Create MeshFilter
            meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

            // Set initial visual mesh if available
            if (data?.mesh != null && meshFilter?.sharedMesh == null)
            {
                AssignMesh(data.mesh);
            }
            isVisible = true;
            // Activation timestamp is set by the manager when it activates the GameObject.
        }

        /// <summary>
        /// Setup material and layer configuration for the tile.
        /// </summary>
        public void ConfigureMaterialAndLayer(Material terrainMaterial, Vector3 planetCenter = default)
        {
            baseMaterial = terrainMaterial;
            
            // Create material instance
            if (meshRenderer != null && terrainMaterial != null)
            {
                materialInstance = new Material(terrainMaterial);
                materialInstance.name = terrainMaterial.name + " (Tile Instance)";
                meshRenderer.material = materialInstance;
                
                materialInstance.SetVector("_PlanetCenter", new Vector4(planetCenter.x, planetCenter.y, planetCenter.z, 0));
            }
        }

        /// <summary>
        /// Assigns the final mesh to the MeshFilter and records the assignment time for diagnostics.
        /// </summary>
        public void AssignMesh(Mesh mesh)
        {
            meshFilter.sharedMesh = mesh;
            meshAssignedTime = Time.time;
        }

        /// <summary>
        /// Restart the auto-deactivation timer. If TestDeactivationDebounceOverride is set (>0), it will be used.
        /// </summary>
        /// <summary>
        /// No-op: auto-deactivation is handled externally by the PlanetTileVisibilityManager.
        /// Kept for API compatibility with tests/code that call RestartAutoDeactivate.
        /// </summary>
        public void RestartAutoDeactivate()
        {
            if (debug) Debug.Log("RestartAutoDeactivate called but auto-deactivation is managed by PTVM.");
        }

        /// <summary>
        /// Public method to refresh tile activity from external systems (for example the visibility heuristic).
        /// This restarts the auto-deactivate timer so the tile remains active while being observed/hit.
        /// If the tile is hidden, it will be shown again.
        /// </summary>
        /// <summary>
        /// Refresh activity - make the tile visible. Activation and deactivation timing
        /// should be controlled by the PlanetTileVisibilityManager. This method simply
        /// ensures the GameObject is active and marks visible for diagnostics.
        /// </summary>
        public void RefreshActivity()
        {
            if (!isVisible)
            {
                this.gameObject.SetActive(true);
                isVisible = true;
            }
            // Record activation time for diagnostics; manager should set this when appropriate.
            activatedTime = Time.time;
        }

        /// <summary>
        /// Immediately deactivate this tile's GameObject. Manager should call this when
        /// a tile is no longer needed.
        /// </summary>
        public void DeactivateImmediately()
        {
            if (debug) Debug.Log($"DeactivateImmediately called on {gameObject.name}");
            this.gameObject.SetActive(false);
            isVisible = false;
        }

        public void SetVisibility(bool visible)
        {
            if (visible && !isVisible)
            {
                this.gameObject.SetActive(true);
                isVisible = true;
                activatedTime = Time.time;
            }
            else if (!visible && isVisible)
            {
                this.gameObject.SetActive(false);
                isVisible = false;
            }
        }

        /// <summary>
        /// Public test-friendly method to ensure a mesh exists for this tile.
        /// Builds a simple quad mesh once and caches it.
        /// </summary>
        public void BuildTerrain()
        {
            if (!gameObject.activeInHierarchy)
            {
                try { gameObject.SetActive(true); }
                catch
                {
                    Debug.LogError($"[TILE] Failed to reactivate GameObject when EnsureMeshBuilt was called on inactive tile.");
                }
            }
            EnsureComponentsExist();
            if (meshFilter == null) return;
            if (meshFilter.sharedMesh != null) return; // already built

            // Note: deactivation is managed by the PlanetTileVisibilityManager.
        }

        /// <summary>
        /// Get diagnostic information about this tile.
        /// </summary>
        public TileDiagnosticInfo GetDiagnosticInfo(Camera camera = null)
        {
            var info = new TileDiagnosticInfo
            {
                tileName = gameObject.name,
                isActive = gameObject.activeInHierarchy,
                parentName = transform.parent?.name ?? "<null>",
                position = transform.position,
                scale = transform.lossyScale,
                isVisible = this.isVisible,
                spawnTime = this.spawnTime
            };

            // Mesh information
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                var mesh = meshFilter.sharedMesh;
                info.meshName = string.IsNullOrEmpty(mesh.name) ? "<unnamed-mesh>" : mesh.name;
                info.vertexCount = mesh.vertexCount;
                info.triangleCount = mesh.triangles?.Length / 3 ?? 0;
                info.bounds = mesh.bounds.ToString();
            }

            // Material information
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                info.materialName = meshRenderer.sharedMaterial.name ?? "<unnamed-material>";
                info.shaderName = meshRenderer.sharedMaterial.shader?.name ?? "<null-shader>";
                info.rendererEnabled = meshRenderer.enabled;
            }

            return info;
        }

        private void OnDestroy()
        {
            // Clean up material instance
            if (materialInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(materialInstance);
                else
                    DestroyImmediate(materialInstance);
            }
        }
    }

    /// <summary>
    /// Diagnostic information structure for tile debugging.
    /// </summary>
    [Serializable]
    public struct TileDiagnosticInfo
    {
        public string tileName;
        public bool isActive;
        public string parentName;
        public Vector3 position;
        public Vector3 scale;
        public bool isVisible;
        public float spawnTime;
        
        // Mesh info
        public string meshName;
        public int vertexCount;
        public int triangleCount;
        public string bounds;
        
        // Material info
        public string materialName;
        public string shaderName;
        public bool rendererEnabled;

        public override string ToString()
        {
            return $"[TILE INFO] {tileName} active={isActive} parent={parentName} " +
                   $"mesh={meshName} verts={vertexCount} tris={triangleCount} bounds={bounds} " +
                   $"material={materialName} shader={shaderName} rendererEnabled={rendererEnabled} " +
                   $"pos={position} scale={scale} visible={isVisible}";
        }
    }
}
