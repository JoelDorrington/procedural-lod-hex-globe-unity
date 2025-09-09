using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Unit tests for subdivided collision mesh generation.
    /// </summary>
    public class SubdividedCollisionMeshTests
    {
        [Test]
        public void SubdividedCollisionMesh_ShouldHaveMoreVerticesThanBasicTriangle()
        {
            // Arrange: create a TileId and PrecomputedTileEntry
            var tileId = new TileId(0, 0, 0, 0);
            var entry = new PlanetTileVisibilityManager.PrecomputedTileEntry
            {
                face = 0,
                x = 0,
                y = 0,
                normal = Vector3.forward,
                centerWorld = Vector3.forward * 50f,
                cornerWorldPositions = new Vector3[]
                {
                    Vector3.forward * 50f + Vector3.right * 10f,
                    Vector3.forward * 50f + Vector3.up * 10f,
                    Vector3.forward * 50f + Vector3.left * 10f
                }
            };

            // Create a test manager to access the method
            var testGO = new GameObject("TestManager");
            var mgr = testGO.AddComponent<PlanetTileVisibilityManager>();
            mgr.config = ScriptableObject.CreateInstance<TerrainConfig>();
            mgr.config.baseRadius = 50f;

            // Use reflection to call the private method
            var method = typeof(PlanetTileVisibilityManager).GetMethod("CreateSubdividedSphereColliderMesh", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Act: create subdivided collision mesh
            var mesh = (Mesh)method.Invoke(mgr, new object[] { tileId, entry, 50f });

            // Assert: should have significantly more vertices than the original 3-vertex triangle
            Assert.IsNotNull(mesh, "Subdivided collision mesh should not be null");
            Assert.Greater(mesh.vertices.Length, 12, "Subdivided mesh should have more than 12 vertices");
            Assert.Greater(mesh.triangles.Length, 36, "Subdivided mesh should have more than 12 triangles (36 indices)");

            // Assert: vertices should be projected to proper distance from tile center
            // Since collision mesh vertices are in local space relative to tile transform,
            // we need to check their distance from the local origin (tile center)
            float tolerance = 10f; // Allow reasonable local space coordinates
            int verticesWithinBounds = 0;

            foreach (var vertex in mesh.vertices)
            {
                float localDistance = vertex.magnitude;
                // Local space vertices should be reasonable offsets (not at sphere radius)
                if (localDistance <= tolerance)
                {
                    verticesWithinBounds++;
                }
            }

            float withinBoundsPercentage = (float)verticesWithinBounds / mesh.vertices.Length;
            Assert.Greater(withinBoundsPercentage, 0.8f, 
                $"At least 80% of collision mesh vertices should be within reasonable local bounds (≤{tolerance}), " +
                $"but only {withinBoundsPercentage:P1} were within bounds");

            // Additional check: vertices should not all be at the same position
            var uniquePositions = new System.Collections.Generic.HashSet<Vector3>();
            // For this unit test the mesh vertices are in local space around the tile center
            // Transform them to world space using the tile center to have consistent checks
            Vector3 tileWorldCenter = entry.centerWorld;
            foreach (var vertex in mesh.vertices)
            {
                Vector3 worldVertex = tileWorldCenter + vertex; // local->world
                var rounded = new Vector3(
                    Mathf.Round(worldVertex.x * 100f) / 100f,
                    Mathf.Round(worldVertex.y * 100f) / 100f,
                    Mathf.Round(worldVertex.z * 100f) / 100f
                );
                uniquePositions.Add(rounded);
            }

            float uniquenessRatio = (float)uniquePositions.Count / mesh.vertices.Length;

            // Debug analysis of vertex distribution
            var distances = new System.Collections.Generic.List<float>();
            foreach (var vertex in mesh.vertices)
            {
                Vector3 worldVertex = tileWorldCenter + vertex;
                distances.Add(worldVertex.magnitude);
            }
            distances.Sort();
            float minDist = distances[0];
            float maxDist = distances[distances.Count - 1];
            float medianDist = distances[distances.Count / 2];

            Debug.Log($"Subdivided collision mesh: {mesh.vertices.Length} vertices, {mesh.triangles.Length/3} triangles, " +
                     $"{withinBoundsPercentage:P1} within local bounds, {uniquenessRatio:P1} unique positions");
            Debug.Log($"Distance analysis: min={minDist:F3}, median={medianDist:F3}, max={maxDist:F3}, expected={mgr.config.baseRadius:F3}");

            // Subdivision without vertex sharing creates duplicate vertices at triangle boundaries
            // For a subdivided triangle mesh, 25-50% uniqueness is reasonable due to shared edges
            Assert.Greater(uniquenessRatio, 0.25f, 
                $"Collision mesh vertices should show reasonable distribution, but only {uniquenessRatio:P1} are unique");

            // Cleanup
            Object.DestroyImmediate(testGO);
        }
    }
}
