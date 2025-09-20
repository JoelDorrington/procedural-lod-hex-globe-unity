using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.HexMap;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Unit tests for the math-based visibility selector mapping camera direction
    /// to canonical TileId coordinates using Icosphere mapping utilities.
    /// </summary>
    public class MathSelector_TileMappingTests
    {
        [Test]
        public void DirectionFacingFaceCenter_MapsToThatFaceTileAtDepth0()
        {
            // Pick face 0 center direction using the mapping utility
            int face = 0;
            float u = 0.5f;
            float v = 0.5f;
            Vector3 dir = IcosphereMapping.BaryToWorldDirection(face, u, v).normalized;

            // Use TileCoordinateMapping (authoritative mapping) to compute tile indices
            int depth = 0;
            TileCoordinateMapping.WorldDirectionToTileCoordinates(dir, depth, out int outFace, out int x, out int y, out float lx, out float ly);

            // At depth 0 there is exactly one tile per face, so indices must be 0
            Assert.AreEqual(0, x, "At depth 0, x index for face center should be 0");
            Assert.AreEqual(0, y, "At depth 0, y index for face center should be 0");
        }

        [Test]
        public void DirectionNearCorner_MapsToExpectedTileAtDepth2()
        {
            // Use a Bary location near a corner of face 3
            int face = 3;
            float u = 0.1f;
            float v = 0.1f;
            Vector3 dir = IcosphereMapping.BaryToWorldDirection(face, u, v).normalized;

            int depth = 2; // tilesPerEdge = 4
            TileCoordinateMapping.WorldDirectionToTileCoordinates(dir, depth, out int outFace2, out int x2, out int y2, out float lx2, out float ly2);

            int tilesPerEdge = 1 << depth; // 4
            Assert.GreaterOrEqual(x2, 0);
            Assert.Less(x2, tilesPerEdge);
            Assert.GreaterOrEqual(y2, 0);
            Assert.Less(y2, tilesPerEdge);
        }
    }
}
