using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class TerrainTileRegistryDepthTests
    {
        [Test]
        public void TerrainTileRegistry_DepthZero_Generates20TileEntries()
        {
            Vector3 planetCenter = Vector3.zero;
            float planetRadius = 1f;

            var registry = new TerrainTileRegistry(0, planetRadius, planetCenter);

            var entries = registry.tiles.Values;
            Assert.AreEqual(20, entries.Count, "Depth 0 should generate 20 tile entries (one per icosphere face)");

            // Verify normals and corner positions exist for each entry
            foreach (var entry in entries)
            {
                Assert.IsTrue(entry.normal.magnitude > 0.9f && entry.normal.magnitude <= 1.0f, "Precomputed normals should be unit-length (or close).");
                Assert.IsNotNull(entry.cornerWorldPositions, "Each precomputed entry should expose corner world positions.");
                Assert.AreEqual(3, entry.cornerWorldPositions.Length, "Each entry must have exactly 3 corner positions.");
            }
        }

        [Test]
        public void TerrainTileRegistry_MultipleDepths_GeneratesExpectedTileCounts()
        {
            Vector3 planetCenter = Vector3.zero;
            float planetRadius = 1f;

            for (int depth = 0; depth <= 2; depth++)
            {
                var registry = new TerrainTileRegistry(depth, planetRadius, planetCenter);
                var entries = registry.tiles.Values;
                int expected = 20 * (int)Mathf.Pow(4, depth);
                Assert.AreEqual(expected, entries.Count, $"Depth {depth} should generate {expected} tile entries");

                // Verify fundamental invariants: normals and corner arrays
                foreach (var entry in entries)
                {
                    Assert.IsNotNull(entry.cornerWorldPositions, "Corner positions should be present.");
                    Assert.AreEqual(3, entry.cornerWorldPositions.Length, "Each entry must have exactly 3 corner positions.");
                }
            }
        }
    }
}
