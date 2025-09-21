using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    public class IcosphereMapping_TileVertexBarysEdgeCountTests
    {
        [Test]
        public void TileVertexBarys_Produces_Res_Vertices_On_Each_Edge([Values(4,8,16,32)] int res)
        {
            // Count vertices on edges: U==0 (left), V==0 (bottom), W==0 (hypotenuse)
            int left = 0, bottom = 0, hyp = 0, total = 0;
            List<Barycentric> allBarys = new List<Barycentric>();
            foreach (var uv in IcosphereMapping.TileVertexBarys(res))
            {
                allBarys.Add(uv);
                total++;
                Debug.Log($"res={res} idx={total-1} bary={uv}");
                // allow small epsilon for float comparisons
                if (uv.U < 1e-6f) left++;
                if (uv.V < 1e-6f) bottom++;
                if (uv.W < 1e-6f) hyp++;
            }
            // Sanity check total vertex count is triangular number
            Assert.AreEqual(res * (res+1) / 2, total, $"Expected triangular vertex count for res={res}");

            // Expected counts: each edge contains exactly 'res' lattice points for our indexing scheme
            Assert.AreEqual(res, left, $"Expected {res} vertices on left edge (U==0) for res={res}, got {left}");
            Assert.AreEqual(res, bottom, $"Expected {res} vertices on bottom edge (V==0) for res={res}, got {bottom}");
            Assert.AreEqual(res, hyp, $"Expected {res} vertices on hypotenuse edge (W==0) for res={res}, got {hyp}");

        }
    }
}
