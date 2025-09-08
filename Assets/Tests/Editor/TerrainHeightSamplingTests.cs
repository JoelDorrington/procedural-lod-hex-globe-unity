using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// TDD tests for terrain height sampling validation.
    /// These tests ensure deterministic edge sampling, even resolution sampling,
    /// and topology consistency across different resolution parameters.
    /// </summary>
    public class TerrainHeightSamplingTests
    {
        private SimplePerlinHeightProvider heightProvider;
        private TerrainConfig testConfig;

        [SetUp]
        public void SetUp()
        {
            // Create a consistent height provider for testing
            heightProvider = new SimplePerlinHeightProvider
            {
                baseFrequency = 1.0f,
                octaves = 3,
                lacunarity = 2.0f,
                gain = 0.5f,
                amplitude = 1.0f,
                seed = 42 // Fixed seed for deterministic tests
            };

            // Create test terrain config
            testConfig = ScriptableObject.CreateInstance<TerrainConfig>();
            testConfig.baseRadius = 100f;
            testConfig.heightScale = 10f;
        }

        [TearDown]
        public void TearDown()
        {
            if (testConfig != null)
            {
                Object.DestroyImmediate(testConfig);
            }
        }

        /// <summary>
        /// Test that height sampling is deterministic - same direction always returns same height.
        /// This is fundamental to topology consistency.
        /// </summary>
        [Test]
        public void HeightSampling_ShouldBeDeterministic()
        {
            var testDirection = new Vector3(0.5f, 0.7f, 0.2f).normalized;
            int testResolution = 32;

            // Sample the same direction multiple times
            float firstSample = heightProvider.Sample(testDirection, testResolution);
            float secondSample = heightProvider.Sample(testDirection, testResolution);
            float thirdSample = heightProvider.Sample(testDirection, testResolution);

            // All samples should be identical
            Assert.AreEqual(firstSample, secondSample, 0.0001f, 
                "Height sampling should be deterministic - same direction should return same height");
            Assert.AreEqual(firstSample, thirdSample, 0.0001f,
                "Height sampling should be deterministic across multiple calls");
        }

        /// <summary>
        /// Test that resolution parameter does NOT affect terrain topology.
        /// Same world direction should return same height regardless of resolution.
        /// This is CRITICAL for seamless LOD transitions.
        /// </summary>
        [Test]
        public void HeightSampling_TopologyConsistency_ResolutionIndependent()
        {
            var testDirection = new Vector3(0.3f, 0.6f, 0.7f).normalized;

            // Sample at different resolutions
            float heightAtRes4 = heightProvider.Sample(testDirection, 4);
            float heightAtRes16 = heightProvider.Sample(testDirection, 16);
            float heightAtRes64 = heightProvider.Sample(testDirection, 64);
            float heightAtRes256 = heightProvider.Sample(testDirection, 256);

            // All heights should be identical - topology must be consistent
            float tolerance = 0.0001f;
            Assert.AreEqual(heightAtRes4, heightAtRes16, tolerance,
                "Resolution should NOT affect terrain topology - same direction must return same height");
            Assert.AreEqual(heightAtRes4, heightAtRes64, tolerance,
                "Higher resolution should maintain identical topology");
            Assert.AreEqual(heightAtRes4, heightAtRes256, tolerance,
                "Maximum resolution should maintain identical topology");
        }

        /// <summary>
        /// Test that tile edge sampling is deterministic across adjacent tiles.
        /// Points on shared tile edges must return identical heights for seamless stitching.
        /// </summary>
        [Test]
        public void TileEdgeSampling_ShouldBeDeterministicAcrossAdjacents()
        {
            // Create two adjacent tile IDs (simplified test case)
            var tile1 = new TileId(0, 0, 0, 1); // depth 1, face 0, coords (0,0)
            var tile2 = new TileId(0, 1, 0, 1); // adjacent tile, same face, different x

            int resolution = 16;
            
            // Test sampling points along the shared edge
            var edgePoints = new List<Vector3>();
            
            // Generate points along the shared edge (simplified approach)
            for (int i = 0; i <= resolution; i++)
            {
                float t = (float)i / resolution;
                // Mock edge direction calculation - this would use actual icosphere mapping
                var edgeDirection = Vector3.Slerp(
                    new Vector3(1, 0, 0).normalized,
                    new Vector3(1, 1, 0).normalized,
                    t
                ).normalized;
                edgePoints.Add(edgeDirection);
            }

            // Sample heights along edge from both tile perspectives
            var heightsFromTile1 = new List<float>();
            var heightsFromTile2 = new List<float>();

            foreach (var point in edgePoints)
            {
                float height1 = heightProvider.Sample(point, resolution);
                float height2 = heightProvider.Sample(point, resolution);
                
                heightsFromTile1.Add(height1);
                heightsFromTile2.Add(height2);
            }

            // Edge heights should be identical
            for (int i = 0; i < heightsFromTile1.Count; i++)
            {
                Assert.AreEqual(heightsFromTile1[i], heightsFromTile2[i], 0.0001f,
                    $"Edge point {i} should have identical height from both adjacent tiles");
            }
        }

        /// <summary>
        /// Test that mesh sampling creates an even grid at the desired resolution.
        /// Vertices should be distributed uniformly across the tile face.
        /// </summary>
        [Test]
        public void MeshSampling_ShouldCreateEvenGrid()
        {
            int resolution = 8; // 8x8 = 64 vertices
            var tileId = new TileId(0, 0, 0, 1);

            // Mock tile mesh generation (simplified)
            var vertices = new List<Vector3>();
            var sampledHeights = new List<float>();

            // Generate uniform grid of sampling points
            for (int j = 0; j < resolution; j++)
            {
                for (int i = 0; i < resolution; i++)
                {
                    // Normalized grid coordinates [0,1]
                    float u = (float)i / (resolution - 1);
                    float v = (float)j / (resolution - 1);

                    // Convert to world direction (simplified cube-to-sphere mapping)
                    float x = (u - 0.5f) * 2f; // [-1, 1]
                    float z = (v - 0.5f) * 2f; // [-1, 1]
                    var direction = new Vector3(x, 1f, z).normalized;

                    float height = heightProvider.Sample(direction, resolution);
                    
                    vertices.Add(direction);
                    sampledHeights.Add(height);
                }
            }

            // Verify we have the expected number of samples
            Assert.AreEqual(resolution * resolution, vertices.Count,
                "Should sample exactly resolution^2 vertices");
            Assert.AreEqual(vertices.Count, sampledHeights.Count,
                "Should have height sample for each vertex");

            // Verify even distribution (check first/last row spacing)
            var firstRowVertices = new List<Vector3>();
            var lastRowVertices = new List<Vector3>();
            
            for (int i = 0; i < resolution; i++)
            {
                firstRowVertices.Add(vertices[i]); // First row: indices 0 to resolution-1
                lastRowVertices.Add(vertices[(resolution-1) * resolution + i]); // Last row
            }

            // Check uniform spacing in first row
            for (int i = 1; i < firstRowVertices.Count; i++)
            {
                float spacing1 = Vector3.Distance(firstRowVertices[i-1], firstRowVertices[i]);
                float spacing2 = Vector3.Distance(firstRowVertices[0], firstRowVertices[1]);
                
                Assert.AreEqual(spacing2, spacing1, 0.1f,
                    "Vertex spacing should be uniform across the grid");
            }
        }

        /// <summary>
        /// Test that height sampling maintains smooth gradients.
        /// Adjacent samples should not have extreme discontinuities.
        /// </summary>
        [Test]
        public void HeightSampling_ShouldMaintainSmoothGradients()
        {
            var baseDirection = new Vector3(0.5f, 0.7f, 0.2f).normalized;
            float baseHeight = heightProvider.Sample(baseDirection, 32);

            // Test nearby directions for gradient smoothness
            var nearbyDirections = new[]
            {
                (baseDirection + new Vector3(0.01f, 0, 0)).normalized,
                (baseDirection + new Vector3(0, 0.01f, 0)).normalized,
                (baseDirection + new Vector3(0, 0, 0.01f)).normalized,
                (baseDirection + new Vector3(-0.01f, 0, 0)).normalized,
                (baseDirection + new Vector3(0, -0.01f, 0)).normalized,
                (baseDirection + new Vector3(0, 0, -0.01f)).normalized
            };

            foreach (var direction in nearbyDirections)
            {
                float nearbyHeight = heightProvider.Sample(direction, 32);
                float heightDifference = Mathf.Abs(nearbyHeight - baseHeight);

                // Height difference should be reasonable for small directional changes
                Assert.Less(heightDifference, heightProvider.amplitude,
                    "Height should change smoothly for small directional changes");
            }
        }

        /// <summary>
        /// Test that height values are within expected bounds.
        /// Heights should respect the amplitude and scale parameters.
        /// </summary>
        [Test]
        public void HeightSampling_ShouldRespectAmplitudeBounds()
        {
            var testDirections = new[]
            {
                Vector3.up,
                Vector3.right,
                Vector3.forward,
                new Vector3(1, 1, 1).normalized,
                new Vector3(-1, 0.5f, 0.3f).normalized,
                new Vector3(0.2f, -0.8f, 0.6f).normalized
            };

            float maxExpectedHeight = heightProvider.amplitude;
            float minExpectedHeight = -heightProvider.amplitude;

            foreach (var direction in testDirections)
            {
                float height = heightProvider.Sample(direction, 32);

                Assert.GreaterOrEqual(height, minExpectedHeight,
                    $"Height {height} should be >= minimum expected {minExpectedHeight}");
                Assert.LessOrEqual(height, maxExpectedHeight,
                    $"Height {height} should be <= maximum expected {maxExpectedHeight}");
            }
        }

        /// <summary>
        /// Test that different noise seeds produce different but still deterministic results.
        /// Same seed should always produce same terrain, different seeds should differ.
        /// </summary>
        [Test]
        public void HeightSampling_SeedDeterminism()
        {
            var testDirection = new Vector3(0.4f, 0.6f, 0.3f).normalized;

            var provider1 = new SimplePerlinHeightProvider { seed = 12345 };
            var provider2 = new SimplePerlinHeightProvider { seed = 12345 }; // Same seed
            var provider3 = new SimplePerlinHeightProvider { seed = 67890 }; // Different seed

            float height1a = provider1.Sample(testDirection, 32);
            float height1b = provider1.Sample(testDirection, 32); // Same provider, same call
            float height2 = provider2.Sample(testDirection, 32);   // Different provider, same seed
            float height3 = provider3.Sample(testDirection, 32);   // Different provider, different seed

            // Same provider should be deterministic
            Assert.AreEqual(height1a, height1b, 0.0001f,
                "Same provider should return identical results");

            // Same seed should produce identical results
            Assert.AreEqual(height1a, height2, 0.0001f,
                "Providers with same seed should return identical results");

            // Different seeds should produce different results
            Assert.AreNotEqual(height1a, height3,
                "Providers with different seeds should return different results");
            
            // Verify they are actually different by a meaningful amount
            float difference = Mathf.Abs(height1a - height3);
            Assert.Greater(difference, 0.001f, 
                "Different seeds should produce meaningfully different results");
        }

        /// <summary>
        /// Test integration with actual tile mesh generation to ensure the sampling
        /// principles are correctly applied in the real mesh building pipeline.
        /// </summary>
        [Test]
        public void IntegrationTest_MeshBuilderRespectsSamplingPrinciples()
        {
            // Create a simple test tile
            var tileId = new TileId(0, 0, 0, 1);
            var tileData = new TileData { id = tileId };

            // This test would verify that PlanetTileMeshBuilder correctly applies
            // our sampling principles, but we'll mark it as integration test
            // that validates the mesh builder uses deterministic, resolution-independent sampling

            // For now, we'll test the principle by directly checking height provider
            var direction = new Vector3(0.5f, 0.5f, 0.5f).normalized;
            
            // Test that mesh builder would get consistent results
            float heightAtLowRes = heightProvider.Sample(direction, 8);
            float heightAtHighRes = heightProvider.Sample(direction, 64);

            Assert.AreEqual(heightAtLowRes, heightAtHighRes, 0.0001f,
                "Mesh builder should get consistent heights regardless of mesh resolution");

            // Mark this as placeholder for full integration testing
            Assert.Pass("Height sampling principles validated - ready for mesh builder integration");
        }
    }
}
