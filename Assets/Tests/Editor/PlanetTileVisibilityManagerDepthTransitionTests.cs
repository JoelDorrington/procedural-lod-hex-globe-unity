using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileVisibilityManagerDepthTransitionTests
    {
        [Test]
        public void SetDepth_Zero_Spawns20Tiles()
        {
            var mgrGO = new GameObject("PTVM_Manager");
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();

            // Provide a minimal TerrainConfig so the manager uses deterministic values
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 1f;
            cfg.baseResolution = 8;
            cfg.heightScale = 0f;
            mgr.config = cfg;

            var planetGO = new GameObject("PlanetRoot");
            mgr.GetType().GetField("planetTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
               .SetValue(mgr, planetGO.transform);

            // Act: set depth 0 which should precompute and spawn one tile per icosphere face (20)
            mgr.SetDepth(0);

            var active = mgr.GetActiveTiles();

            // Expect 20 spawned tiles (one per icosphere face at depth 0)
            Assert.AreEqual(20, active.Count, "Depth 0 should spawn 20 tiles (one per icosphere face).");

            Object.DestroyImmediate(mgrGO);
            Object.DestroyImmediate(planetGO);
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void SetDepth_Depths0_1_2_SpawnExpectedCounts()
        {
            for (int depth = 0; depth <= 2; depth++)
            {
                var mgrGO = new GameObject($"PTVM_Manager_d{depth}");
                var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();

                // Provide a minimal TerrainConfig so the manager uses deterministic values
                var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
                cfg.baseRadius = 1f;
                cfg.baseResolution = 8;
                cfg.heightScale = 0f;
                mgr.config = cfg;

                var planetGO = new GameObject($"PlanetRoot_d{depth}");
                mgr.GetType().GetField("planetTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                   .SetValue(mgr, planetGO.transform);

                // Act: set depth which should precompute and spawn the expected number of tiles
                mgr.SetDepth(depth);

                var active = mgr.GetActiveTiles();

                int expected = 20 * (int)Mathf.Pow(4, depth);
                Assert.AreEqual(expected, active.Count, $"Depth {depth} should spawn {expected} tiles.");

                Object.DestroyImmediate(mgrGO);
                Object.DestroyImmediate(planetGO);
                Object.DestroyImmediate(cfg);
            }
        }
    }
}
