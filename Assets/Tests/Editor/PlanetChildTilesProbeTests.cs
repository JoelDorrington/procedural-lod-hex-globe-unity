using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using UnityEngine.TestTools;
using System.Collections;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetChildTilesProbeTests
    {
        [UnityTest]
        public IEnumerator SpawnedTiles_AreParentedUnderPlanetTransform()
        {
            // Arrange: create a planet root and a manager
            var planetGO = new GameObject("PlanetRootProbe");
            var mgrGO = new GameObject("VisibilityManagerProbe");
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();

            // Set the manager's private planetTransform to the planet GameObject via reflection
            var fi = typeof(PlanetTileVisibilityManager).GetField("planetTransform", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, "planetTransform field must exist for the manager");
            fi.SetValue(mgr, planetGO.transform);

            // Act: request a depth precompute and spawn a canonical tile
            mgr.SetDepth(0);
            // allow a frame for any initialization
            yield return null;

            var tileId = new TileId(0, 0, 0, 0);
            var spawned = mgr.TrySpawnTile(tileId);

            // Assert: a tile was spawned and it is parented under the planet transform
            Assert.IsNotNull(spawned, "Manager should spawn a tile for a precomputed entry");
            Assert.AreEqual(planetGO.transform, spawned.transform.parent, "Spawned tile should be parented under the planet transform");

            // Cleanup
            Object.DestroyImmediate(mgrGO);
            Object.DestroyImmediate(planetGO);
        }
    }
}
