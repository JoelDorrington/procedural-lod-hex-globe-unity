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
            var sampleId = new TileId(0, 0, 0, 0);
            foreach (var local in IcosphereMapping.TileVertexBarys(res))
            {
                // convert local indices (i,j) to global barycentric coordinates
                var global = IcosphereMapping.BaryLocalToGlobal(sampleId, local, res);
                allBarys.Add(global);
                total++;
                Debug.Log($"res={res} idx={total-1} bary={global}");
                // allow small epsilon for float comparisons
                if (global.U < 1e-6f) left++;
                if (global.V < 1e-6f) bottom++;
                if (global.W < 1e-6f) hyp++;
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
