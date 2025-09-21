using NUnit.Framework;
using System;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class IcosphereMapping_BaryLocalToGlobalTests
    {
        [Test]
        public void BaryLocalToGlobal_Reflection_Behavior_IsStableOnBoundary()
        {
            // Verify behavior exactly at the reflection boundary and just inside/outside it.
            var id = new TileId(0, 0, 0, 1);
            int res = 5;

            var local = IcosphereMapping.TileIndexToBaryOrigin(id.depth, id.x, id.y);

            var g = IcosphereMapping.BaryLocalToGlobal(id, local, res);
            // After reflection rule, when u+v == 1 the implementation mirrors only when >1.
            // Expect no reflection here; just ensure g sums to approximately 1.
            Assert.AreEqual(1f, g.U + g.V + g.W, 1e-4f, "Boundary sum should be ~1 when not reflected");

            // Now test a value slightly over 1
            var localOver = new Barycentric(local.U + 0.001f, local.V + 0.001f);
            var gOver = IcosphereMapping.BaryLocalToGlobal(id, localOver, res);
            Assert.Less(gOver.U + gOver.V, 1f + 1e-4f, "Reflected result should bring sum back below or near 1");
        }
    }
}
