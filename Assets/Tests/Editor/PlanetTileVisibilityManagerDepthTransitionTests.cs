using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileVisibilityManagerDepthTransitionTests
    {
        [Test]
        public void PrecomputeTileNormalsForDepth_Zero_Generates20TileEntries()
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

            // Get access to precomputed registry to inspect tile enumeration
            var registryField = typeof(PlanetTileVisibilityManager).GetField("s_precomputedRegistry", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(registryField, "s_precomputedRegistry field should exist");

            // Act: call PrecomputeTileNormalsForDepth to generate tile entries for depth 0
            var precomputeMethod = typeof(PlanetTileVisibilityManager).GetMethod("PrecomputeTileNormalsForDepth",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(precomputeMethod, "PrecomputeTileNormalsForDepth method should exist");
            precomputeMethod.Invoke(mgr, new object[] { 0 });

            // Get the registry and check depth 0 entries
            var registry = (Dictionary<int, List<PlanetTileVisibilityManager.PrecomputedTileEntry>>)registryField.GetValue(null);
            Assert.IsTrue(registry.ContainsKey(0), "Registry should contain entries for depth 0");

            var entries = registry[0];
            Assert.AreEqual(20, entries.Count, "Depth 0 should generate 20 tile entries (one per icosphere face)");

            // Verify each face (0-19) has exactly one tile at depth 0
            var faces = new HashSet<int>();
            foreach (var entry in entries)
            {
                Assert.AreEqual(0, entry.x, "Depth 0 tiles should have x=0");
                Assert.AreEqual(0, entry.y, "Depth 0 tiles should have y=0");
                Assert.IsTrue(entry.face >= 0 && entry.face < 20, $"Face {entry.face} should be in range 0-19");
                Assert.IsFalse(faces.Contains(entry.face), $"Face {entry.face} should appear only once");
                faces.Add(entry.face);
            }

            Object.DestroyImmediate(mgrGO);
            Object.DestroyImmediate(planetGO);
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void PrecomputeTileNormalsForDepth_MultipleDepths_GeneratesExpectedTileCounts()
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

            // Get access to precomputed registry and precompute method
            var registryField = typeof(PlanetTileVisibilityManager).GetField("s_precomputedRegistry", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var precomputeMethod = typeof(PlanetTileVisibilityManager).GetMethod("PrecomputeTileNormalsForDepth",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            for (int depth = 0; depth <= 2; depth++)
            {
                // Act: precompute tiles for this depth
                precomputeMethod.Invoke(mgr, new object[] { depth });

                // Get the registry and check entries for this depth
                var registry = (Dictionary<int, List<PlanetTileVisibilityManager.PrecomputedTileEntry>>)registryField.GetValue(null);
                Assert.IsTrue(registry.ContainsKey(depth), $"Registry should contain entries for depth {depth}");

                var entries = registry[depth];
                int expected = 20 * (int)Mathf.Pow(4, depth);
                Assert.AreEqual(expected, entries.Count, $"Depth {depth} should generate {expected} tile entries");

                // Verify tile coordinate ranges are correct for this depth
                int tilesPerEdge = 1 << depth;
                var coordinatePairs = new HashSet<string>();
                
                foreach (var entry in entries)
                {
                    Assert.IsTrue(entry.face >= 0 && entry.face < 20, $"Face {entry.face} should be in range 0-19");
                    Assert.IsTrue(entry.x >= 0 && entry.x < tilesPerEdge, $"X coordinate {entry.x} should be in range 0-{tilesPerEdge-1}");
                    Assert.IsTrue(entry.y >= 0 && entry.y < tilesPerEdge, $"Y coordinate {entry.y} should be in range 0-{tilesPerEdge-1}");
                    
                    string coordKey = $"{entry.face}_{entry.x}_{entry.y}";
                    Assert.IsFalse(coordinatePairs.Contains(coordKey), $"Coordinate combination {coordKey} should be unique");
                    coordinatePairs.Add(coordKey);
                }
            }

            Object.DestroyImmediate(mgrGO);
            Object.DestroyImmediate(planetGO);
            Object.DestroyImmediate(cfg);
        }
    }
}
