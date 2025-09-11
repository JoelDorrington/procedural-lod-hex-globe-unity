using NUnit.Framework;
using UnityEngine;
using System.Linq;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// TDD test: Require that MathVisibilitySelector.GetKRing expands across face boundaries
    /// when the canonical tile lies adjacent to a face edge. This ensures the buffer radius
    /// covers neighboring faces and avoids visible seams at face borders.
    /// </summary>
    public class MathSelector_FaceCrossingKRingTests
    {
        [Test]
        public void KRing_ShouldIncludeAdjacentFaceTiles_WhenCenterNearFaceEdge()
        {
            int depth = 3; // moderate depth to produce many tiles per face

            // Pick a barycentric location close to the triangle edge (u+v ~= 1)
            int face = 0;
            float u = 0.49f;
            float v = 0.51f; // u+v = 1.0 -> near edge; small perturbation to be inside
            Vector3 dir = IcosphereMapping.BarycentricToWorldDirection(face, u, v).normalized;

            var center = MathVisibilitySelector.TileFromDirection(dir, depth);
            Assert.AreEqual(face, center.face, "Sanity: canonical tile should be on the expected face for this direction.");

            var ring = MathVisibilitySelector.GetKRing(center, 1, null);
            Assert.IsNotNull(ring, "GetKRing must not return null.");

            // Expect: at least one neighbor in the k-ring should be on a different face
            bool hasOtherFace = ring.Any(t => t.face != center.face);
            Assert.IsTrue(hasOtherFace, "K-ring must include tiles from adjacent faces when center is near a face edge.");
        }
    }
}
