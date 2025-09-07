using UnityEngine;
using System;
using System.Collections;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Combines terrain mesh and collider logic for a planet tile.
    /// Centralized management of ray-collider functionality, texture/material setup, and visibility state.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlanetTerrainTile : MonoBehaviour
    {
        // MeshFilter and MeshRenderer for terrain visuals
        public MeshFilter meshFilter { get; private set; }
        public MeshRenderer meshRenderer { get; private set; }

        // MeshCollider for occlusion and raycast logic
        public MeshCollider meshCollider { get; private set; }

        // Cached visual mesh for fast show/hide
        private Mesh cachedVisualMesh;

        // Tile metadata (can be extended)
        public TileId tileId;
        public TileData tileData;

        // Visibility and ray-casting state
        public bool isVisible { get; private set; } = true;
        public float spawnTime { get; private set; }

        // How long (seconds) without hits before the tile auto-deactivates.
        // Tests can override the debounce by setting TestDeactivationDebounceOverride to a positive value.
        [SerializeField]
        public float deactivationDebounceSeconds = 5f;

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
        /// Ensure MeshFilter, MeshRenderer and MeshCollider components are present on the GameObject.
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
            if (meshCollider == null)
            {
                meshCollider = gameObject.GetComponent<MeshCollider>();
                if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();
            }
        }

        /// <summary>
        /// Initializes the tile and generates the collider mesh.
        /// </summary>
        public void Initialize(TileId id, TileData data, System.Func<TileId, Mesh> colliderMeshGenerator)
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

                // Create MeshCollider
                meshCollider = gameObject.GetComponent<MeshCollider>();
                if (meshCollider == null)
                {
                    meshCollider = gameObject.AddComponent<MeshCollider>();
                    if (meshCollider == null)
                    {
                        Debug.LogError($"[TILE INIT] FAILED to create MeshCollider component!");
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

            // Generate and assign collider mesh
            Mesh colliderMesh = colliderMeshGenerator != null ? colliderMeshGenerator(id) : null;
            if (meshCollider != null)
            {
                if (colliderMesh != null)
                {
                    meshCollider.sharedMesh = colliderMesh;
                }
                else
                {
                    Debug.LogWarning($"[TILE INIT] No collider mesh generated for {id} - MeshCollider will have no mesh");
                }
                meshCollider.convex = false;
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
            if (meshCollider == null)
            {
                Debug.LogError($"[TILE INIT] CRITICAL: MeshCollider is still null after initialization!");
                meshCollider = gameObject.AddComponent<MeshCollider>(); // Try one more time
            }
        }

        /// <summary>
        /// Setup material and layer configuration for the tile.
        /// </summary>
        public void ConfigureMaterialAndLayer(Material terrainMaterial, int targetLayer)
        {
            baseMaterial = terrainMaterial;
            
            // Create material instance
            if (meshRenderer != null && terrainMaterial != null)
            {
                materialInstance = new Material(terrainMaterial);
                materialInstance.name = terrainMaterial.name + " (Tile Instance)";
                meshRenderer.material = materialInstance;
                
                // Set default color
                if (materialInstance.HasProperty("_Color"))
                {
                    materialInstance.SetColor("_Color", Color.white);
                }
            }

            // Configure layers recursively
            SetLayerRecursively(gameObject, targetLayer);
        }

        /// <summary>
        /// Set layer recursively on GameObject and all children.
        /// </summary>
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            foreach (Transform t in obj.transform)
            {
                SetLayerRecursively(t.gameObject, layer);
            }
        }

        /// <summary>
        /// Test if a ray intersects with this tile's collider.
        /// </summary>
        public bool TestRayIntersection(Ray ray, out RaycastHit hitInfo, int layerMask)
        {
            hitInfo = default;
            if (meshCollider == null || !gameObject.activeInHierarchy)
                return false;

            return meshCollider.Raycast(ray, out hitInfo, Mathf.Infinity);
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
        /// Public method to refresh tile activity from external systems (for example the raycast heuristic).
        /// This restarts the auto-deactivate timer so the tile remains active while being observed/hit.
        /// </summary>
        public void RefreshActivity()
        {
            try { RestartAutoDeactivate(); } catch { }
        }

        private IEnumerator AutoDeactivateCoroutine(float timeout)
        {
            // Wait for the timeout in real time, then deactivate visuals if still eligible
            if(debug) Debug.Log($"{Time.realtimeSinceStartup} AutoDeactivateCoroutine started, waiting {timeout}s");
            yield return new WaitForSecondsRealtime(timeout);

            // Instead of fully deactivating the GameObject (which disables colliders
            // and makes the tile un-targetable by raycasts), only hide the visual
            // mesh. Keep the GameObject and MeshCollider active so external
            // heuristics can still raycast against this tile and reactivate visuals
            // when needed. This preserves raycastability while saving rendering cost.
            try
            {
                if(debug) Debug.Log($"{Time.realtimeSinceStartup} AutoDeactivateCoroutine timeout reached, hiding mesh");
                HideVisualMesh();
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
            EnsureComponentsExist();
            if (meshFilter == null) return;
            if (meshFilter.sharedMesh != null) return; // already built
            if (cachedVisualMesh != null)
            {
                meshFilter.sharedMesh = cachedVisualMesh;
                return; // already cached
            }

            // Create a simple box mesh to satisfy tests (much more reliable for raycasting than a flat quad)
            var m = new Mesh();
            m.name = "Tile_FallbackMesh";
            
            // Create a box with small thickness (0.2 units) centered at origin
            float w = 1.0f, h = 0.2f, d = 1.0f; // width, height, depth
            float hw = w * 0.5f, hh = h * 0.5f, hd = d * 0.5f;
            
            // 8 vertices of a box
            m.vertices = new Vector3[] {
                // Bottom face (y = -hh)
                new Vector3(-hw, -hh, -hd), // 0: back-left-bottom
                new Vector3( hw, -hh, -hd), // 1: back-right-bottom
                new Vector3( hw, -hh,  hd), // 2: front-right-bottom
                new Vector3(-hw, -hh,  hd), // 3: front-left-bottom
                // Top face (y = +hh)
                new Vector3(-hw,  hh, -hd), // 4: back-left-top
                new Vector3( hw,  hh, -hd), // 5: back-right-top
                new Vector3( hw,  hh,  hd), // 6: front-right-top
                new Vector3(-hw,  hh,  hd), // 7: front-left-top
            };
            
            // 12 triangles (2 per face, 6 faces)
            m.triangles = new int[] {
                // Bottom face (y = -hh)
                0, 2, 1,  0, 3, 2,
                // Top face (y = +hh)  
                4, 5, 6,  4, 6, 7,
                // Front face (+Z)
                3, 6, 2,  3, 7, 6,
                // Back face (-Z)
                1, 4, 0,  1, 5, 4,
                // Left face (-X)
                0, 7, 3,  0, 4, 7,
                // Right face (+X)
                2, 5, 1,  2, 6, 5
            };
            
            m.RecalculateNormals();
            m.RecalculateBounds();
            meshFilter.sharedMesh = m;
            cachedVisualMesh = m;

            // If this method was called while the GameObject was inactive, treat
            // it as a hint that the tile has become relevant again and reactivate
            // the GameObject. This lets external systems (for example a visibility
            // heuristic) call EnsureMeshBuilt on inactive tiles and have the tile
            // handle reactivation itself.
            if (!gameObject.activeInHierarchy)
            {
                try { gameObject.SetActive(true); }
                catch
                {
                    Debug.LogError($"[TILE] Failed to reactivate GameObject when EnsureMeshBuilt was called on inactive tile.");
                }
            }

            // Restart the auto-deactivation timer so any externally-set debounce
            // value (for example, set by tests before calling EnsureMeshBuilt)
            // is respected immediately.
            try { RestartAutoDeactivate(); } catch { }
        }

        public bool ActivateIfHit(Ray ray)
        {
            bool hitThisTile = false;
            // Prefer testing the tile's own MeshCollider directly when available
            var col = meshCollider;
            if (col != null)
            {
                try
                {
                    if (col.Raycast(ray, out RaycastHit phCol, Mathf.Infinity))
                    {
                        hitThisTile = true;
                    }
                }
                catch { }
            }

            if (hitThisTile)
            {
        if (debug) Debug.Log($"{Time.realtimeSinceStartup} Tile hit by ray: {gameObject.name}");
                // If the tile's visuals are currently hidden, reactivate them
                // since the camera is now hitting this tile
                if (!isVisible)
                {
                    try
                    {
            if (debug) Debug.Log($"{Time.realtimeSinceStartup} Reactivating visuals for: {gameObject.name}");
                        ShowVisualMesh();  // Re-enable the renderer
                    }
                    catch { }
                }

                // Always refresh activity to restart the debounce timer
                try { RefreshActivity(); } catch { }
            }
            
            return hitThisTile;
        }

        /// <summary>
        /// Show the visual mesh (enable renderer and assign cached mesh).
        /// Tile remains a raycast target to prevent hitting the far side of the planet.
        /// </summary>
        public void ShowVisualMesh()
        {
            if(debug) Debug.Log($"{Time.realtimeSinceStartup} ShowMesh");
            isVisible = true;
            if (meshRenderer != null) meshRenderer.enabled = true;
            if (meshFilter != null && cachedVisualMesh != null) meshFilter.sharedMesh = cachedVisualMesh;
        }

        /// <summary>
        /// Hide the visual mesh (disable renderer) but keep collider active.
        /// Tile remains a raycast target to prevent hitting the far side of the planet.
        /// </summary>
        public void HideVisualMesh()
        {
            if(debug) Debug.Log($"{Time.realtimeSinceStartup} HideMesh");
            isVisible = false;
            if (meshRenderer != null) meshRenderer.enabled = false;
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
                layer = gameObject.layer,
                layerName = LayerMask.LayerToName(gameObject.layer),
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

            // Camera visibility check
            if (camera != null)
            {
                try 
                { 
                    info.cameraSeesLayer = (camera.cullingMask & (1 << gameObject.layer)) != 0; 
                } 
                catch 
                { 
                    info.cameraSeesLayer = false; 
                }
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
        public int layer;
        public string layerName;
        public Vector3 position;
        public Vector3 scale;
        public bool isVisible;
        public float spawnTime;
        public bool cameraSeesLayer;
        
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
                   $"layer={layer}({layerName}) camSeesLayer={cameraSeesLayer} mesh={meshName} " +
                   $"verts={vertexCount} tris={triangleCount} bounds={bounds} " +
                   $"material={materialName} shader={shaderName} rendererEnabled={rendererEnabled} " +
                   $"pos={position} scale={scale} visible={isVisible}";
        }
    }
}
