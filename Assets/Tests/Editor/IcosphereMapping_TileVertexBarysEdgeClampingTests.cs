using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    public class IcosphereMapping_TileVertexBarysEdgeClampingTests
    {

        private void IterateTileIds()
        {

            Vector3 planetCenter = Vector3.zero;
            float planetRadius = 1f;

            var registry0 = new TerrainTileRegistry(0, planetRadius, planetCenter);
            var registry1 = new TerrainTileRegistry(1, planetRadius, planetCenter);
            var registry2 = new TerrainTileRegistry(2, planetRadius, planetCenter);

            var registries = new TerrainTileRegistry[] { registry0, registry1, registry2 };

            foreach (var registry in registries)
            {
                foreach (var tileId in registry.tiles.Keys)
                {
                }
            }
        }

        [Test]
        public void TileVertexBarys_AllEdgePointsHaveZeroUVOrW([Values(4, 8, 16, 32, 64, 128, 512)] int res)
        {

            int count = 0;
            int i = 0, j = 0;

            foreach (var uv in IcosphereMapping.TileVertexBarys(res))
            {
                float testU = uv[0];
                float testV = uv[1];
                float testW = 1f - testU - testV;
                count++;

                // w must never be negative; small positive residuals are acceptable
                Assert.IsTrue(testW >= 0f, $"Computed w < 0 at i={i}, j={j}, res={res}: w={testW} (u={testU}, v={testV})");

                bool isEdge = (i == 0) || (j == 0) || (i + j == res - 1);

                if (isEdge)
                {
                    bool uZero = Mathf.Abs(testU) <= 0f;
                    bool vZero = Mathf.Abs(testV) <= 0f;
                    // Accept exact zero or small positive residuals up to 1e-6
                    bool wAccept = (testW >= 0f && testW < 1e-6f);

                    Assert.IsTrue(uZero || vZero || wAccept,
                        $"Edge lattice point at i={i}, j={j}, res={res} produced (u={testU}, v={testV}, w={testW}) which does not satisfy edge zero-or-small-positive requirement.");
                }

                // Increment i,j in the triangular lattice
                i++;
                if (i + j >= res)
                {
                    j++;
                    i = 0;
                }
            }

            // Ensure we iterated the expected number of vertices
            int expectedCount = res * (res + 1) / 2;
            // We can't directly count here because foreach consumed iterator; replicate to count
            foreach (var _ in IcosphereMapping.TileVertexBarys(res))
                Assert.AreEqual(expectedCount, count, $"TileVertexBarys for res={res} must yield {expectedCount} vertices");
        }
    }
}
