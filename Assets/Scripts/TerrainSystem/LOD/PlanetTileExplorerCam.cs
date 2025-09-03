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
        private int _maxRays = 100;

    [Header("Adaptive Ray Distribution")]
    [SerializeField]
    [Tooltip("Exponent for radial bias smoothing function. Higher values concentrate rays more toward sphere edges.")]
    private float radialBiasExponent = 2.0f;

    [SerializeField]
    [Tooltip("Debug: show sphere bounds in viewport space")]
    private bool debugShowSphereBounds = false;    [Header("Debug (editor/runtime)")]
    [SerializeField]
    [Tooltip("Draw sampled rays in the Scene view / Game view when running.")]
    private bool debugDrawRays = false;

    [SerializeField]
    [Tooltip("Color used for rays that did not hit the sphere")]
    private Color debugRayColor = Color.yellow;

    [SerializeField]
    [Tooltip("Length for debug ray visualization (in world units)")]
    private float debugRayLength = 1000f;

    // Runtime storage for last sampling pass (not serialized)
    private List<Vector3> _dbgSampleOrigins = new();
    private List<Vector3> _dbgSampleDirs = new();
    private List<bool> _dbgSampleHit = new();
    // timestamps for each debug sample so we can keep samples visible for a short time to avoid flicker
    private List<float> _dbgSampleTime = new();

    [SerializeField]
    [Tooltip("How long (seconds) to keep debug samples visible before they're culled - prevents flicker when samples are updated asynchronously.")]
    private float debugSampleLifetime = 0.25f;

        private void OnDrawGizmos()
        {
            if (!debugDrawRays) 
            {
                // clear any stored samples to avoid unbounded growth
                _dbgSampleOrigins.Clear();
                _dbgSampleDirs.Clear();
                _dbgSampleHit.Clear();
                _dbgSampleTime.Clear();
                return;
            }

            // Draw all stored samples (do not pop them) to avoid flicker when Gizmos updates between frames.
            // Samples are kept for debugSampleLifetime seconds and then removed.
            float now = Time.realtimeSinceStartup;
            int count = _dbgSampleOrigins.Count;
            for (int i = 0; i < count; i++)
            {
                Vector3 o = _dbgSampleOrigins[i];
                Vector3 d = _dbgSampleDirs[i];
                bool hit = _dbgSampleHit[i];
                Gizmos.color = hit ? Color.green : debugRayColor;
                Gizmos.DrawLine(o, o + d * debugRayLength);
                Gizmos.DrawSphere(o + d * Mathf.Min(debugRayLength, 10f), 0.5f);
            }

            // Cull old samples while iterating backwards to keep list indices valid
            for (int i = _dbgSampleOrigins.Count - 1; i >= 0; i--)
            {
                if (now - _dbgSampleTime[i] > debugSampleLifetime)
                {
                    _dbgSampleOrigins.RemoveAt(i);
                    _dbgSampleDirs.RemoveAt(i);
                    _dbgSampleHit.RemoveAt(i);
                    _dbgSampleTime.RemoveAt(i);
                }
            }

            // Debug: draw sphere bounds in viewport
            if (debugShowSphereBounds && GameCamera != null)
            {
                Camera cam = GameCamera.GetComponent<Camera>();
                if (cam != null)
                {
                    Vector3 planetCenter = planetTransform != null ? planetTransform.position : this.transform.position;
                    float cameraDistance = (cam.transform.position - planetCenter).magnitude;
                    float planetRadius = config != null ? config.baseRadius : 1f;
                    
                    if (cameraDistance > planetRadius && planetRadius > 0f)
                    {
                        float angularRadius = Mathf.Asin(planetRadius / cameraDistance);
                        float vFovHalf = cam.fieldOfView * Mathf.Deg2Rad * 0.5f;
                        float sphereViewportRadius = Mathf.Tan(angularRadius) / Mathf.Tan(vFovHalf);
                        sphereViewportRadius = Mathf.Clamp(sphereViewportRadius, 0f, 1.0f);
                        
                        // Draw sphere bounds as a circle in screen space
                        Gizmos.color = Color.cyan;
                        Vector3 screenCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, cam.nearClipPlane + 1f));
                        float distance = cam.nearClipPlane + 1f;
                        float worldRadius = sphereViewportRadius * distance * Mathf.Tan(vFovHalf) * 2f;
                        
                        // Draw circle approximation
                        int segments = 32;
                        Vector3 prevPoint = screenCenter + cam.transform.right * worldRadius;
                        for (int i = 1; i <= segments; i++)
                        {
                            float angle = (float)i / segments * 2f * Mathf.PI;
                            Vector3 point = screenCenter + cam.transform.right * (Mathf.Cos(angle) * worldRadius) 
                                                        + cam.transform.up * (Mathf.Sin(angle) * worldRadius);
                            Gizmos.DrawLine(prevPoint, point);
                            prevPoint = point;
                        }
                    }
                }
            }
        }

        [SerializeField]
        [Tooltip("Viewport padding (0..0.2) - fraction of the viewport to sample outside the screen so tiles are spawned slightly before they enter view. Clamped to 20% max. Example: 0.05 = 5% padding on each side.")]
        private float viewportPadding = 0.05f;

        // stitching removed: tile meshes rely on shared sample coordinates for edge consistency

        // ----- Internal state -----
        private PlanetTileMeshBuilder meshBuilder;
        private readonly Dictionary<TileId, GameObject> tileObjects = new();
        private readonly Dictionary<TileId, float> tileSpawnTimes = new();
        private Vector2[] _baseSamples = null;
        private int _baseSampleGrid = 0; // sqrtRays
        private int _sampleCapacity = 0;

        private void EnsureBaseSamples(int sqrtRays)
        {
            if (_baseSamples != null && _baseSampleGrid == sqrtRays) return;
            _baseSampleGrid = sqrtRays;
            _sampleCapacity = sqrtRays * sqrtRays;
            _baseSamples = new Vector2[_sampleCapacity];
            int idx = 0;
            for (int y = 0; y < sqrtRays; y++)
            {
                for (int x = 0; x < sqrtRays; x++)
                {
                    _baseSamples[idx++] = new Vector2((x + 0.5f) / sqrtRays, (y + 0.5f) / sqrtRays);
                }
            }
        }

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

            // Ensure viewport padding stays in a reasonable range to avoid excessive off-screen sampling
                float originalPadding = viewportPadding;
                viewportPadding = Mathf.Clamp(viewportPadding, 0f, 0.2f);
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
            // Debug logging for north pole tiles
            bool isNorthPole = (id.face == 2 && debugDrawRays && id.depth > 0);
            
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

            bool foundAfterSpawn = tileObjects.TryGetValue(id, out go);

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
            EnsureBaseSamples(sqrtRays);

            Vector3 planetCenter = planetTransform != null ? planetTransform.position : this.transform.position;

            // Expand sampling range by viewportPadding so we cast rays slightly off-screen.
            // Apply an extra vertical multiplier to account for aspect differences.
            float vPadMultiplier = 1.618f;
            float padH = Mathf.Clamp01(viewportPadding);
            float padV = Mathf.Clamp01(viewportPadding) * vPadMultiplier;

            float minU = -padH;
            float maxU = 1f + padH;
            float minV = -padV;
            float maxV = 1f + padV;

            int iter = 0;
            
            // Calculate sphere's angular radius and viewport bounds for adaptive ray distribution
            float cameraDistance = (cam.transform.position - planetCenter).magnitude;
            
            // Calculate angular radius of sphere as seen from camera
            float angularRadius = 0f;
            float sphereViewportRadius = 0f;
            if (cameraDistance > planetRadius && planetRadius > 0f)
            {
                angularRadius = Mathf.Asin(planetRadius / cameraDistance);
                // Convert angular radius to viewport radius using camera's field of view
                float vFovHalf = cam.fieldOfView * Mathf.Deg2Rad * 0.5f;
                sphereViewportRadius = Mathf.Tan(angularRadius) / Mathf.Tan(vFovHalf);
                sphereViewportRadius = Mathf.Clamp(sphereViewportRadius, 0f, 1.0f);
            }
            
            // Linear transition: ProportionalDistance 1.0 (far) = full bias, 0.0 (close) = even distribution
            float biasStrength = GameCamera != null ? GameCamera.ProportionalDistance : 0f;
            
            // Deterministic sampling over the precomputed grid
            int rayCountIter = sqrtRays * sqrtRays;
            for (int s = 0; s < rayCountIter; s++)
            {
                float su = _baseSamples[s].x;
                float sv = _baseSamples[s].y;

                // Apply adaptive radial bias based on camera distance
                if (biasStrength > 0f && sphereViewportRadius > 0f)
                {
                    // Convert to centered coordinates [-0.5, 0.5]
                    float centeredU = su - 0.5f;
                    float centeredV = sv - 0.5f;
                    float currentRadius = Mathf.Sqrt(centeredU * centeredU + centeredV * centeredV);
                    
                    if (currentRadius > 0f)
                    {
                        // Apply quadratic radial bias - push samples toward sphere edge
                        float normalizedRadius = currentRadius / 0.5f; // normalize to [0,1]
                        float biasedRadius = Mathf.Pow(normalizedRadius, radialBiasExponent);
                        
                        // Scale biased radius to sphere bounds and blend with original based on bias strength
                        float targetRadius = Mathf.Lerp(normalizedRadius, biasedRadius * sphereViewportRadius, biasStrength);
                        targetRadius = Mathf.Min(targetRadius, sphereViewportRadius); // clamp to sphere bounds
                        
                        float scale = (targetRadius * 0.5f) / currentRadius;
                        centeredU *= scale;
                        centeredV *= scale;
                        
                        su = centeredU + 0.5f;
                        sv = centeredV + 0.5f;
                        
                        // Clamp to valid viewport range
                        su = Mathf.Clamp01(su);
                        sv = Mathf.Clamp01(sv);
                    }
                }

                // map into expanded viewport using separate horizontal/vertical padding
                float u = Mathf.Lerp(minU, maxU, su);
                float v = Mathf.Lerp(minV, maxV, sv);

                Ray ray = cam.ViewportPointToRay(new Vector3(u, v, 0f));

                // For rays that would miss the sphere, clamp them to hit the sphere edge
                Vector3 camToPlanet = planetCenter - ray.origin;
                float projectionLength = Vector3.Dot(camToPlanet, ray.direction);
                Vector3 closestPoint = ray.origin + ray.direction * projectionLength;
                float distToCenter = (closestPoint - planetCenter).magnitude;
                
                if (distToCenter > planetRadius)
                {
                    // Clamp ray to sphere edge with minimal padding
                    float sphereRadiusWithPadding = planetRadius * 0.999f; // slight inward padding
                    Vector3 directionToClosest = (closestPoint - planetCenter).normalized;
                    Vector3 clampedPoint = planetCenter + directionToClosest * sphereRadiusWithPadding;
                    ray.direction = (clampedPoint - ray.origin).normalized;
                }

                // Ray-sphere intersection test (should always hit now after clamping)
                float a = Vector3.Dot(ray.direction, ray.direction);
                float b = 2f * Vector3.Dot(ray.direction, ray.origin - planetCenter);
                float c = (ray.origin - planetCenter).sqrMagnitude - planetRadius * planetRadius;
                float discriminant = b * b - 4f * a * c;
                bool didHit = discriminant >= 0f;

                float t0 = -1f;
                if (didHit)
                {
                    float sqrtDisc = Mathf.Sqrt(Mathf.Max(0f, discriminant));
                    t0 = (-b - sqrtDisc) / (2f * a);
                    if (t0 < 0f) didHit = false; // intersection behind camera
                }

                if (debugDrawRays)
                {
                    _dbgSampleOrigins.Add(ray.origin);
                    _dbgSampleDirs.Add(ray.direction.normalized);
                    _dbgSampleHit.Add(didHit);
                    _dbgSampleTime.Add(Time.realtimeSinceStartup);
                }

                if (!didHit) continue;

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

                    iter++;
                    if ((iter & 63) == 0) // yield periodically (every 64 iterations)
                        yield return null;
                }

            // Snapshot existing keys to avoid modifying collection while iterating
            var existingKeys = tileObjects.Keys.ToList();

            // Spawn/refresh tiles that were hit
            foreach (var id in hitTiles)
            {
                int resolution = ResolveResolutionForDepth(depth);
                
                GameObject tileGO = GetOrSpawnTile(id, resolution);
                
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
