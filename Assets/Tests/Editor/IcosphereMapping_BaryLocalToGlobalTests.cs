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
            int[] ress = new int[] { 8, 16, 32 };

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
                                new float[2] { 1f/res, 0f },
                                new float[2] { 0f, 1f/res },
                                new float[2] { 1f/res, 1f/res },
                            };
                            for (int s = 0; s < 5; s++)
                            {
                                // sample tile-local indices in [0, res-1]
                                samples.Add(new float[2] { 1f/rng.Next(1, res), 1f/rng.Next(1, res) });
                            }

                            foreach (var local in samples)
                            {
                                var g = IcosphereMapping.BaryLocalToGlobal(id, local, res);
                                var actualU = g.x;
                                var actualV = g.y;
                                
                                int subdivisionsPerTileEdge = Math.Max(1, res - 1);
                                float tileWidthGlobalWeight = 1f / Mathf.Pow(3, depth + 1);
                                float localVertIntervalWeight = 1f / subdivisionsPerTileEdge;

                                float localToGlobalRatio = localVertIntervalWeight / tileWidthGlobalWeight;

                                // shrink local uvs to match face scale
                                float localUToGlobal = local[0] * localToGlobalRatio;
                                float localVToGlobal = local[1] * localToGlobalRatio;

                                // combine tile offset + local offset to get global
                                float uGlobal = g.x * tileWidthGlobalWeight + localUToGlobal;
                                float vGlobal = g.y * tileWidthGlobalWeight + localVToGlobal;

                                float subdivisionsPerFaceEdge = tilesPerFaceEdge * subdivisionsPerTileEdge;
                                float expectedU = id.x * subdivisionsPerTileEdge + uGlobal;
                                float expectedV = id.y * subdivisionsPerTileEdge + vGlobal;
                                if (expectedU + expectedV > 1f)
                                {
                                    expectedU = 1f - expectedV;
                                    expectedV = 1f - expectedU;
                                }

                                Assert.That(actualU, Is.InRange(-EPS, 1f + EPS));
                                Assert.That(actualV, Is.InRange(-EPS, 1f + EPS));
                                Assert.AreEqual(expectedU, actualU, EPS, $"Mismatch U for depth={depth} tile=({tileX},{tileY}) res={res} local=(u: {local[0]}, v: {local[1]})");
                                Assert.AreEqual(expectedV, actualV, EPS, $"Mismatch V for depth={depth} tile=({tileX},{tileY}) res={res} local=(u: {local[0]}, v: {local[1]})");
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
            float uRaw = 1f / subdivisionsPerFaceEdge;
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
