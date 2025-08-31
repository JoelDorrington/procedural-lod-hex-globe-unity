using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.TerrainSystem.LOD
{
    // Behaves like a Google Maps tile explorer for planet terrain
    [AddComponentMenu("HexGlobe/Planet Tile Explorer Cam")]
    public class PlanetTileExplorerCam : MonoBehaviour
    {
        private void Update()
        {
            UpdateVisibleTiles();
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
        /// Updates visible tiles based on camera frustum and LOD.
        /// Call this in Update() or when camera moves.
        /// </summary>
        public void UpdateVisibleTiles()
        {
            if (GameCamera == null) return;
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(GameCamera);
            int maxDepthToShow = maxDepth;
            float planetRadius = manager != null ? manager.Config.baseRadius : 1f;
            // For each possible tile at each depth, check if it should be visible
            HashSet<TileId> visibleTiles = new HashSet<TileId>();
            for (int depth = 0; depth <= maxDepthToShow; depth++)
            {
                int tilesPerEdge = 1 << depth;
                int resolution = manager != null ? manager.ResolveResolutionForDepth(depth, tilesPerEdge) : 8;
                for (int face = 0; face < 6; face++)
                {
                    for (int y = 0; y < tilesPerEdge; y++)
                    {
                        for (int x = 0; x < tilesPerEdge; x++)
                        {
                            var id = new TileId((byte)face, (byte)depth, (ushort)x, (ushort)y);
                            // Compute tile center and bounds
                            Vector3 center = CubeSphere.FaceLocalToUnit(face, (x + 0.5f) / tilesPerEdge * 2f - 1f, (y + 0.5f) / tilesPerEdge * 2f - 1f) * planetRadius;
                            float boundsRadius = planetRadius / tilesPerEdge; // Approximate
                            Bounds tileBounds = new Bounds(center, Vector3.one * boundsRadius * 2f);
                            bool frustumVisible = GeometryUtility.TestPlanesAABB(frustumPlanes, tileBounds);
                            // Occlusion check: is tile center facing the camera?
                            Vector3 camPos = GameCamera.transform.position;
                            Vector3 toTile = (center - camPos).normalized;
                            Vector3 camForward = GameCamera.transform.forward;
                            bool facingCamera = Vector3.Dot(toTile, camForward) > 0f;
                            bool visible = frustumVisible && facingCamera;
                            if (visible)
                            {
                                var go = GetOrSpawnTile(id, resolution);
                                visibleTiles.Add(id);
                                if (go != null && !go.activeSelf)
                                {
                                    Debug.Log($"Activated tile: {id}");
                                }
                            }
                        }
                    }
                }
            }
            // Cull (deactivate) tiles that are not visible
            foreach (var kv in tileObjects)
            {
                if (!visibleTiles.Contains(kv.Key) && kv.Value != null)
                {
                    if (kv.Value.activeSelf)
                    {
                        kv.Value.SetActive(false);
                        Debug.Log($"Deactivated tile: {kv.Key}");
                    }
                }
            }
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
                    // Remove stale entry if GameObject was destroyed
                    tileObjects.Remove(id);
                }
            }
            var tileData = new TileData { id = id, resolution = resolution, isBaked = true };
            float dummyMin = 0, dummyMax = 0;
            meshBuilder.BuildTileMesh(tileData, ref dummyMin, ref dummyMax);
            var spawner = new PlanetTileSpawner();
            spawner.SpawnOrUpdateTileGO(tileData, tileObjects, manager.TerrainMaterial, this.transform);
            tileObjects.TryGetValue(id, out go);
            if (go != null) go.SetActive(true);
            return go;
        }

        // Destroy a tile instantly
        public void DestroyTile(TileId id)
        {
            if (tileObjects.TryGetValue(id, out var go) && go != null)
            {
                Object.Destroy(go);
                tileObjects.Remove(id);
            }
        }

        /// <summary>
        /// Spawns a test tile at face 0, depth 0, x 0, y 0, resolution 16.
        /// Call this from the inspector or context menu to verify tile creation.
        /// </summary>
        [ContextMenu("Spawn Test Tile")]
        public void SpawnTestTile()
        {
            var id = new TileId(0, 0, 0, 0);
            int resolution = 16;
            GetOrSpawnTile(id, resolution);
        }
    }
}
