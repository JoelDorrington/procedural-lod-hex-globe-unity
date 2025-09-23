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

            var i = 0; var j = 0;
            foreach (var local in IcosphereMapping.TileVertexBarys(res, 0, 0, 0))
            {
                var global = IcosphereMapping.BaryLocalToGlobal(sampleId, local, res);
                float testW = 1f - global.U - global.V;
                count++;

                // w should not be meaningfully negative; allow tiny rounding errors
                const float kW_EPS = 1e-6f;
                Assert.IsTrue(testW >= -kW_EPS, $"Computed w < -eps at i={i}, j={j}, res={res}: w={testW} (u={global.U}, v={global.V})");

                bool isEdge = (i == 0) || (j == 0) || (i + j == res - 1);

                if (isEdge)
                {
                    bool uZero = Mathf.Abs(global.U) <= 1e-6f;
                    bool vZero = Mathf.Abs(global.V) <= 1e-6f;
                    // Accept exact zero or small residuals (positive or tiny negative) within eps
                    bool wAccept = testW >= -kW_EPS && Mathf.Abs(testW) < kW_EPS;

                    Assert.IsTrue(uZero || vZero || wAccept,
                        $"Edge lattice point at i={i}, j={j}, res={res} produced (u={global.U}, v={global.V}, w={testW}) which does not satisfy edge zero-or-small-positive requirement.");
                }
                i++;
                if (i > res - 1 - j)
                {
                    j++;
                    i = 0;
                }
            }

            // Ensure we iterated the expected number of vertices
            int expectedCount = res * (res + 1) / 2;
            // We can't directly count here because foreach consumed iterator; replicate to count
            foreach (var _ in IcosphereMapping.TileVertexBarys(res, 0, 0, 0))
                Assert.AreEqual(expectedCount, count, $"TileVertexBarys for res={res} must yield {expectedCount} vertices");
        }
    }
}
