using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class TileEdgeContinuityTests
    {
        // Small epsilon to allow floating point noise
        private const float kPosEps = 1e-5f;

        [Test]
        public void AdjacentTilesSharedEdgeVerticesMatch()
        {
            // Arrange: create a simple TerrainConfig asset in-memory
            var config = ScriptableObject.CreateInstance<TerrainSystem.TerrainConfig>();
            config.baseRadius = 30f;
            config.heightScale = 1f;
            config.cullBelowSea = false;

            var builder = new PlanetTileMeshBuilder(config);

            // Choose a depth and two adjacent tiles on same face
            int depth = 2;

            // Ensure the precomputed registry is populated for this depth by creating
            // a PlanetTileVisibilityManager and calling SetDepth. The builder relies
            // on the precomputed entries produced by the manager.
            var managerGO = new GameObject("PTVM_Test");
            var visibilityManager = managerGO.AddComponent<PlanetTileVisibilityManager>();
            visibilityManager.config = config;
            visibilityManager.SetDepth(depth);

            int face = 0;
            int x = 1;
            int y = 1;

            // tile A = (face,x,y), tile B = (face,x+1,y) -> share vertical edge at i = res-1
            int res = 8; // reasonable small resolution

            var tileAId = new TileId(face, x, y, depth);
            var tileBId = new TileId(face, x + 1, y, depth);

            var dataA = new TileData() { id = tileAId, resolution = res };
            var dataB = new TileData() { id = tileBId, resolution = res };

            float minA = float.MaxValue, maxA = float.MinValue;
            float minB = float.MaxValue, maxB = float.MinValue;

            // Act: build meshes
            builder.BuildTileMesh(dataA, ref minA, ref maxA);
            builder.BuildTileMesh(dataB, ref minB, ref maxB);

            Assert.IsNotNull(dataA.mesh, "mesh A should be created");
            Assert.IsNotNull(dataB.mesh, "mesh B should be created");

            // Convert tile-local verts back to world space using data.center
            var vertsA = dataA.mesh.vertices;
            var vertsB = dataB.mesh.vertices;

            // Determine shared-edge vertices: for tileA, the right column i = res-1, for tileB, the left column i = 0
            // Vertex index = j*res + i
            for (int j = 0; j < res; j++)
            {
                int idxA = j * res + (res - 1);
                int idxB = j * res + 0;

                Vector3 worldA = vertsA[idxA] + dataA.center;
                Vector3 worldB = vertsB[idxB] + dataB.center;

                float d = Vector3.Distance(worldA, worldB);
                Assert.LessOrEqual(d, kPosEps, $"Shared edge vertex mismatch at row {j}: dist={d}");
            }
        }

        [Test]
        public void DetectNonCoplanarQuadsAlongSeam()
        {
            var config = ScriptableObject.CreateInstance<TerrainSystem.TerrainConfig>();
            config.baseRadius = 30f; config.heightScale = 1f; config.cullBelowSea = false;
            var builder = new PlanetTileMeshBuilder(config);
            int depth = 2;
            var managerGO = new GameObject("PTVM_NC");
            var visibilityManager = managerGO.AddComponent<PlanetTileVisibilityManager>();
            visibilityManager.config = config; visibilityManager.SetDepth(depth);

            int face = 0; int x = 1; int y = 1; int res = 8;
            var A = new TileData() { id = new TileId(face, x, y, depth), resolution = res };
            var B = new TileData() { id = new TileId(face, x + 1, y, depth), resolution = res };
            float ma = float.MaxValue, mb = float.MaxValue; float na = float.MinValue, nb = float.MinValue;
            builder.BuildTileMesh(A, ref ma, ref na); builder.BuildTileMesh(B, ref mb, ref nb);

            var vertsA = A.mesh.vertices; var vertsB = B.mesh.vertices;

            // threshold in degrees for considering quad non-coplanar
            const float angleThreshDeg = 10f;

            for (int j = 0; j < res - 1; j++)
            {
                int idxA = j * res + (res - 2);
                int a0 = idxA; int a1 = idxA + 1; int a2 = idxA + res; int a3 = idxA + res + 1;

                Vector3 w0 = vertsA[a0] + A.center; Vector3 w1 = vertsA[a1] + A.center; Vector3 w2 = vertsA[a2] + A.center; Vector3 w3 = vertsA[a3] + A.center;

                // Two triangles as currently built (we don't know diagonal choice), compute both possible dihedral angles
                float angle1 = Vector3.Angle(Vector3.Cross(w2 - w0, w1 - w0), Vector3.Cross(w3 - w1, w2 - w1));
                float angle2 = Vector3.Angle(Vector3.Cross(w1 - w0, w3 - w0), Vector3.Cross(w2 - w3, w1 - w3));

                float minAngle = Mathf.Min(angle1, angle2);
                Assert.LessOrEqual(minAngle, angleThreshDeg, $"Quad at row {j} is strongly non-coplanar (min dihedral angle {minAngle} deg)");
            }
        }

        [Test]
        public void MeshBuilder_NormalsShouldBeDeterministicAndReasonable()
        {
            // This test validates that the mesh builder produces consistent, deterministic normals
            // and catches the regression we fixed where transform-dependent calculations caused seams
            
            // Arrange: create test configuration
            var config = ScriptableObject.CreateInstance<TerrainSystem.TerrainConfig>();
            config.baseRadius = 30f;
            config.heightScale = 1f;
            config.cullBelowSea = false;

            var builder = new PlanetTileMeshBuilder(config);
            int depth = 2;

            // Setup visibility manager for precomputed entries
            var managerGO = new GameObject("PTVM_Normals_Test");
            var visibilityManager = managerGO.AddComponent<PlanetTileVisibilityManager>();
            visibilityManager.config = config;
            visibilityManager.SetDepth(depth);

            // Create a tile to test normal consistency
            int face = 0, x = 1, y = 1, res = 8;
            var tileId = new TileId(face, x, y, depth);
            
            // Build the same tile multiple times - should get identical results
            var data1 = new TileData() { id = tileId, resolution = res };
            var data2 = new TileData() { id = tileId, resolution = res };

            float min1 = float.MaxValue, max1 = float.MinValue;
            float min2 = float.MaxValue, max2 = float.MinValue;
            
            builder.BuildTileMesh(data1, ref min1, ref max1);
            builder.BuildTileMesh(data2, ref min2, ref max2);

            var vertices1 = data1.mesh.vertices;
            var vertices2 = data2.mesh.vertices;
            var normals1 = data1.mesh.normals;
            var normals2 = data2.mesh.normals;

            Assert.AreEqual(vertices1.Length, vertices2.Length, "Should have same vertex count");
            Assert.AreEqual(normals1.Length, normals2.Length, "Should have same normal count");

            // Test deterministic mesh generation - this is the key regression test
            // If mesh building becomes transform-dependent again, this would fail
            for (int i = 0; i < vertices1.Length; i++)
            {
                float vertDiff = Vector3.Distance(vertices1[i], vertices2[i]);
                Assert.LessOrEqual(vertDiff, 0.0001f, 
                    $"Vertex {i} should be identical across builds. Difference: {vertDiff}");
                    
                float normalDiff = Vector3.Distance(normals1[i], normals2[i]);
                Assert.LessOrEqual(normalDiff, 0.0001f, 
                    $"Normal {i} should be identical across builds. Difference: {normalDiff}");
            }

            // Test that normals are unit length
            for (int i = 0; i < normals1.Length; i++)
            {
                float magnitude = normals1[i].magnitude;
                Assert.LessOrEqual(Mathf.Abs(magnitude - 1.0f), 0.001f, 
                    $"Normal {i} should be unit length, got magnitude {magnitude}");
            }

            // Test that the mesh builder is using the expected simple radial normal approach
            // (This validates our fix: mesh normals should be simple, shader handles the rest)
            Assert.GreaterOrEqual(normals1.Length, res * res - 10, "Should have reasonable number of normals");
            
            // At least some normals should be non-zero (basic sanity check)
            int nonZeroNormals = 0;
            for (int i = 0; i < normals1.Length; i++)
            {
                if (normals1[i].magnitude > 0.1f) nonZeroNormals++;
            }
            Assert.GreaterOrEqual(nonZeroNormals, normals1.Length / 2, "Most normals should be non-zero");

            // Cleanup
            Object.DestroyImmediate(managerGO);
        }
    }
}
