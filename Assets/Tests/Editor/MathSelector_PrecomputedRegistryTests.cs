using NUnit.Framework;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.Linq;

namespace HexGlobeProject.Tests.Editor
{
    public class MathSelector_PrecomputedRegistryTests
    {
        [Test]
        public void PrecomputedRegistry_PopulatesExpectedCounts_AndNormalsAreUnit()
        {
            var go = new GameObject("PTVM_Test");
            var mgr = go.AddComponent<PlanetTileVisibilityManager>();

            // Depth 0 should produce 20 entries (one per icosphere face)
            mgr.SetDepth(0);
            var list0 = GetPrecomputedForDepth(mgr, 0);
            Assert.IsNotNull(list0);
            Assert.AreEqual(20, list0.Count, "Depth 0 should have 20 precomputed entries");
            foreach (var e in list0)
            {
                Assert.AreEqual(1f, e.normal.magnitude, 1e-4f, "Precomputed normals should be unit length");
            }

            // Depth 2 should produce 20 * 4^2 = 320 entries
            mgr.SetDepth(2);
            var list2 = GetPrecomputedForDepth(mgr, 2);
            Assert.IsNotNull(list2);
            Assert.AreEqual(320, list2.Count, "Depth 2 should have 320 entries");
            foreach (var e in list2)
            {
                Assert.AreEqual(1f, e.normal.magnitude, 1e-4f, "Precomputed normals should be unit length");
            }

            Object.DestroyImmediate(go);
        }

        // Helper uses reflection to access private static registry
        private List<PrecomputedTileEntry> GetPrecomputedForDepth(PlanetTileVisibilityManager mgr, int depth)
        {
            var regField = mgr.GetType().GetField("tileRegistry", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(regField, "tileRegistry expected.");
            var dict = regField.GetValue(mgr) as Dictionary<int, TerrainTileRegistry>;
            if (dict.ContainsKey(depth))
            {
                return dict[depth].tiles.Values.ToList();
            }
            return null;
        }
    }
}
