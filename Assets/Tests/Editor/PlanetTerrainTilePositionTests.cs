using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTerrainTilePositionTests
    {
        [Test]
        public void Initialize_SetsGameObjectWorldPosition_ToTileCenter()
        {
            var go = new GameObject("test_tile");
            var tile = go.AddComponent<PlanetTerrainTile>();

            var id = new TileId(0, 0, 0, 0);
            var td = new TileData { id = id, resolution = 4, isBaked = true };
            td.center = new Vector3(10.5f, -2.25f, 3.125f);

            tile.Initialize(id, td, colliderMeshGenerator: _ => null);

            Assert.AreEqual(td.center, go.transform.position);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Initialize_WithParent_PreservesWorldPosition_WhenParentMoved()
        {
            var parent = new GameObject("parent");
            parent.transform.position = new Vector3(5f, 5f, 5f);

            var go = new GameObject("test_tile_parented");
            go.transform.SetParent(parent.transform, true);

            var tile = go.AddComponent<PlanetTerrainTile>();

            var id = new TileId(1, 0, 0, 0);
            var td = new TileData { id = id, resolution = 4, isBaked = true };
            td.center = new Vector3(12f, 6f, 5f);

            tile.Initialize(id, td, colliderMeshGenerator: _ => null);

            // World position should equal tile center regardless of parent offset
            Assert.AreEqual(td.center, go.transform.position);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(parent);
        }
    }
}
