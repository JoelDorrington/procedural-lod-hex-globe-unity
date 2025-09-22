using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class TileIndexReflectionIntegrityTests
    {
        [Test]
        public void TileIndexToBaryOrigin_UniquePerXY_NoReflectionCollisions()
        {
            int depth = 1;
            int tilesPerEdge = 1 << depth;
            var seen = new Dictionary<string, (int x, int y)>();

            for (int x = 0; x < tilesPerEdge; x++)
            {
                for (int y = 0; y < tilesPerEdge; y++)
                {
                    var origin = IcosphereMapping.TileIndexToBaryOrigin(depth, x, y);
                    var key = $"{origin.U:F6}-{origin.V:F6}";
                    if (seen.ContainsKey(key))
                    {
                        Assert.Fail($"Collision: ({x},{y}) maps to same origin as ({seen[key].x},{seen[key].y}) -> {key}");
                    }
                    seen[key] = (x, y);
                }
            }
        }

        [Test]
        public void BaryLocalToGlobal_DistinctForAllLocalIndices()
        {
            int depth = 1;
            int res = 4;
            int tilesPerEdge = 1 << depth;
            var seen = new HashSet<string>();
            float tileSpan = 1f / Mathf.Pow(3f, depth);
            const float eps = 1e-5f;
            for (int face = 0; face < 20; face++)
            {
                for (int x = 0; x < tilesPerEdge; x++)
                {
                    for (int y = 0; y < tilesPerEdge; y++)
                    {
                        var id = new TileId(face, x, y, depth);
                        foreach (var local in IcosphereMapping.TileVertexBarys(res))
                        {
                            var global = IcosphereMapping.BaryLocalToGlobal(id, local, res);
                            var key = $"F{face}:{global.U:F6}-{global.V:F6}";
                            // Allow duplicates only when the sampled global bary lies on a tile boundary
                            // (shared vertex between tiles). Check if any bary component is an integer
                            // multiple of tileSpan (within eps). If so this is an expected shared vertex.
                            bool onBoundary = false;
                            float w = 1f - (global.U + global.V);
                            // Check U, V, or W multiples of tileSpan
                            float uMul = global.U / tileSpan;
                            float vMul = global.V / tileSpan;
                            float wMul = w / tileSpan;
                            if (Mathf.Abs(uMul - Mathf.Round(uMul)) < eps) onBoundary = true;
                            if (Mathf.Abs(vMul - Mathf.Round(vMul)) < eps) onBoundary = true;
                            if (Mathf.Abs(wMul - Mathf.Round(wMul)) < eps) onBoundary = true;
                            if (!onBoundary)
                            {
                                Assert.IsFalse(seen.Contains(key), $"Duplicate global bary {key} for tile F{face}({x},{y}) local {local} (interior duplicate)");
                            }
                            seen.Add(key);
                        }
                    }
                }
            }
        }
    }
}
