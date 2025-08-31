using UnityEngine;
using System.Collections.Generic;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class TileCache
    {
        public IEnumerable<TileId> GetAllTileIds()
        {
            return tileCache.Keys;
        }
        // Key: (face, depth, x, y)
        private readonly Dictionary<TileId, GameObject> tileCache = new Dictionary<TileId, GameObject>();
        private readonly PlanetTileMeshBuilder meshBuilder;
        private readonly Transform parentTransform;
        private readonly Material terrainMaterial;

        public TileCache(PlanetTileMeshBuilder meshBuilder, Transform parentTransform, Material terrainMaterial)
        {
            this.meshBuilder = meshBuilder;
            this.parentTransform = parentTransform;
            this.terrainMaterial = terrainMaterial;
        }

        // Request a tile mesh GameObject of a given size and coordinates
        public GameObject GetOrCreateTile(TileId id, int resolution)
        {
            if (tileCache.TryGetValue(id, out var go) && go != null)
                return go;

            // Generate mesh and GameObject
            var tileData = new TileData { id = id, resolution = resolution, isBaked = true };
            float dummyMin = 0, dummyMax = 0;
            meshBuilder.BuildTileMesh(tileData, ref dummyMin, ref dummyMax);
            var spawner = new PlanetTileSpawner();
            spawner.SpawnOrUpdateTileGO(tileData, tileCache, terrainMaterial, parentTransform);
            tileCache.TryGetValue(id, out go);
            return go;
        }

        // Optionally, add logic to remove or deactivate tiles not visible/adjacent
        public void SetTileActive(TileId id, bool active)
        {
            if (tileCache.TryGetValue(id, out var go) && go != null)
                go.SetActive(active);
        }

        // Determine visible/adjacent tiles (stub for integration)
        public IEnumerable<TileId> GetVisibleOrAdjacentTiles(Camera cam, float zoomLevel)
        {
            // TODO: Implement frustum/adjacency logic
            yield break;
        }
    }
}
