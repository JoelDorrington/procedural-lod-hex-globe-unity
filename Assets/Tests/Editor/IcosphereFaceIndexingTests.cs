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
                // centroid for depth 0
                IcosphereMapping.GetTileBaryCenter(0, 0, 0, out float u, out float v);
                Vector3 dir = IcosphereMapping.BaryToWorldDirection(face, u, v).normalized;
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
                        if (!IcosphereTestHelpers.IsValidTileIndex(x, y, depth)) continue;

                        // compute Bary center for this tile
                        IcosphereMapping.GetTileBaryCenter(depth, x, y, out float uCenter, out float vCenter);
                        // skip tiles too close to edge to avoid ambiguous face selection
                        if (uCenter + vCenter > 0.9f) continue;

                        Vector3 dir = IcosphereMapping.BaryToWorldDirection(face, uCenter, vCenter).normalized;
                        IcosphereMapping.WorldDirectionToTileFaceIndex(dir, out int foundFace);
                        Assert.AreEqual(face, foundFace, $"Depth {depth} tile center round-trip failed for face {face} x={x} y={y} (found {foundFace})");
                    }
                }
            }
        }
    }
}
