using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Simple editor tests that verify mesh vertex density increases exponentially with
    /// resolution and that the height provider remains topology-consistent across
    /// resolutions (sampling must be resolution-agnostic).
    /// </summary>
    public class PlanetTileMeshResolutionTests
    {
        [Test]
        public void VertexCount_ScalesQuadraticallyWithResolution()
        {
            var tileId = new TileId(0, 0, 0, 1);

            int lowRes = 8;
            int highRes = 32;

            // Create deterministic height provider
            var heightProvider = new SimplePerlinHeightProvider
            {
                baseFrequency = 1.0f,
                octaves = 3,
                lacunarity = 2.0f,
                gain = 0.5f,
                amplitude = 1.0f,
                seed = 42
            };

            var lowConfig = ScriptableObject.CreateInstance<TerrainConfig>();
            lowConfig.baseRadius = 100f;
            lowConfig.heightScale = 10f;
            lowConfig.baseResolution = lowRes;

            var highConfig = ScriptableObject.CreateInstance<TerrainConfig>();
            highConfig.baseRadius = 100f;
            highConfig.heightScale = 10f;
            highConfig.baseResolution = highRes;

            var lowBuilder = new PlanetTileMeshBuilder(lowConfig, heightProvider);
            var highBuilder = new PlanetTileMeshBuilder(highConfig, heightProvider);

            // Ensure precomputed entries exist (visibility manager required by builder)
            var managerObj = new GameObject("PrecomputeManager");

            try
            {
                var lowData = new TileData { id = tileId, resolution = lowRes };
                var highData = new TileData { id = tileId, resolution = highRes };

                float rawMinL = float.MaxValue, rawMaxL = float.MinValue;
                float rawMinH = float.MaxValue, rawMaxH = float.MinValue;

                // Build both meshes so we can inspect vertex counts
                lowBuilder.BuildTileMesh(lowData, ref rawMinL, ref rawMaxL);
                highBuilder.BuildTileMesh(highData, ref rawMinH, ref rawMaxH);

                Assert.IsNotNull(lowData.mesh, "Low-res mesh should be generated");
                Assert.IsNotNull(highData.mesh, "High-res mesh should be generated");

                int expectedLow = lowRes * (lowRes + 1) / 2;
                int expectedHigh = highRes * (highRes + 1) / 2;

                Assert.AreEqual(expectedLow, lowData.mesh.vertexCount, $"Low-res vertex count should equal {expectedLow}");
                Assert.AreEqual(expectedHigh, highData.mesh.vertexCount, $"High-res vertex count should equal {expectedHigh}");

                float expectedRatio = (float)expectedHigh / expectedLow;
                float actualRatio = (float)highData.mesh.vertexCount / Mathf.Max(1, lowData.mesh.vertexCount);
                Assert.AreEqual(expectedRatio, actualRatio, expectedRatio * 0.02f, "Vertex count ratio should be approximately quadratic in resolution (triangular lattice)");
            }
            finally
            {
                Object.DestroyImmediate(lowConfig);
                Object.DestroyImmediate(highConfig);
                Object.DestroyImmediate(managerObj);
            }
        }

        [Test]
        public void HeightProvider_IsResolutionAgnostic()
        {
            var tileId = new TileId(0, 0, 0, 1);

            int lowRes = 8;
            int highRes = 32;

            var heightProvider = new SimplePerlinHeightProvider
            {
                baseFrequency = 1.0f,
                octaves = 3,
                lacunarity = 2.0f,
                gain = 0.5f,
                amplitude = 1.0f,
                seed = 42
            };

            // Instead of comparing mesh extrema (which depend on sampling density),
            // verify that the height provider returns the same value for the same
            // world directions regardless of the 'resolution' parameter.

            // Obtain the canonical barycentric center used by the mesh builder
            IcosphereMapping.GetTileBarycentricCenter(tileId.x, tileId.y, tileId.depth, out float centerU, out float centerV);

            var samplePoints = new List<(float u, float v)>
            {
                (centerU, centerV),
                (0.25f, 0.25f),
                (0.5f, 0.1f),
                (0.1f, 0.7f),
            };

            float sampleTolerance = 1e-6f; // very small tolerance since provider shouldn't depend on resolution
            foreach (var (u, v) in samplePoints)
            {
                Vector3 dir = IcosphereMapping.BarycentricToWorldDirection(tileId.face, u, v).normalized;
                float valLow = heightProvider.Sample(in dir, lowRes);
                float valHigh = heightProvider.Sample(in dir, highRes);
                Assert.AreEqual(valLow, valHigh, sampleTolerance, $"Height provider must return same value for identical world position (u={u},v={v})");
            }
        }
    }
}
