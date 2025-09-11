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

    // How long (seconds) without hits before the tile auto-deactivates.
    // Tests can override the debounce by setting TestDeactivationDebounceOverride to a positive value.
    [SerializeField]
    public float deactivationDebounceSeconds = 0.5f;

        // Internal coroutine handle for auto-deactivation so we can restart when the tile is refreshed.
        private Coroutine _autoDeactivateCoroutine;

        // Material management
        private Material materialInstance;
        public Material baseMaterial { get; private set; }

        public bool debug = false;

        // Ensure required components exist even if Initialize is not called (tests expect this)
        private void Awake()
        {
            EnsureComponentsExist();
        }

        /// <summary>
        /// Ensure MeshFilter and MeshRenderer components are present on the GameObject.
        /// This mirrors part of Initialize but is safe to call independently for tests.
        /// </summary>
        private void EnsureComponentsExist()
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
            tileId = id;
            tileData = data;
            spawnTime = Time.time;

            // Restart auto-deactivate timer when tile is (re)initialized
            RestartAutoDeactivate();

            // Ensure the GameObject is positioned at the tile's world-space center.
            // Some callers previously set transform.position externally; centralize here to avoid mistakes.
            if (data != null)
            {
                transform.position = data.center;
            }

            try
            {
                // Ensure GameObject is active for component creation
                bool wasActive = gameObject.activeSelf;
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }
                
                // Create MeshFilter
                meshFilter = gameObject.GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    meshFilter = gameObject.AddComponent<MeshFilter>();
                    if (meshFilter == null)
                    {
                        Debug.LogError($"[TILE INIT] FAILED to create MeshFilter component!");
                    }
                }
                
                // Create MeshRenderer
                meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    // creation logs removed
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                    if (meshRenderer == null)
                    {
                        Debug.LogError($"[TILE INIT] FAILED to create MeshRenderer component!");
                    }
                    else
                    {
                        // success log removed
                    }
                }
                
                // Restore original active state if we changed it
                if (!wasActive && gameObject.activeSelf)
                {
                    gameObject.SetActive(wasActive);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TILE INIT] Exception during component creation: {ex.Message}\n{ex.StackTrace}");
                throw;
            }

            // Set initial visual mesh if available
            if (data != null && data.mesh != null && meshFilter != null)
            {
                meshFilter.sharedMesh = data.mesh;
            }
            
            // Final verification - ensure all components were created successfully
            if (meshFilter == null)
            {
                Debug.LogError($"[TILE INIT] CRITICAL: MeshFilter is still null after initialization!");
                meshFilter = gameObject.AddComponent<MeshFilter>(); // Try one more time
            }
            if (meshRenderer == null)
            {
                Debug.LogError($"[TILE INIT] CRITICAL: MeshRenderer is still null after initialization!");
                meshRenderer = gameObject.AddComponent<MeshRenderer>(); // Try one more time
            }
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
            else
            {
                // no-op when meshRenderer or terrainMaterial is missing
            }

        }

        /// <summary>
        /// Get sphere hit point using mathematical ray-sphere intersection.
        /// </summary>
        public static Vector3 GetSphereHitPoint(Ray ray, Vector3 sphereCenter, float sphereRadius, float curvedRadiusMultiplier = 1.0f)
        {
            // Clamp rays to planet's visible circumference for 100% hit rate when zoomed out
            Vector3 camToPlanet = sphereCenter - ray.origin;
            float projectionLength = Vector3.Dot(camToPlanet, ray.direction);
            Vector3 closestPoint = ray.origin + ray.direction * projectionLength;
            float distToCenter = (closestPoint - sphereCenter).magnitude;

            if (distToCenter > sphereRadius)
            {
                // Ray would miss the sphere - clamp it to hit the sphere edge
                Vector3 rayToPlanetCenter = (sphereCenter - ray.origin).normalized;
                Vector3 rayDirection = ray.direction.normalized;

                float dot = Vector3.Dot(rayDirection, rayToPlanetCenter);
                if (dot > 0f) // Ray pointing towards planet
                {
                    Vector3 perpendicular = (rayDirection - dot * rayToPlanetCenter).normalized;
                    Vector3 tangentPoint = sphereCenter + perpendicular * sphereRadius;
                    ray.direction = (tangentPoint - ray.origin).normalized;
                }
                else
                {
                    Vector3 nearestVisible = sphereCenter + rayToPlanetCenter * sphereRadius;
                    ray.direction = (nearestVisible - ray.origin).normalized;
                }
            }

            // Mathematical ray-sphere intersection
            float a = Vector3.Dot(ray.direction, ray.direction);
            float b = 2f * Vector3.Dot(ray.direction, ray.origin - sphereCenter);
            float c = (ray.origin - sphereCenter).sqrMagnitude - sphereRadius * sphereRadius;
            float discriminant = b * b - 4f * a * c;

            if (discriminant < 0f)
                return Vector3.zero; // No intersection

            float sqrtDisc = Mathf.Sqrt(discriminant);
            float t0 = (-b - sqrtDisc) / (2f * a);
            
            if (t0 < 0f)
                return Vector3.zero; // Intersection behind camera

            Vector3 hitPoint = ray.origin + ray.direction * t0;
            
            // Apply curved projection if specified
            if (curvedRadiusMultiplier != 1.0f)
            {
                float curvedMultiplier = Mathf.Clamp(curvedRadiusMultiplier, 0.95f, 1.2f);
                hitPoint = hitPoint.normalized * (sphereRadius * curvedMultiplier);
            }

            return hitPoint;
        }

        /// <summary>
        /// Restart the auto-deactivation timer. If TestDeactivationDebounceOverride is set (>0), it will be used.
        /// </summary>
        public void RestartAutoDeactivate()
        {
            if(debug) Debug.Log($"{Time.realtimeSinceStartup} RestartAutoDeactivate, {deactivationDebounceSeconds}s");
            // Stop existing coroutine
            if (_autoDeactivateCoroutine != null)
            {
                if(debug) Debug.Log($"coroutine stop {_autoDeactivateCoroutine}");
                try { StopCoroutine(_autoDeactivateCoroutine); } catch { }
                _autoDeactivateCoroutine = null;
            }

            // Only start coroutine when timeout is non-negative
            if (deactivationDebounceSeconds >= 0f)
            {
                if(debug) Debug.Log($"coroutine start, {deactivationDebounceSeconds}s");
                _autoDeactivateCoroutine = StartCoroutine(AutoDeactivateCoroutine(deactivationDebounceSeconds));
            }
        }

        /// <summary>
        /// Public method to refresh tile activity from external systems (for example the visibility heuristic).
        /// This restarts the auto-deactivate timer so the tile remains active while being observed/hit.
        /// If the tile is hidden, it will be shown again.
        /// </summary>
        public void RefreshActivity()
        {
            // If tile is hidden, show it again when refreshed
            if (!isVisible)
            {
                this.gameObject.SetActive(true);
            }
            try { RestartAutoDeactivate(); } catch { }
        }

        private IEnumerator AutoDeactivateCoroutine(float timeout)
        {
            // Wait for the timeout in real time, then deactivate visuals if still eligible
            if(debug) Debug.Log($"{Time.realtimeSinceStartup} AutoDeactivateCoroutine started, waiting {timeout}s");
            yield return new WaitForSecondsRealtime(timeout);

            try
            {
                if(debug) Debug.Log($"{Time.realtimeSinceStartup} AutoDeactivateCoroutine timeout reached, hiding mesh");
                this.gameObject.SetActive(false);
            }
            catch (Exception ex)
            {
                if(debug) Debug.LogError($"[TILE] Exception in AutoDeactivateCoroutine: {ex.Message}");
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

            // Restart the auto-deactivation timer so any externally-set debounce
            // value (for example, set by tests before calling EnsureMeshBuilt)
            // is respected immediately.
            try { RestartAutoDeactivate(); } catch { }
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
