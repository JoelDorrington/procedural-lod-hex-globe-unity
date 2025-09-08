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
        private readonly Vector3 planetCenter;

        public TileCache(PlanetTileMeshBuilder meshBuilder, Transform parentTransform, Material terrainMaterial, Vector3 planetCenter = default)
        {
            this.meshBuilder = meshBuilder;
            this.parentTransform = parentTransform;
            this.terrainMaterial = terrainMaterial;
            this.planetCenter = planetCenter;
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
            // Directly create and initialize the tile GameObject
            var newGo = new GameObject($"Tile_{id.faceNormal}_d{id.depth}");
            newGo.transform.position = tileData.center;
            newGo.transform.SetParent(parentTransform, true);
            var terrainTile = newGo.AddComponent<PlanetTerrainTile>();
            terrainTile.Initialize(id, tileData, colliderMeshGenerator: _ => tileData.mesh);
            
            // Use centralized material and layer configuration
            int tileLayer = LayerMask.NameToLayer("TerrainTiles");
            terrainTile.ConfigureMaterialAndLayer(terrainMaterial, tileLayer, planetCenter);
            
            tileCache[id] = newGo;
            return newGo;
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
