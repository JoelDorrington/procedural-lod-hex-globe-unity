using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using System.Reflection;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileVisibilityManagerImmediateSpawnTests
    {
        [Test]
        public void TrySpawnTile_WorksImmediatelyAfterSetDepth_NoFrameWait()
        {
            var go = new GameObject("mgr_immediate");
            var mgr = go.AddComponent<PlanetTileVisibilityManager>();

            // Ensure the static registry is cleared to test synchronous population behavior
            var regField = typeof(PlanetTileVisibilityManager).GetField("s_precomputedRegistry", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(regField, "s_precomputedRegistry field expected");
            var registry = (Dictionary<int, List<PlanetTileVisibilityManager.PrecomputedTileEntry>>)regField.GetValue(null);
            if (registry == null)
            {
                registry = new Dictionary<int, List<PlanetTileVisibilityManager.PrecomputedTileEntry>>();
                regField.SetValue(null, registry);
            }
            registry.Clear();

            // Act: call SetDepth and immediately attempt to spawn without yielding a frame.
            mgr.SetDepth(0);

            var tileId = new TileId(0, 0, 0, 0);
            var spawned = mgr.TrySpawnTile(tileId, resolution: 8);

            // Expectation: spawn should succeed synchronously when SetDepth is called.
            Assert.IsNotNull(spawned, "TrySpawnTile should spawn a tile immediately after SetDepth without waiting a frame");

            Object.DestroyImmediate(go);
        }
    }
}
