using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// This test verifies the depth-to-resolution mapping logic directly
    /// by testing the resolution calculation formula without spawning tiles.
    /// </summary>
    public class DepthResolutionMappingTests
    {
        [Test]
        public void DeeperDepth_ShouldHaveHigherResolution()
        {
            // Setup a deterministic config
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 100f;
            cfg.heightScale = 10f;
            cfg.baseResolution = 8; // base resolution per tile side

            // Create manager and assign config
            var go = new GameObject("TestManager");
            var mgr = go.AddComponent<PlanetTileVisibilityManager>();
            mgr.config = cfg;

            try
            {
                // Test the resolution mapping logic directly using reflection
                var resolutionMethod = typeof(PlanetTileVisibilityManager).GetMethod("resolutionForBuilder",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(resolutionMethod, "resolutionForBuilder method should exist");

                // Test resolution at different depths
                var tileId0 = new TileId(0, 0, 0, 0); // depth 0
                var tileId1 = new TileId(0, 0, 0, 1); // depth 1  
                var tileId2 = new TileId(0, 0, 0, 2); // depth 2

                int resolution0 = (int)resolutionMethod.Invoke(mgr, new object[] { tileId0 });
                int resolution1 = (int)resolutionMethod.Invoke(mgr, new object[] { tileId1 });
                int resolution2 = (int)resolutionMethod.Invoke(mgr, new object[] { tileId2 });

                // Verify the exponential progression: baseResolution << depth
                Assert.AreEqual(8, resolution0, "Depth 0 should have base resolution (8)");
                Assert.AreEqual(16, resolution1, "Depth 1 should have 2x base resolution (16)");
                Assert.AreEqual(32, resolution2, "Depth 2 should have 4x base resolution (32)");

                // Verify each deeper depth has strictly higher resolution
                Assert.Greater(resolution1, resolution0, "Depth 1 should have higher resolution than depth 0");
                Assert.Greater(resolution2, resolution1, "Depth 2 should have higher resolution than depth 1");

                // Verify the mathematical relationship: resolution = baseResolution << depth
                Assert.AreEqual(cfg.baseResolution << 0, resolution0, "Depth 0 resolution formula");
                Assert.AreEqual(cfg.baseResolution << 1, resolution1, "Depth 1 resolution formula");
                Assert.AreEqual(cfg.baseResolution << 2, resolution2, "Depth 2 resolution formula");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolutionMapping_WithDifferentBaseResolutions_ShouldScale()
        {
            var testCases = new[] { 4, 8, 16, 32 };

            foreach (int baseRes in testCases)
            {
                var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
                cfg.baseResolution = baseRes;

                var go = new GameObject($"TestManager_{baseRes}");
                var mgr = go.AddComponent<PlanetTileVisibilityManager>();
                mgr.config = cfg;

                try
                {
                    var resolutionMethod = typeof(PlanetTileVisibilityManager).GetMethod("resolutionForBuilder",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    for (int depth = 0; depth <= 3; depth++)
                    {
                        var tileId = new TileId(0, 0, 0, depth);
                        int actualResolution = (int)resolutionMethod.Invoke(mgr, new object[] { tileId });
                        int expectedResolution = baseRes << depth;

                        Assert.AreEqual(expectedResolution, actualResolution, 
                            $"Base resolution {baseRes} at depth {depth} should produce resolution {expectedResolution}");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(cfg);
                    Object.DestroyImmediate(go);
                }
            }
        }
    }
}
