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
            // For each icosphere face, convert Bary centroid to world direction
            // and ensure WorldDirectionToTileFaceIndex returns the original face index.
            for (int face = 0; face < 20; face++)
            {
                Vector3 dir = IcosphereMapping.BaryToWorldDirection(face, new(1f/3f,1f/3f)).normalized;
                IcosphereMapping.WorldDirectionToFaceIndex(dir, out int foundFace);
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
                        if (!IcosphereTestHelpers.IsValidTileIndex(x, y, depth)) continue;
                        // compute Bary center for this tile
                        var center = IcosphereMapping.TileIndexToBaryCenter(depth, x, y);
                        Vector3 dir = IcosphereMapping.BaryToWorldDirection(face, center).normalized;
                        IcosphereMapping.WorldDirectionToFaceIndex(dir, out int foundFace);
                        Assert.AreEqual(face, foundFace, $"Depth {depth} tile center round-trip failed for face {face} x={x} y={y} (found {foundFace})");
                    }
                }
            }
        }
    }
}
