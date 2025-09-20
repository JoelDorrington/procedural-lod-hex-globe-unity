using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class IcosphereMappingTests
    {
        [Test]
        public void TileCenterWorldDirectionMapsToSameFace()
        {
            for (int depth = 0; depth <= 3; depth++)
            {
                int tilesPerEdge = 1 << depth;
                for (int face = 0; face < 20; face++)
                {
                    for (int x = 0; x < tilesPerEdge; x++)
                    {
                        for (int y = 0; y < tilesPerEdge; y++)
                        {
                            if (!IcosphereTestHelpers.IsValidTileIndex(x, y, depth)) continue;
                            // Use canonical Bary center computation
                            var center = IcosphereMapping.GetTileBaryCenter(depth, x, y);
                            Vector3 dir = IcosphereMapping.BaryToWorldDirection(face, center).normalized;
                            IcosphereMapping.WorldDirectionToFaceIndex(dir, out int foundFace);
                            Assert.AreEqual(face, foundFace, $"Face mismatch at depth={depth} face={face} x={x} y={y}");
                        }
                    }
                }
            }
        }

        [Test]
        public void TileIdFaceNormal_EqualsBaryCenterDirection()
        {
            int depth = 3;
            int tilesPerEdge = 1 << depth;
            for (int face = 0; face < 20; face++)
            {
                for (int x = 0; x < tilesPerEdge; x++)
                {
                    for (int y = 0; y < tilesPerEdge; y++)
                    {
                        if (!IcosphereTestHelpers.IsValidTileIndex(x, y, depth)) continue;
                        var id = new TileId(face, x, y, depth);
                        // Use canonical Bary center computation
                        var center = IcosphereMapping.GetTileBaryCenter(depth, x, y);
                        Vector3 expected = IcosphereMapping.BaryToWorldDirection(face, center).normalized;
                        Vector3 actual = id.faceNormal.normalized;
                        Assert.LessOrEqual(Vector3.Distance(expected, actual), 1e-6f, $"Face normal mismatch for {id}");
                    }
                }
            }
        }
    }
}
