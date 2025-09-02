using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Google Maps-style tile explorer for the planet.
    /// - Zoom level (depth) determines the tile resolution.
    /// - A raycast heuristic samples the screen and spawns tiles for hit locations.
    /// - The heuristic runs at a target frequency and ensures tiles aren't duplicated or destroyed prematurely.
    /// </summary>
    [AddComponentMenu("HexGlobe/Planet Tile Explorer Cam")]
    public class PlanetTileExplorerCam : MonoBehaviour
    {
        // ----- Inspector / references -----

        [SerializeField]
        [Tooltip("Game Camera controller (must provide access to an actual Camera)")]
        public CameraController GameCamera;
        [SerializeField]
        [Tooltip("Terrain config")]
        public TerrainConfig config;

        [SerializeField]
        [Tooltip("Optional transform representing the planet center (used for raycasts). If null, this GameObject's transform is used.")]
        private Transform planetTransform;

        [SerializeField]
        [Tooltip("Fallback terrain material used when no manager is present")]
        private Material terrainMaterial;

        // ----- Tuning -----
        [SerializeField]
        [Tooltip("Maximum allowed depth (soft cap)")]
        private int maxDepth = 2;

        [SerializeField]
        [Tooltip("Approximate number of rays to cast; actual cast uses a square sampling grid <= this value")]
        private int _maxRays = 200;

    // stitching removed: tile meshes rely on shared sample coordinates for edge consistency

        // ----- Internal state -----
        private PlanetTileMeshBuilder meshBuilder;
        private readonly Dictionary<TileId, GameObject> tileObjects = new();
        private readonly Dictionary<TileId, float> tileSpawnTimes = new();

        private Coroutine _heuristicCoroutine;
        private float _lastHeuristicTime = 0f;
        private const float HeuristicInterval = 1f / 30f; // target ~30 Hz

        // Current depth (derived from camera state). Use _currentDepth for all tile logic.
        private int _currentDepth = 0;

        /// <summary>
        /// Initialize mesh builder reference.
        /// </summary>
        private void Awake()
        {
            if (config == null)
            {
                throw new System.NotImplementedException("Default config not available");
            }
            meshBuilder = new PlanetTileMeshBuilder(config, config.heightProvider, null, 0, 1, 0f, false);
        }

        /// <summary>
        /// Update depth from the camera and schedule the heuristic coroutine (single-run enforcement).
        /// </summary>
        private void Update()
        {
            // Derive depth from camera proportional distance (CameraController exposes ProportionalDistance [0..1])
            if (GameCamera != null)
            {
                int newDepth = Mathf.RoundToInt(Mathf.Lerp(0, maxDepth, 1-GameCamera.ProportionalDistance));
                if (newDepth != _currentDepth)
                {
                    Debug.Log($"[TileDepth] Depth changed: {_currentDepth} -> {newDepth} (distance: {GameCamera.ProportionalDistance:F3})");
                    _currentDepth = newDepth;
                }
            }

            // Schedule heuristic if not already running and interval elapsed
            if (_heuristicCoroutine == null && Time.time - _lastHeuristicTime >= HeuristicInterval)
            {
                _lastHeuristicTime = Time.time;
                _heuristicCoroutine = StartCoroutine(RunTileRaycastHeuristicCoroutine());
            }
        }

        /// <summary>
        /// Fetch or spawn a tile GameObject for the given id/resolution.
        /// This function ensures a recently spawned tile won't be immediately culled.
        /// </summary>
        public GameObject GetOrSpawnTile(TileId id, int resolution)
        {
            if (tileObjects.TryGetValue(id, out var go))
            {
                if (go != null)
                {
                    go.SetActive(true);
                    return go;
                }

                // stale entry: remove it and continue to spawn
                tileObjects.Remove(id);
            }

            // record spawn time to prevent immediate destruction
            tileSpawnTimes[id] = Time.time;

            var tileData = new TileData { id = id, resolution = resolution, isBaked = true };
            float dummyMin = 0f, dummyMax = 0f;
            if (meshBuilder != null)
            {
                meshBuilder.BuildTileMesh(tileData, ref dummyMin, ref dummyMax, true);
            }

            var spawner = new PlanetTileSpawner();
            spawner.SpawnOrUpdateTileGO(tileData, tileObjects, terrainMaterial, this.transform);

            tileObjects.TryGetValue(id, out go);
            if (go != null)
            {
                go.SetActive(true);
            }

            return go;
        }

        /// <summary>
        /// Destroy a tile if it exists and it's older than the safety window.
        /// </summary>
        public void DestroyTile(TileId id)
        {
            if (!tileObjects.TryGetValue(id, out var go) || go == null) return;

            if (tileSpawnTimes.TryGetValue(id, out float spawnTime) && Time.time - spawnTime < 0.5f)
            {
                // skip destruction for newly spawned tiles
                return;
            }

            Object.Destroy(go);
            tileObjects.Remove(id);
            tileSpawnTimes.Remove(id);
        }

        /// <summary>
        /// Raycast-based heuristic that samples the camera viewport and spawns tiles for hit locations.
        /// Single-running coroutine; yields periodically for responsiveness.
        /// </summary>
        private System.Collections.IEnumerator RunTileRaycastHeuristicCoroutine()
        {
            // Validate required references
            if (GameCamera == null)
            {
                _heuristicCoroutine = null;
                yield break;
            }

            Camera cam = GameCamera.GetComponent<Camera>();
            if (cam == null)
            {
                _heuristicCoroutine = null;
                yield break;
            }

            var hitTiles = new HashSet<TileId>();

            int raysToCast = Mathf.Max(1, _maxRays);
            int depth = _currentDepth;
            int tilesPerEdge = 1 << depth;
            float planetRadius = config != null ? config.baseRadius : 1f;
            
            int sqrtRays = Mathf.CeilToInt(Mathf.Sqrt(raysToCast));
            int rayCount = sqrtRays * sqrtRays;

            Vector3 planetCenter = planetTransform != null ? planetTransform.position : this.transform.position;

            for (int i = 0; i < rayCount; i++)
            {
                float u = (i % sqrtRays + 0.5f) / sqrtRays;
                float v = (i / sqrtRays + 0.5f) / sqrtRays;

                Ray ray = cam.ViewportPointToRay(new Vector3(u, v, 0f));

                // Ray-sphere intersection test (quick reject using closest approach)
                Vector3 camToPlanet = planetCenter - ray.origin;
                float t = Vector3.Dot(camToPlanet, ray.direction);
                Vector3 closest = ray.origin + ray.direction * t;
                float distToCenter = (closest - planetCenter).magnitude;
                if (distToCenter > planetRadius) continue;

                float a = Vector3.Dot(ray.direction, ray.direction);
                float b = 2f * Vector3.Dot(ray.direction, ray.origin - planetCenter);
                float c = (ray.origin - planetCenter).sqrMagnitude - planetRadius * planetRadius;
                float discriminant = b * b - 4f * a * c;
                if (discriminant < 0f) continue;

                float sqrtDisc = Mathf.Sqrt(discriminant);
                float t0 = (-b - sqrtDisc) / (2f * a);
                if (t0 < 0f) continue; // intersection behind camera

                Vector3 hitPoint = ray.origin + ray.direction * t0;

                // Map hitPoint to cube face and local coordinates
                Vector3 p = hitPoint.normalized;
                float absX = Mathf.Abs(p.x);
                float absY = Mathf.Abs(p.y);
                float absZ = Mathf.Abs(p.z);

                int face;
                float fx, fy;

                if (absX >= absY && absX >= absZ)
                {
                    if (p.x > 0f) { face = 0; fx = -p.z / absX; fy = -p.y / absX; }
                    else { face = 1; fx = p.z / absX; fy = -p.y / absX; }
                }
                else if (absY >= absX && absY >= absZ)
                {
                    if (p.y > 0f) { face = 2; fx = p.x / absY; fy = -p.z / absY; }
                    else { face = 3; fx = p.x / absY; fy = -p.z / absY; }
                }
                else
                {
                    if (p.z > 0f) { face = 4; fx = p.x / absZ; fy = -p.y / absZ; }
                    else { face = 5; fx = -p.x / absZ; fy = -p.y / absZ; }
                }

                // Invert fy to align hemisphere mapping with camera
                fy = -fy;

                int x = Mathf.Clamp(Mathf.FloorToInt((fx + 1f) / 2f * tilesPerEdge), 0, tilesPerEdge - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt((fy + 1f) / 2f * tilesPerEdge), 0, tilesPerEdge - 1);

                var id = new TileId((byte)face, (byte)depth, (ushort)x, (ushort)y);
                hitTiles.Add(id);

                if ((i & 63) == 0) // yield periodically (every 64 iterations)
                    yield return null;
            }

            // Snapshot existing keys to avoid modifying collection while iterating
            var existingKeys = tileObjects.Keys.ToList();

            // Spawn/refresh tiles that were hit
            foreach (var id in hitTiles)
            {
                int resolution = ResolveResolutionForDepth(depth);
                GetOrSpawnTile(id, resolution);
                existingKeys.Remove(id);
            }

            // Destroy any tiles that were not hit this pass
            foreach (var id in existingKeys)
            {
                DestroyTile(id);
            }

            // Additionally remove any tiles with mismatched depth
            var mismatched = tileObjects.Keys.Where(k => k.depth != depth).ToList();
            foreach (var id in mismatched)
            {
                DestroyTile(id);
            }

            _heuristicCoroutine = null;
        }

        /// <summary>
        /// Resolve mesh resolution for a given depth to create progressive mesh detail.
        /// Higher depth tiles get exponentially higher resolution meshes to show more geometric detail
        /// of the SAME underlying terrain (height values remain consistent across depths).
        /// </summary>
        private int ResolveResolutionForDepth(int depth)
        {
            if (config != null)
            {
                // Use base resolution from config and scale with depth for more mesh detail
                // This creates progressive geometric detail without changing terrain topology
                int baseRes = config.baseResolution > 0 ? config.baseResolution : 32;
                
                // Scale resolution with depth to maintain consistent world-space detail density
                // Each depth level doubles the tiles per edge but we want to maintain detail per world unit
                int scaledRes = Mathf.Max(8, baseRes >> depth);
                
                // Add extra mesh detail for deeper levels (more vertices, same terrain)
                int extraDetail = depth * 16; // Add 16 verts per edge per depth level
                
                return Mathf.Max(16, scaledRes + extraDetail);
            }
            
            // Conservative fallback with progressive mesh resolution scaling
            return Mathf.Max(16, 32 + depth * 16);
        }
    }
}
