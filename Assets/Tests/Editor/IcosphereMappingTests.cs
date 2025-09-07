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
                            if (!IcosphereMapping.IsValidTileIndex(x, y, depth)) continue;
                            // Use canonical barycentric center computation
                            IcosphereMapping.GetTileBarycentricCenter(x, y, depth, out float u, out float v);
                            Vector3 dir = IcosphereMapping.BarycentricToWorldDirection(face, u, v).normalized;
                            IcosphereMapping.WorldDirectionToTileFaceIndex(dir, out int foundFace);
                            Assert.AreEqual(face, foundFace, $"Face mismatch at depth={depth} face={face} x={x} y={y}");
                        }
                    }
                }
            }
        }

        [Test]
        public void TileVertexToBarycentricCoordinates_ProducesValidTriangleCoords()
        {
            var testTile = new TileId(0, 0, 0, 2);
            int resolution = 16;
            for (int i = 0; i < resolution; i += 3)
            {
                for (int j = 0; j < resolution; j += 3)
                {
                    IcosphereMapping.TileVertexToBarycentricCoordinates(testTile, i, j, resolution, out float u, out float v);
                    Assert.That(u, Is.InRange(0f, 1f), $"u out of range: {u}");
                    Assert.That(v, Is.InRange(0f, 1f), $"v out of range: {v}");
                    Assert.LessOrEqual(u + v, 1f + 1e-6f, $"u+v exceeded 1: u={u}, v={v}");
                }
            }
        }

        [Test]
        public void TileIdFaceNormal_EqualsBarycentricCenterDirection()
        {
            int depth = 3;
            int tilesPerEdge = 1 << depth;
            for (int face = 0; face < 20; face++)
            {
                for (int x = 0; x < tilesPerEdge; x++)
                {
                    for (int y = 0; y < tilesPerEdge; y++)
                    {
                        if (!IcosphereMapping.IsValidTileIndex(x, y, depth)) continue;
                        var id = new TileId(face, x, y, depth);
                        // Use canonical barycentric center computation
                        IcosphereMapping.GetTileBarycentricCenter(x, y, depth, out float u, out float v);
                        Vector3 expected = IcosphereMapping.BarycentricToWorldDirection(face, u, v).normalized;
                        Vector3 actual = id.faceNormal.normalized;
                        Assert.LessOrEqual(Vector3.Distance(expected, actual), 1e-6f, $"Face normal mismatch for {id}");
                    }
                }
            }
        }
    }
}
