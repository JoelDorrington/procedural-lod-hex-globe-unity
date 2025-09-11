using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using System.Linq;

namespace HexGlobeProject.Tests.Editor
{
    public class MathSelector_CandidateGenerationTests
    {
        [Test]
        public void TileFromDirection_And_KRing_Basic()
        {
            Vector3 dir = IcosphereMapping.BarycentricToWorldDirection(0, 0.33f, 0.33f).normalized;
            var tile = MathVisibilitySelector.TileFromDirection(dir, 2);
            Assert.AreEqual(0, tile.face, "Direction near face 0 center should map to face 0");

            var neighbors = MathVisibilitySelector.GetKRing(tile, 1);
            Assert.IsTrue(neighbors.Any(), "K-ring should return at least the center tile");
            Assert.IsTrue(neighbors.Any(n => n.x == tile.x && n.y == tile.y), "Center tile should be included in K-ring");
        }
    }
}
