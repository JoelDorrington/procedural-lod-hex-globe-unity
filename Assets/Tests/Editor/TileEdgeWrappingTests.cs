using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Test that exposes edge-wrapping problems by comparing canonical
    /// barycentric coords and world directions for shared edge vertices.
    /// This test intentionally does NOT modify production code; it will
    /// fail when the mapping or sampling wraps or remaps edge coordinates.
    /// </summary>
    public class TileEdgeWrappingTests
    {
        [Test]
        public void AdjacentTiles_RightLeftEdge_GlobalCoordsAndDirections_ShouldMatch()
        {
            // Adjacent tiles on face 0 at depth 1 (tile A at x=0 right edge, tile B at x=1 left edge)
            var tileA = new TileId(0, 0, 0, 1);
            var tileB = new TileId(0, 1, 0, 1);
            int resolution = 16;

            for (int j = 0; j < resolution; j++)
            {
                // Right edge of tileA (i = resolution-1)
                IcosphereMapping.TileVertexToBarycentricCoordinates(tileA, resolution - 1, j, resolution, out float uA, out float vA);

                // Left edge of tileB (i = 0)
                IcosphereMapping.TileVertexToBarycentricCoordinates(tileB, 0, j, resolution, out float uB, out float vB);

                // The canonical global barycentric coordinates should be identical
                Assert.AreEqual(uA, uB, 1e-6f, $"globalU mismatch at row j={j}: uA={uA} uB={uB}");
                Assert.AreEqual(vA, vB, 1e-6f, $"globalV mismatch at row j={j}: vA={vA} vB={vB}");

                // And their world directions (used for sampling) must match closely
                var dirA = IcosphereMapping.BarycentricToWorldDirection(tileA.face, uA, vA);
                var dirB = IcosphereMapping.BarycentricToWorldDirection(tileB.face, uB, vB);
                float angleDeg = Vector3.Angle(dirA, dirB);
                Assert.Less(angleDeg, 0.01f, $"world direction mismatch at j={j}: angle={angleDeg} deg; dirA={dirA} dirB={dirB}");
            }
        }
    }
}
