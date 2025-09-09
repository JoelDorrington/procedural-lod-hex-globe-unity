using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// This test exposes reversed depth-to-resolution mapping by asserting that
    /// a tile spawned at a higher depth must have strictly more vertices than
    /// a tile spawned at depth 0 (coarser).
    /// </summary>
    public class DepthResolutionMappingTests
    {
        [Test]
        public void DeeperDepth_ShouldHaveHigherVertexCount()
        {
            // Setup a deterministic config
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 100f;
            cfg.heightScale = 10f;
            cfg.baseResolution = 8; // base resolution per tile side
            cfg.heightProvider = new SimplePerlinHeightProvider { seed = 42, octaves = 3 };

            // Create manager and assign config
            var go = new GameObject("TestManager");
            var mgr = go.AddComponent<PlanetTileVisibilityManager>();
            mgr.config = cfg; // public serialized field

            // Ensure planet transform exists and is centered
            var planet = new GameObject("PlanetRoot");
            var pt = planet.transform;
            var planetField = typeof(PlanetTileVisibilityManager).GetField("planetTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (planetField != null) planetField.SetValue(mgr, pt);

            try
            {
                // Ensure precomputation & spawn at coarse depth (0)
                mgr.SetDepth(0);
                var active0 = mgr.GetActiveTiles();
                Assert.Greater(active0.Count, 0, "Expected some active tiles at depth 0");
                int vertexCount0 = 0;
                foreach (var t in active0)
                {
                    if (t.meshFilter != null && t.meshFilter.sharedMesh != null)
                    {
                        vertexCount0 = t.meshFilter.sharedMesh.vertexCount;
                        break;
                    }
                }

                // Now set a deeper depth (higher LOD)
                int deep = 2;
                mgr.SetDepth(deep);
                var activeD = mgr.GetActiveTiles();
                Assert.Greater(activeD.Count, 0, $"Expected some active tiles at depth {deep}");
                int vertexCountD = 0;
                foreach (var t in activeD)
                {
                    if (t.meshFilter != null && t.meshFilter.sharedMesh != null)
                    {
                        vertexCountD = t.meshFilter.sharedMesh.vertexCount;
                        break;
                    }
                }

                // Expect deeper depth to have more vertices than depth 0 (exponential increase)
                Assert.Greater(vertexCountD, vertexCount0, $"Depth {deep} should produce higher vertex count than depth 0 (got {vertexCountD} <= {vertexCount0})");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(planet);
            }
        }
    }
}
