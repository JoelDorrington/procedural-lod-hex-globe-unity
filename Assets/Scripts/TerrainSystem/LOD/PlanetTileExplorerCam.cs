using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.TerrainSystem.LOD
{
    // Behaves like a Google Maps tile explorer for planet terrain
    // Zoom level and depth are interchangeable; zoom determines tile depth.
    // At each zoom/depth, the ocean sphere surface is conceptually divided into square tile coordinates.
    // A few hundred rays are cast from the camera to the sphere; hit locations determine which tiles to spawn.
    // This heuristic runs ~30 times per second. Tile instances are tracked to avoid duplicates and premature destruction.
    // When zooming/changing depth, all old tiles are removed and replaced with tiles of the new depth on the next raycast pass.
    [AddComponentMenu("HexGlobe/Planet Tile Explorer Cam")]
    public class PlanetTileExplorerCam : MonoBehaviour
    {
        private int _lastDepth = -1;
        private void Update()
        {
            if (_heuristicCoroutine == null && Time.time - _lastHeuristicTime >= HeuristicInterval)
            {
                _lastHeuristicTime = Time.time;
                if (maxDepth != _lastDepth)
                {
                    Debug.Log($"[TileRaycast] Depth changed: {maxDepth}");
                    _lastDepth = maxDepth;
                }
                _heuristicCoroutine = StartCoroutine(RunTileRaycastHeuristicCoroutine());
            }
        }
        private PlanetTileMeshBuilder meshBuilder;
        [SerializeField]
        [Tooltip("Reference to the PlanetLodManager controlling terrain and LOD")]
        private PlanetLodManager manager;
        [SerializeField]
        [Tooltip("Game Camera")]
        public Camera GameCamera;
        private int maxDepth = 2; // Soft cap for recursion
        private Dictionary<TileId, GameObject> tileObjects = new();
        private Coroutine _heuristicCoroutine;
        private float _lastHeuristicTime = 0f;
        private const float HeuristicInterval = 1f / 30f; // ~30 times per second
        private int _maxRays = 200; // Default max rays
        private Dictionary<TileId, float> tileSpawnTimes = new();

        private void Awake()
        {
            // You must assign manager from another script or inspector before using meshBuilder
            if (manager != null)
            {
                meshBuilder = new PlanetTileMeshBuilder(manager.Config, manager.Config.heightProvider, manager.OctaveWrapper, manager.BakedDepth, manager.SplitChildResolutionMultiplier, manager.ChildHeightEnhancement, manager.EdgePromotionRebuild);
            }
            else
            {
                Debug.LogError("PlanetTileExplorerCam: PlanetLodManager reference not assigned. Please assign it in the inspector.");
            }
        }

        /// <summary>
        /// Updates visible tiles based on ray tracing heuristic only.
        /// </summary>
        public void UpdateVisibleTiles()
        {
            // No-op: visibility is now managed exclusively by the ray tracing heuristic coroutine.
        }

        // Fetch or spawn a tile at given coordinates and depth
        public GameObject GetOrSpawnTile(TileId id, int resolution)
        {
            if (tileObjects.TryGetValue(id, out var go))
            {
                if (go != null)
                {
                    go.SetActive(true);
                    return go;
                }
                else
                {
                    tileObjects.Remove(id);
                }
            }
            tileSpawnTimes[id] = Time.time;
            var tileData = new TileData { id = id, resolution = resolution, isBaked = true };
            float dummyMin = 0, dummyMax = 0;
            meshBuilder.BuildTileMesh(tileData, ref dummyMin, ref dummyMax);
            var spawner = new PlanetTileSpawner();
            spawner.SpawnOrUpdateTileGO(tileData, tileObjects, manager.TerrainMaterial, this.transform);
            tileObjects.TryGetValue(id, out go);
            if (go != null)
            {
                go.SetActive(true);
            }
            return go;
        }

        // Destroy a tile instantly
        public void DestroyTile(TileId id)
        {
            if (tileObjects.TryGetValue(id, out var go) && go != null)
            {
                if (tileSpawnTimes.TryGetValue(id, out float spawnTime) && Time.time - spawnTime < 0.5f)
                {
                    return;
                }
                Object.Destroy(go);
                tileObjects.Remove(id);
                tileSpawnTimes.Remove(id);
            }
        }

        // Heuristic for raycast-based tile spawning
        private System.Collections.IEnumerator RunTileRaycastHeuristicCoroutine()
        {
            if (GameCamera == null || manager == null)
            {
                _heuristicCoroutine = null;
                yield break;
            }
            HashSet<TileId> hitTiles = new HashSet<TileId>();
            int raysToCast = _maxRays;
            int depth = maxDepth;
            int tilesPerEdge = 1 << depth;
            float planetRadius = manager.Config.baseRadius;
            int sqrtRays = Mathf.CeilToInt(Mathf.Sqrt(raysToCast));
            int rayCount = sqrtRays * sqrtRays;
            for (int i = 0; i < rayCount; i++)
            {
                float u = (i % sqrtRays + 0.5f) / sqrtRays;
                float v = (i / sqrtRays + 0.5f) / sqrtRays;
                Ray ray = GameCamera.ViewportPointToRay(new Vector3(u, v, 0));
                Vector3 camToPlanet = manager.transform.position - ray.origin;
                float t = Vector3.Dot(camToPlanet, ray.direction);
                Vector3 closest = ray.origin + ray.direction * t;
                float distToCenter = (closest - manager.transform.position).magnitude;
                if (distToCenter > planetRadius) continue;
                float a = Vector3.Dot(ray.direction, ray.direction);
                float b = 2f * Vector3.Dot(ray.direction, ray.origin - manager.transform.position);
                float c = (ray.origin - manager.transform.position).sqrMagnitude - planetRadius * planetRadius;
                float discriminant = b * b - 4 * a * c;
                if (discriminant < 0) continue;
                float sqrtDisc = Mathf.Sqrt(discriminant);
                float t0 = (-b - sqrtDisc) / (2 * a);
                if (t0 < 0) continue;
                Vector3 hitPoint = ray.origin + ray.direction * t0;
                // Map hitPoint to cube face and tile coordinates
                Vector3 p = hitPoint.normalized;
                float absX = Mathf.Abs(p.x);
                float absY = Mathf.Abs(p.y);
                float absZ = Mathf.Abs(p.z);
                // Fix longitude (north-south hemisphere) reversal by negating fy
                // Calculate fx, fy as before
                int face; float fx, fy;
                if (absX >= absY && absX >= absZ) {
                    if (p.x > 0) {
                        face = 0; fx = -p.z / absX; fy = -p.y / absX;
                    } else {
                        face = 1; fx = p.z / absX; fy = -p.y / absX;
                    }
                } else if (absY >= absX && absY >= absZ) {
                    if (p.y > 0) {
                        face = 2; fx = p.x / absY; fy = -p.z / absY;
                    } else {
                        face = 3; fx = p.x / absY; fy = -p.z / absY;
                    }
                } else {
                    if (p.z > 0) {
                        face = 4; fx = p.x / absZ; fy = -p.y / absZ;
                    } else {
                        face = 5; fx = -p.x / absZ; fy = -p.y / absZ;
                    }
                }
                // Invert fy for all faces to fix hemisphere selection
                fy = -fy;
                int x = Mathf.Clamp(Mathf.FloorToInt((fx + 1f) / 2f * tilesPerEdge), 0, tilesPerEdge - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt((fy + 1f) / 2f * tilesPerEdge), 0, tilesPerEdge - 1);
                var id = new TileId((byte)face, (byte)depth, (ushort)x, (ushort)y);
                if (!hitTiles.Contains(id))
                {
                    hitTiles.Add(id);
                }
                if (i % 50 == 0) yield return null;
            }
            HashSet<TileId> toRemove = new HashSet<TileId>(tileObjects.Keys);
            foreach (var id in hitTiles)
            {
                int resolution = manager.ResolveResolutionForDepth(depth, tilesPerEdge);
                GetOrSpawnTile(id, resolution);
                toRemove.Remove(id);
            }
            // Remove tiles not in hitTiles or with mismatched depth
            foreach (var id in toRemove)
            {
                DestroyTile(id);
            }
            // Remove tiles with mismatched depth
            foreach (var kv in tileObjects)
            {
                if (kv.Key.depth != depth)
                {
                    DestroyTile(kv.Key);
                }
            }
            _heuristicCoroutine = null;
        }
    }
}
