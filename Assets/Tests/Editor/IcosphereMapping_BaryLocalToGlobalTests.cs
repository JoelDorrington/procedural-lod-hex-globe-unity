using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class IcosphereMapping_BaryLocalToGlobalTests
    {
        // Small epsilon for float comparisons; slightly relaxed to account
        // for any floating point arithmetic differences across platforms.
        private const float EPS = 1e-5f;

        [Test]
        public void BaryLocalToGlobal_IntegerSubdivisionMapping_IsConsistentAcrossDepthsAndRes()
        {
            var rng = new System.Random(12345);

            // test a variety of depths and resolutions
            int[] depths = new int[] { 0, 1, 2, 3 };
            int[] ress = new int[] { 2, 3, 4, 6 };

            foreach (var depth in depths)
            {
                int tilesPerFaceEdge = 1 << depth;
                for (int tileX = 0; tileX < Math.Max(1, tilesPerFaceEdge); tileX++)
                {
                    for (int tileY = 0; tileY < Math.Max(1, tilesPerFaceEdge); tileY++)
                    {
                        var id = new TileId(0, tileX, tileY, depth);
                        foreach (var res in ress)
                        {
                            // test corner/subdivision integer coordinates and a few random ones
                            var samples = new List<float[]>() {
                                new float[2] { 0f, 0f },
                                new float[2] { res - 1f, 0f },
                                new float[2] { 0f, res - 1f },
                            };
                            for (int s = 0; s < 5; s++)
                            {
                                // sample tile-local indices in [0, res-1]
                                samples.Add(new float[2] { rng.Next(0, res), rng.Next(0, res) });
                            }

                            foreach (var local in samples)
                            {
                                var g = IcosphereMapping.BaryLocalToGlobal(id, local, res);

                                // Compute expected using the same mapping as implementation:
                                // The mapping now uses subdivisionsPerTileEdge = res - 1 (segments per tile edge)
                                int subdivisionsPerTileEdge = Math.Max(1, res - 1);
                                float subdivisionsPerFaceEdge = tilesPerFaceEdge * subdivisionsPerTileEdge;
                                float expectedU = (id.x * subdivisionsPerTileEdge + local[0]) / subdivisionsPerFaceEdge;
                                float expectedV = (id.y * subdivisionsPerTileEdge + local[1]) / subdivisionsPerFaceEdge;
                                if (expectedU + expectedV > 1f)
                                {
                                    expectedU = 1f - expectedU;
                                    expectedV = 1f - expectedV;
                                }

                                Assert.That(g.x, Is.InRange(-EPS, 1f + EPS));
                                Assert.That(g.y, Is.InRange(-EPS, 1f + EPS));
                                Assert.AreEqual(expectedU, g.x, EPS, $"Mismatch U for depth={depth} tile=({tileX},{tileY}) res={res} local={local}");
                                Assert.AreEqual(expectedV, g.y, EPS, $"Mismatch V for depth={depth} tile=({tileX},{tileY}) res={res} local={local}");
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void BaryLocalToGlobal_Reflection_Behavior_IsStableOnBoundary()
        {
            // Verify behavior exactly at the reflection boundary and just inside/outside it.
            var id = new TileId(0, 0, 0, 1);
            int res = 5;
            int tilesPerFaceEdge = 1 << id.depth;
            int subdivisionsPerTileEdge = Math.Max(1, res - 1);
            float subdivisionsPerFaceEdge = tilesPerFaceEdge * subdivisionsPerTileEdge;

            // Construct a pair (u,v) that sums to exactly 1 (before reflection)
            float uRaw = (2f) / subdivisionsPerFaceEdge;
            float vRaw = 1f - uRaw;
            var local = new float[] { uRaw * subdivisionsPerFaceEdge, vRaw * subdivisionsPerFaceEdge };

            var g = IcosphereMapping.BaryLocalToGlobal(id, local, res);
            // After reflection rule, when u+v == 1 the implementation mirrors only when >1.
            // Expect no reflection here; just ensure g sums to approximately 1.
            Assert.AreEqual(1f, g.x + g.y, 1e-4f, "Boundary sum should be ~1 when not reflected");

            // Now test a value slightly over 1
            var localOver = new float[2] { local[0] + 0.001f, local[1] + 0.001f };
            var gOver = IcosphereMapping.BaryLocalToGlobal(id, localOver, res);
            Assert.Less(gOver.x + gOver.y, 1f + 1e-4f, "Reflected result should bring sum back below or near 1");
        }
    }
}
