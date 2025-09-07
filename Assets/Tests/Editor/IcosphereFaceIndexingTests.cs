using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class IcosphereFaceIndexingTests
    {
        [Test]
        public void WorldDirectionToTileFaceIndex_RoundTrips_ForFaceCentroids()
        {
            // For each icosphere face, convert barycentric centroid to world direction
            // and ensure WorldDirectionToTileFaceIndex returns the original face index.
            for (int face = 0; face < 20; face++)
            {
                // centroid for depth 0
                IcosphereMapping.GetTileBarycentricCenter(0, 0, 0, out float u, out float v);
                Vector3 dir = IcosphereMapping.BarycentricToWorldDirection(face, u, v).normalized;
                IcosphereMapping.WorldDirectionToTileFaceIndex(dir, out int foundFace);
                Assert.AreEqual(face, foundFace, $"Face centroid round-trip failed for face {face} (found {foundFace})");
            }
        }

        [Test]
        public void WorldDirectionToTileFaceIndex_RoundTrips_ForInteriorTiles_Depth2()
        {
            int depth = 2; // some subdivision
            int tilesPerEdge = 1 << depth;

            for (int face = 0; face < 20; face++)
            {
                for (int x = 0; x < tilesPerEdge; x++)
                {
                    for (int y = 0; y < tilesPerEdge; y++)
                    {
                        if (!IcosphereMapping.IsValidTileIndex(x, y, depth)) continue;

                        // compute barycentric center for this tile
                        IcosphereMapping.GetTileBarycentricCenter(x, y, depth, out float uCenter, out float vCenter);
                        // skip tiles too close to edge to avoid ambiguous face selection
                        if (uCenter + vCenter > 0.9f) continue;

                        Vector3 dir = IcosphereMapping.BarycentricToWorldDirection(face, uCenter, vCenter).normalized;
                        IcosphereMapping.WorldDirectionToTileFaceIndex(dir, out int foundFace);
                        Assert.AreEqual(face, foundFace, $"Depth {depth} tile center round-trip failed for face {face} x={x} y={y} (found {foundFace})");
                    }
                }
            }
        }

        [Test]
        public void TileVertexToBarycentricCoordinates_MapsVertices_ToSameFace_ForInteriorVertices()
        {
            int depth = 2;
            int resolution = 8;
            int tilesPerEdge = 1 << depth;

            for (int face = 0; face < 20; face++)
            {
                for (int x = 0; x < tilesPerEdge; x++)
                {
                    for (int y = 0; y < tilesPerEdge; y++)
                    {
                        if (!IcosphereMapping.IsValidTileIndex(x, y, depth)) continue;

                        IcosphereMapping.GetTileBarycentricCenter(x, y, depth, out float uCenter, out float vCenter);
                        if (uCenter + vCenter > 0.9f) continue; // avoid edge tiles

                        var tileId = new TileId(face, x, y, depth);

                        // test a few interior vertex indices (avoid outermost vertices to stay inside)
                        int[] testIs = { resolution/4, resolution/2, (3*resolution)/4 };
                        int[] testJs = { resolution/4, resolution/2, (3*resolution)/4 };

                        foreach (int i in testIs)
                        foreach (int j in testJs)
                        {
                            IcosphereMapping.TileVertexToBarycentricCoordinates(tileId, i, j, resolution, out float u, out float v);
                            // sanity: barycentric coords should be inside triangle
                            Assert.LessOrEqual(u + v, 1.0f + 1e-6f, $"Vertex barycentric outside triangle for face {face} x={x} y={y} i={i} j={j}");

                            Vector3 dir = IcosphereMapping.BarycentricToWorldDirection(face, u, v).normalized;
                            IcosphereMapping.WorldDirectionToTileFaceIndex(dir, out int foundFace);
                            Assert.AreEqual(face, foundFace, $"Vertex mapped to wrong face for face {face} x={x} y={y} i={i} j={j} (found {foundFace})");
                        }
                    }
                }
            }
        }
    }
}
