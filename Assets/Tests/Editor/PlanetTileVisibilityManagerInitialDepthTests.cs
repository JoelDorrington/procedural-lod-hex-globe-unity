using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections.Generic;
using System.Reflection;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileVisibilityManagerInitialDepthTests
    {
        [Test]
        public void SetDepth_PopulatesPrecomputedRegistry()
        {
            // Arrange: create manager
            var go = new GameObject("mgr_initialdepth");
            var mgr = go.AddComponent<PlanetTileVisibilityManager>();

            // Access the private static registry via reflection
            var regField = typeof(PlanetTileVisibilityManager).GetField("s_precomputedRegistry", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(regField, "Expected a private static field 's_precomputedRegistry' on PlanetTileVisibilityManager");

            var registry = (Dictionary<int, List<PlanetTileVisibilityManager.PrecomputedTileEntry>>)regField.GetValue(null);
            if (registry == null)
            {
                registry = new Dictionary<int, List<PlanetTileVisibilityManager.PrecomputedTileEntry>>();
                regField.SetValue(null, registry);
            }

            // Ensure clean start
            registry.Clear();

            // Act: request depth precomputation
            mgr.SetDepth(0);

            // Assert: the static registry must contain an entry list for depth 0 and it must not be empty
            Assert.IsTrue(registry.ContainsKey(0), "Precomputed registry should contain depth=0 after SetDepth(0)");
            Assert.IsNotEmpty(registry[0], "Precomputed registry[0] should not be empty after SetDepth(0)");

            Object.DestroyImmediate(go);
        }
    }
}
