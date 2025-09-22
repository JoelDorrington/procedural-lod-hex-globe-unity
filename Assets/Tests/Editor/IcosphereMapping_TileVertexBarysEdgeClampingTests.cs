using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    public class IcosphereMapping_TileVertexBarysEdgeClampingTests
    {

        [Test]
        public void TileVertexBarys_AllEdgePointsHaveZeroUVOrW([Values(4, 8, 16, 32, 64)] int res)
        {

            int count = 0;

            // Use a sample tile id for converting local indices into global bary coordinates
            var sampleId = new TileId(0, 0, 0, 0);

            foreach (var local in IcosphereMapping.TileVertexBarys(res))
            {
                // local.U and local.V are integer tile-local indices (i,j)
                int i = Mathf.RoundToInt(local.U);
                int j = Mathf.RoundToInt(local.V);
                var global = IcosphereMapping.BaryLocalToGlobal(sampleId, local, res);
                float testW = 1f - global.U - global.V;
                count++;

                // w must never be negative; small positive residuals are acceptable
                Assert.IsTrue(testW >= 0f, $"Computed w < 0 at i={i}, j={j}, res={res}: w={testW} (u={global.U}, v={global.V})");

                bool isEdge = (i == 0) || (j == 0) || (i + j == res - 1);

                if (isEdge)
                {
                    bool uZero = Mathf.Abs(global.U) <= 1e-6f;
                    bool vZero = Mathf.Abs(global.V) <= 1e-6f;
                    // Accept exact zero or small positive residuals up to 1e-6
                    bool wAccept = (testW >= 0f && testW < 1e-6f);

                    Assert.IsTrue(uZero || vZero || wAccept,
                        $"Edge lattice point at i={i}, j={j}, res={res} produced (u={global.U}, v={global.V}, w={testW}) which does not satisfy edge zero-or-small-positive requirement.");
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
