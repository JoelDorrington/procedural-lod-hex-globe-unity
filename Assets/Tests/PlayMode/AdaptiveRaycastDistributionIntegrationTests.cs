using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections;
using UnityEngine.TestTools;
using HexGlobeProject.HexMap;
using HexGlobeProject.Tests.PlayMode;

namespace HexGlobeProject.Tests.PlayMode
{
    /// <summary>
    /// Integration tests to verify the adaptive raycast distribution feature works correctly
    /// in the PlanetTileVisibilityManager.
    /// </summary>
    public class AdaptiveRaycastDistributionIntegrationTests
    {
        [UnityTest]
        public IEnumerator AdaptiveDistribution_ShouldConcentrateRaysOnPlanet()
        {
            // Arrange: create manager with adaptive distribution enabled
            var mgrGO = new GameObject("PTVM_AdaptiveTest");
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();
            var planet = mgrGO.AddComponent<Planet>();

            mgr.config = ScriptableObject.CreateInstance<TerrainConfig>();
            mgr.config.baseRadius = 50f;
            mgr.config.seaLevel = 0f;
            mgr.config.heightProvider = new SimplePerlinHeightProvider();

            // Create camera positioned to see the planet
            var cameraGO = new GameObject("Camera");
            cameraGO.AddComponent<Camera>();
            var cameraController = cameraGO.AddComponent<CameraController>();
            cameraController.target = planet.transform;
            cameraController.distance = 150f; // far enough to see the whole planet
            cameraController.minDistance = 60f;
            cameraController.maxDistance = 300f;
            cameraController.enabled = true;

            cameraController.transform.position = new Vector3(0, 0, -150f);
            cameraController.transform.LookAt(planet.transform);

            mgr.GameCamera = cameraController;
            mgr.debugDisableCameraDepthSync = true; // control depth manually
            
            // Act: wait for the adaptive raycast system to run
            yield return null; // Start frame
            yield return null; // Let initialization complete
            
            // Manually set depth to 0 for consistent testing
            mgr.SetDepth(0);
            yield return null;
            
            // Wait for raycast heuristic to run a few times
            yield return new WaitForSecondsRealtime(2f);

            // Assert: should have spawned tiles due to adaptive distribution
            var activeTiles = mgr.GetActiveTiles();
            Assert.Greater(activeTiles.Count, 0, "Adaptive raycast distribution should spawn tiles");
            
            // The adaptive distribution should have concentrated rays on the planet surface
            // and spawned tiles at depth 0 (which should be 20 tiles for icosphere face centers)
            Assert.LessOrEqual(activeTiles.Count, 25, "Should not spawn excessive tiles with adaptive distribution");
            
            Debug.Log($"Adaptive distribution spawned {activeTiles.Count} tiles at depth 0");

            // Cleanup
            Object.DestroyImmediate(mgrGO);
        }
        
        [UnityTest]
        public IEnumerator AdaptiveDistribution_ShouldWorkWithDifferentCameraDistances()
        {
            // Arrange: create manager
            var mgrGO = new GameObject("PTVM_DistanceTest");
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();
            var planet = mgrGO.AddComponent<Planet>();

            mgr.config = ScriptableObject.CreateInstance<TerrainConfig>();
            mgr.config.baseRadius = 50f;
            mgr.config.seaLevel = 0f;
            mgr.config.heightProvider = new SimplePerlinHeightProvider();

            var cameraGO = new GameObject("Camera");
            cameraGO.AddComponent<Camera>();
            var cameraController = cameraGO.AddComponent<CameraController>();
            cameraController.target = planet.transform;
            cameraController.minDistance = 60f;
            cameraController.maxDistance = 300f;
            cameraController.enabled = true;

            mgr.GameCamera = cameraController;
            mgr.debugDisableCameraDepthSync = true;

            // Test close distance
            cameraController.distance = 80f;
            cameraController.transform.position = new Vector3(0, 0, -80f);
            cameraController.transform.LookAt(planet.transform);
            
            yield return null;
            mgr.SetDepth(1); // closer = higher depth
            yield return new WaitForSecondsRealtime(1f);
            
            var closeTiles = mgr.GetActiveTiles();
            
            // Test far distance
            cameraController.distance = 250f;
            cameraController.transform.position = new Vector3(0, 0, -250f);
            cameraController.transform.LookAt(planet.transform);
            
            yield return null;
            mgr.SetDepth(0); // farther = lower depth
            yield return new WaitForSecondsRealtime(1f);
            
            var farTiles = mgr.GetActiveTiles();
            
            // Assert: adaptive distribution should work at both distances
            Assert.Greater(closeTiles.Count, 0, "Should spawn tiles when camera is close");
            Assert.Greater(farTiles.Count, 0, "Should spawn tiles when camera is far");
            
            Debug.Log($"Close distance: {closeTiles.Count} tiles, Far distance: {farTiles.Count} tiles");

            // Cleanup
            Object.DestroyImmediate(mgrGO);
        }
        
        [UnityTest]
        public IEnumerator TileCollisionMesh_ShouldBeSubdividedAndProjectedToSphereRadius()
        {
            // Arrange: minimal setup to test collision mesh generation
            var mgrGO = new GameObject("CollisionTest");
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();
            
            // Set up minimal config
            mgr.config = ScriptableObject.CreateInstance<TerrainConfig>();
            mgr.config.baseRadius = 50f;
            mgr.config.seaLevel = 0f;
            mgr.config.heightProvider = new SimplePerlinHeightProvider();

            // Test collision mesh creation directly without camera/heuristic complexity
            yield return null;
            
            // Create a test tile manually by invoking the collision mesh creation method
            var testTileId = new TileId(0, 0, 0, 0); // Simple depth 0 tile
            
            // Use reflection to access private collision mesh creation method
            var createCollisionMethod = typeof(PlanetTileVisibilityManager).GetMethod(
                "CreateSubdividedSphereColliderMesh", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Create a minimal precomputed entry for testing
            var testEntry = new PlanetTileVisibilityManager.PrecomputedTileEntry();
            testEntry.centerWorld = Vector3.forward * mgr.config.baseRadius; // Point on sphere surface
            testEntry.normal = Vector3.forward;
            testEntry.cornerWorldPositions = null; // Will trigger fallback triangle creation
            
            // Act: create collision mesh directly
            var collisionMesh = (Mesh)createCollisionMethod.Invoke(mgr, new object[] { testTileId, testEntry, mgr.config.baseRadius });
            
            // Assert: collision mesh should be properly subdivided
            Assert.IsNotNull(collisionMesh, "Collision mesh should be created");
            var vertices = collisionMesh.vertices;
            var triangles = collisionMesh.triangles;
            
            Assert.Greater(vertices.Length, 8, "Collision mesh should have more than 8 vertices (subdivided)");
            Assert.Greater(triangles.Length, 24, "Collision mesh should have more than 8 triangles");

            // Assert: vertices should be projected to sphere radius when transformed to world space
            float expectedRadius = mgr.config.baseRadius;
            int verticesAtCorrectRadius = 0;
            float tolerance = 0.1f;
            
            // For this test, tile center is the transform position
            Vector3 tileWorldCenter = testEntry.centerWorld;
            
            foreach (var vertex in vertices)
            {
                // Transform from local space to world space (vertex is relative to tile center)
                Vector3 worldVertex = vertex + tileWorldCenter;
                float distanceFromOrigin = worldVertex.magnitude;
                
                if (Mathf.Abs(distanceFromOrigin - expectedRadius) <= tolerance)
                {
                    verticesAtCorrectRadius++;
                }
            }

            // At least 80% of vertices should be at the correct sphere radius
            float correctRadiusPercentage = (float)verticesAtCorrectRadius / vertices.Length;
            Assert.Greater(correctRadiusPercentage, 0.8f, 
                $"At least 80% of collision mesh vertices should be at sphere radius {expectedRadius}, " +
                $"but only {correctRadiusPercentage:P1} were correct. Found {verticesAtCorrectRadius}/{vertices.Length} vertices at correct radius.");

            // Cleanup
            Object.DestroyImmediate(mgrGO);
        }
        
        [UnityTest]
        public IEnumerator TileCollisionMesh_ShouldProvideReliableRaycastHits()
        {
            // Arrange: create manager and test raycast reliability
            var mgrGO = new GameObject("PTVM_RaycastReliabilityTest");
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();
            var planet = mgrGO.AddComponent<Planet>();

            mgr.config = ScriptableObject.CreateInstance<TerrainConfig>();
            mgr.config.baseRadius = 50f;
            mgr.config.seaLevel = 0f;
            mgr.config.heightProvider = new SimplePerlinHeightProvider();

            var cameraGO = new GameObject("Camera");
            var camera = cameraGO.AddComponent<Camera>();
            var cameraController = cameraGO.AddComponent<CameraController>();
            cameraController.target = planet.transform;
            cameraController.distance = 120f;
            cameraController.minDistance = 60f;
            cameraController.maxDistance = 300f;
            cameraController.enabled = true;

            cameraController.transform.position = new Vector3(0, 0, -120f);
            cameraController.transform.LookAt(planet.transform);

            mgr.GameCamera = cameraController;
            mgr.debugDisableCameraDepthSync = true;
            
            // Act: spawn tiles
            yield return null;
            mgr.SetDepth(0);
            yield return new WaitForSecondsRealtime(1f);

            var activeTiles = mgr.GetActiveTiles();
            Assert.Greater(activeTiles.Count, 0, "Need tiles to test raycast reliability");

            // Test multiple rays from camera toward planet center to verify reliable hits
            int totalRays = 20;
            int successfulHits = 0;
            
            for (int i = 0; i < totalRays; i++)
            {
                // Create slightly offset rays around the planet center to test coverage
                float offsetAngle = (float)i / totalRays * 360f * Mathf.Deg2Rad;
                float offsetRadius = 0.1f; // Small offset in viewport space
                
                Vector3 viewportPos = new Vector3(
                    0.5f + Mathf.Cos(offsetAngle) * offsetRadius,
                    0.5f + Mathf.Sin(offsetAngle) * offsetRadius,
                    0f
                );
                
                Ray ray = camera.ViewportPointToRay(viewportPos);
                
                // Test if any tile's ActivateIfHit method returns true
                bool hitAnyTile = false;
                foreach (var tile in activeTiles)
                {
                    if (tile.ActivateIfHit(ray))
                    {
                        hitAnyTile = true;
                        break;
                    }
                }
                
                if (hitAnyTile)
                {
                    successfulHits++;
                }
            }

            // Assert: should have reliable hit rate with subdivided collision meshes
            float hitRate = (float)successfulHits / totalRays;
            Assert.Greater(hitRate, 0.7f, 
                $"Collision mesh should provide reliable raycast hits. Expected >70% hit rate, " +
                $"but got {hitRate:P1} ({successfulHits}/{totalRays} hits). " +
                "This indicates collision meshes may need better subdivision and sphere projection.");

            Debug.Log($"Raycast reliability: {hitRate:P1} hit rate ({successfulHits}/{totalRays} rays hit tiles)");

            // Cleanup
            Object.DestroyImmediate(mgrGO);
        }
    }
}
