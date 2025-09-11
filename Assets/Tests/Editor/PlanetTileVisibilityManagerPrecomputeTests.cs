using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using UnityEngine.TestTools;
using System.Collections;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileVisibilityManagerPrecomputeTests
    {
        [UnityTest]
        public IEnumerator PrecomputedTiles_AreSpawned_OnDepthChange()
        {
            var go = new GameObject("mgr");
            var mgr = go.AddComponent<PlanetTileVisibilityManager>();

            // Ensure depth precomputation runs
            mgr.SetDepth(1);

            // Wait a frame for any editor/play callbacks
            yield return null;

            // Try to spawn a canonical tile at depth 1 (face 0, x=0,y=0 should be valid)
            var tileId = new TileId(0, 0, 0, 1);
            var spawned = mgr.TrySpawnTile(tileId);

            Assert.IsNotNull(spawned, "Manager should spawn a precomputed tile after SetDepth is called");

            Object.DestroyImmediate(go);
        }
    }
}
