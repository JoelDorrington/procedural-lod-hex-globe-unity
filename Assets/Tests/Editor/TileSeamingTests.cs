using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Core;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Focused tests for tile edge seaming issues.
    /// Based on the screenshot showing visible cracks between tiles,
    /// these tests identify and drive fixes for edge matching problems.
    /// </summary>
    public class TileSeamingTests
    {
        private TerrainConfig testConfig;
        private SimplePerlinHeightProvider heightProvider;

        [SetUp]
        public void SetUp()
        {
            testConfig = ScriptableObject.CreateInstance<TerrainConfig>();
            testConfig.baseRadius = 100f;
            testConfig.heightScale = 10f;
            testConfig.baseResolution = 16;

            heightProvider = new SimplePerlinHeightProvider
            {
                baseFrequency = 1.0f,
                octaves = 3,
                lacunarity = 2.0f,
                gain = 0.5f,
                amplitude = 1.0f,
                seed = 42
            };
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
        /// Test the icospheric coordinate mapping for edge consistency.
        /// The screenshot shows seams suggesting coordinate mapping issues.
        /// </summary>
        [Test]
        public void IcosphericMapping_EdgeCoordinates_ShouldBeConsistent()
        {
            // Test that TileVertexToBarycentricCoordinates produces consistent results
            // for vertices that should be at the same world position

            var tile1 = new TileId(0, 0, 0, 1); // face 0, coords (0,0), depth 1
            var tile2 = new TileId(0, 1, 0, 1); // adjacent tile same face

            int resolution = 8;

            // Test edge vertex mapping consistency
            for (int j = 0; j < resolution; j++)
            {
                // Right edge of tile1 (i = resolution-1)
                IcosphereMapping.TileVertexToBarycentricCoordinates(
                    tile1, resolution - 1, j, resolution,
                    out float u1, out float v1);

                // Left edge of tile2 (i = 0) - should map to same world position
                IcosphereMapping.TileVertexToBarycentricCoordinates(
                    tile2, 0, j, resolution,
                    out float u2, out float v2);

                // Convert both to world directions
                Vector3 dir1 = IcosphereMapping.BarycentricToWorldDirection(0, u1, v1);
                Vector3 dir2 = IcosphereMapping.BarycentricToWorldDirection(0, u2, v2);

                // These should be very close (same world position)
                float angleDifference = Vector3.Angle(dir1, dir2);
                Assert.Less(angleDifference, 0.1f, 
                    $"Edge vertices at j={j} should map to same world direction (angle diff: {angleDifference})");

                // Sample heights at these directions - they should be identical
                float height1 = heightProvider.Sample(dir1, resolution);
                float height2 = heightProvider.Sample(dir2, resolution);
                
                Assert.AreEqual(height1, height2, 0.0001f,
                    $"Edge vertices at j={j} should have identical heights");
            }
        }

        /// <summary>
        /// Test that the barycentric coordinate system handles edge cases properly.
        /// Edge vertices near triangle boundaries need special handling.
        /// </summary>
        [Test]
        public void BarycentricCoordinates_TriangleEdges_ShouldBeValid()
        {
            var tileId = new TileId(0, 0, 0, 1);
            int resolution = 8;

            // Test all vertices in the tile
            for (int j = 0; j < resolution; j++)
            {
                for (int i = 0; i < resolution; i++)
                {
                    IcosphereMapping.TileVertexToBarycentricCoordinates(
                        tileId, i, j, resolution,
                        out float u, out float v);

                    // Barycentric coordinates should be valid
                    Assert.GreaterOrEqual(u, 0f, $"u coordinate should be >= 0 at ({i},{j})");
                    Assert.GreaterOrEqual(v, 0f, $"v coordinate should be >= 0 at ({i},{j})");
                    Assert.LessOrEqual(u + v, 1.001f, $"u+v should be <= 1 at ({i},{j}) (got {u+v})"); // Small tolerance for floating point

                    // Convert to world direction and verify it's normalized
                    Vector3 worldDir = IcosphereMapping.BarycentricToWorldDirection(0, u, v);
                    float magnitude = worldDir.magnitude;
                    Assert.AreEqual(1f, magnitude, 0.001f, 
                        $"World direction should be normalized at ({i},{j}) (magnitude: {magnitude})");
                }
            }
        }

        /// <summary>
        /// Test that height sampling is actually deterministic for edge coordinates.
        /// This directly tests the core requirement for seamless tiles.
        /// </summary>
        [Test]
        public void HeightSampling_EdgeDeterminism_ActualTileCoordinates()
        {
            // Use real tile coordinate mapping, not simplified mock
            var tile1 = new TileId(0, 0, 0, 1);
            var tile2 = new TileId(0, 1, 0, 1);
            
            int resolution = 8;
            var edgeHeights1 = new List<float>();
            var edgeHeights2 = new List<float>();
            var edgeDirections1 = new List<Vector3>();
            var edgeDirections2 = new List<Vector3>();

            // Sample edge coordinates using real icospheric mapping
            for (int j = 0; j < resolution; j++)
            {
                // Tile1 right edge
                IcosphereMapping.TileVertexToBarycentricCoordinates(
                    tile1, resolution - 1, j, resolution,
                    out float u1, out float v1);
                Vector3 dir1 = IcosphereMapping.BarycentricToWorldDirection(0, u1, v1);
                float height1 = heightProvider.Sample(dir1, resolution);

                // Tile2 left edge  
                IcosphereMapping.TileVertexToBarycentricCoordinates(
                    tile2, 0, j, resolution,
                    out float u2, out float v2);
                Vector3 dir2 = IcosphereMapping.BarycentricToWorldDirection(0, u2, v2);
                float height2 = heightProvider.Sample(dir2, resolution);

                edgeHeights1.Add(height1);
                edgeHeights2.Add(height2);
                edgeDirections1.Add(dir1);
                edgeDirections2.Add(dir2);
            }

            // Edge heights should match exactly
            for (int i = 0; i < edgeHeights1.Count; i++)
            {
                float heightDiff = Mathf.Abs(edgeHeights1[i] - edgeHeights2[i]);
                Vector3 dirDiff = edgeDirections1[i] - edgeDirections2[i];
                
                // Log details for debugging if heights don't match
                if (heightDiff > 0.0001f)
                {
                    Debug.LogWarning($"Edge mismatch at {i}: heights {edgeHeights1[i]} vs {edgeHeights2[i]}, " +
                                   $"directions {edgeDirections1[i]} vs {edgeDirections2[i]}, " +
                                   $"dir diff magnitude: {dirDiff.magnitude}");
                }

                Assert.AreEqual(edgeHeights1[i], edgeHeights2[i], 0.0001f,
                    $"Edge height at index {i} should match between adjacent tiles");
            }
        }

        /// <summary>
        /// Test that the tile coordinate system correctly handles depth scaling.
        /// Different depth levels should maintain coordinate consistency.
        /// </summary>
        [Test]
        public void TileCoordinates_DepthScaling_ShouldMaintainConsistency()
        {
            // Test a point at different depths
            Vector3 testWorldDirection = new Vector3(0.5f, 0.7f, 0.2f).normalized;

            // Sample this direction at different tile depths/resolutions
            float heightAtDepth0 = heightProvider.Sample(testWorldDirection, 8);   // Low detail
            float heightAtDepth1 = heightProvider.Sample(testWorldDirection, 16);  // Medium detail  
            float heightAtDepth2 = heightProvider.Sample(testWorldDirection, 32);  // High detail

            // Heights should be identical regardless of "resolution" parameter
            Assert.AreEqual(heightAtDepth0, heightAtDepth1, 0.0001f,
                "Height should be consistent between depth 0 and 1");
            Assert.AreEqual(heightAtDepth0, heightAtDepth2, 0.0001f,
                "Height should be consistent between depth 0 and 2");
        }

        /// <summary>
        /// Test for the specific issue visible in the screenshot:
        /// Validate that tile edge vertices actually exist and are computed correctly.
        /// </summary>
        [Test]
        public void TileEdgeVertices_ShouldExistAndBeValid()
        {
            var tileId = new TileId(0, 0, 0, 1);
            int resolution = 8;

            // Test all four edges of the tile
            var edgeVertices = new Dictionary<string, List<Vector3>>();
            edgeVertices["top"] = new List<Vector3>();      // j = 0
            edgeVertices["bottom"] = new List<Vector3>();   // j = resolution-1
            edgeVertices["left"] = new List<Vector3>();     // i = 0
            edgeVertices["right"] = new List<Vector3>();    // i = resolution-1

            // Extract edge vertices using real coordinate mapping
            for (int i = 0; i < resolution; i++)
            {
                // Top edge (j = 0)
                IcosphereMapping.TileVertexToBarycentricCoordinates(
                    tileId, i, 0, resolution, out float uTop, out float vTop);
                Vector3 topVertex = IcosphereMapping.BarycentricToWorldDirection(0, uTop, vTop);
                edgeVertices["top"].Add(topVertex);

                // Bottom edge (j = resolution-1)
                IcosphereMapping.TileVertexToBarycentricCoordinates(
                    tileId, i, resolution - 1, resolution, out float uBottom, out float vBottom);
                Vector3 bottomVertex = IcosphereMapping.BarycentricToWorldDirection(0, uBottom, vBottom);
                edgeVertices["bottom"].Add(bottomVertex);
            }

            for (int j = 0; j < resolution; j++)
            {
                // Left edge (i = 0)
                IcosphereMapping.TileVertexToBarycentricCoordinates(
                    tileId, 0, j, resolution, out float uLeft, out float vLeft);
                Vector3 leftVertex = IcosphereMapping.BarycentricToWorldDirection(0, uLeft, vLeft);
                edgeVertices["left"].Add(leftVertex);

                // Right edge (i = resolution-1)
                IcosphereMapping.TileVertexToBarycentricCoordinates(
                    tileId, resolution - 1, j, resolution, out float uRight, out float vRight);
                Vector3 rightVertex = IcosphereMapping.BarycentricToWorldDirection(0, uRight, vRight);
                edgeVertices["right"].Add(rightVertex);
            }

            // Validate edge vertices
            foreach (var edge in edgeVertices)
            {
                string edgeName = edge.Key;
                var vertices = edge.Value;

                Assert.AreEqual(resolution, vertices.Count, 
                    $"{edgeName} edge should have {resolution} vertices");

                // All vertices should be on unit sphere
                foreach (var vertex in vertices)
                {
                    float magnitude = vertex.magnitude;
                    Assert.AreEqual(1f, magnitude, 0.001f,
                        $"{edgeName} edge vertex should be on unit sphere (magnitude: {magnitude})");
                }

                // Vertices should form a reasonable progression (not all the same)
                if (vertices.Count > 1)
                {
                    bool hasVariation = false;
                    for (int i = 1; i < vertices.Count; i++)
                    {
                        float distance = Vector3.Distance(vertices[i - 1], vertices[i]);
                        if (distance > 0.01f) // Some minimum variation
                        {
                            hasVariation = true;
                            break;
                        }
                    }
                    Assert.IsTrue(hasVariation, $"{edgeName} edge should have variation between vertices");
                }
            }

            // Corner vertices should be shared between adjacent edges
            float cornerTolerance = 0.001f;

            // Top-left corner: top[0] should equal left[0]
            Assert.Less(Vector3.Distance(edgeVertices["top"][0], edgeVertices["left"][0]), cornerTolerance,
                "Top-left corner should be shared between top and left edges");

            // Top-right corner: top[resolution-1] should equal right[0]
            Assert.Less(Vector3.Distance(edgeVertices["top"][resolution - 1], edgeVertices["right"][0]), cornerTolerance,
                "Top-right corner should be shared between top and right edges");

            // Bottom-left corner: bottom[0] should equal left[resolution-1]
            Assert.Less(Vector3.Distance(edgeVertices["bottom"][0], edgeVertices["left"][resolution - 1]), cornerTolerance,
                "Bottom-left corner should be shared between bottom and left edges");

            // Bottom-right corner: bottom[resolution-1] should equal right[resolution-1]
            Assert.Less(Vector3.Distance(edgeVertices["bottom"][resolution - 1], edgeVertices["right"][resolution - 1]), cornerTolerance,
                "Bottom-right corner should be shared between bottom and right edges");
        }
    }
}
