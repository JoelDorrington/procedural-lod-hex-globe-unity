using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class TileVertexBarycentricDiagnosticsTests
    {
        [Test]
        public void SweepAdjacentTiles_FindBarycentricMappingMismatches()
        {
            int[] depthsToCheck = { 1, 2 };
            int resolution = 16; // vertex grid per tile
            int failureCount = 0;

            for (int depth = 1; depth <= 2; depth++)
            {
                int tilesPerEdge = 1 << depth;
                for (int face = 0; face < 20; face++)
                {
                    for (int x = 0; x < tilesPerEdge - 1; x++)
                    {
                        int y = 0; // sweep a sample row; if needed expand y
                        var tileA = new TileId(face, x, y, depth);
                        var tileB = new TileId(face, x + 1, y, depth);

                        for (int j = 0; j < resolution; j++)
                        {
                            IcosphereMapping.TileVertexToBarycentricCoordinates(tileA, resolution - 1, j, resolution, out float uA, out float vA);
                            IcosphereMapping.TileVertexToBarycentricCoordinates(tileB, 0, j, resolution, out float uB, out float vB);

                            if (!Mathf.Approximately(uA, uB) || !Mathf.Approximately(vA, vB))
                            {
                                failureCount++;
                                Debug.LogError($"Mismatch depth={depth} face={face} tilesPerEdge={tilesPerEdge} x={x} j={j} -> uA={uA} vA={vA} | uB={uB} vB={vB}");
                                if (failureCount >= 10) Assert.Fail($"Found {failureCount} barycentric mapping mismatches (see logs).");
                            }
                        }
                    }
                }
            }

            Assert.AreEqual(0, failureCount, $"Found {failureCount} barycentric mapping mismatches; check logs for details.");
        }
    }
}
