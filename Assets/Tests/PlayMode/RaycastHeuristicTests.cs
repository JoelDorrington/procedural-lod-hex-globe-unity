#define PLAYMODE_TESTS

using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using UnityEngine.TestTools;
using HexGlobeProject.TerrainSystem;
using System.Collections;
using HexGlobeProject.HexMap;

namespace HexGlobeProject.Tests.PlayMode
{
    public class RaycastHeuristicTests
    {
        [UnityTest]
        public IEnumerator Heuristic_RunsAtApprox30Hz()
        {
            var builder = new PlaymodeTestSceneBuilder();
            builder.Build();

            var mgr = builder.Manager;
            // Ensure we start playing the coroutine
            mgr.enabled = true;
#if PLAYMODE_TESTS
            // Reset counter
            mgr.heuristicTickCount = 0;
#endif

            float testDuration = 1.0f; // seconds (increase for measurement stability)
            yield return new WaitForSeconds(testDuration);

#if PLAYMODE_TESTS
            int ticks = mgr.heuristicTickCount;
            float freq = ticks / testDuration;

            // Expect roughly 30Hz within generous tolerance (±30%)
            Assert.Greater(freq, 21f, "Heuristic frequency too low");
            Assert.Less(freq, 39f, "Heuristic frequency too high");

#endif
            builder.Teardown();
        }

        [Test]
        public void PlanetTerrainTile_OnlyBuildsOnce()
        {
            // Arrange: create TerrainConfig and TerrainRoot
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 30f;
            config.seaLevel = 0f;
            config.heightProvider = new SimplePerlinHeightProvider();

            var terrainGO = new GameObject("TerrainRootTest");
            var terrainRoot = terrainGO.AddComponent<TerrainRoot>();
            terrainRoot.config = config;

            // Create a PlanetTerrainTile
            var tileGO = new GameObject("PlanetTerrainTileTest");
            var tile = tileGO.AddComponent<PlanetTerrainTile>();

            // Act: call EnsureMeshBuilt multiple times
            var meshBeforeBuild = tile.GetComponent<MeshFilter>()?.sharedMesh;
            Assert.IsNull(meshBeforeBuild, "Mesh must be not be built before first call");

            tile.BuildTerrain();
            var meshAfterBuild = tile.GetComponent<MeshFilter>()?.sharedMesh;
            Assert.IsNotNull(meshAfterBuild, "Mesh must be built after first call");

            tile.BuildTerrain();
            var meshAfterSecondBuild = tile.GetComponent<MeshFilter>()?.sharedMesh;
            Assert.AreSame(meshAfterBuild, meshAfterSecondBuild, "Mesh instance should be the same after second call");
        }

        [UnityTest]
        public IEnumerator PlanetTerrainTile_DeactivatesSelfAfterNoHits_Debounced()
        {
            // Arrange: create TerrainConfig and TerrainRoot
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 30f;
            config.seaLevel = 0f;
            config.heightProvider = new SimplePerlinHeightProvider();

            var terrainGO = new GameObject("TerrainRootTest");
            var terrainRoot = terrainGO.AddComponent<TerrainRoot>();
            terrainRoot.config = config;

            // Create a PlanetTerrainTile
            var tileGO = new GameObject("PlanetTerrainTileTest");
            var tile = tileGO.AddComponent<PlanetTerrainTile>();

            // Arrange: set a short debounce before building so BuildTerrain's internal
            // RestartAutoDeactivate uses the desired test value (avoids a short race).
            tile.deactivationDebounceSeconds = 0.5f; // shorten debounce for test speed

            // Ensure tile builds its mesh/collider so lifecycle code can observe hits
            tile.BuildTerrain();

            float startDebounce = Time.realtimeSinceStartup;
            Debug.Log($"{startDebounce} Tile deactivationDebounceSeconds set to {tile.deactivationDebounceSeconds}");
            yield return null; // wait a frame for any coroutines to start

            // Act: wait (poll) until the mesh renderer is disabled or a small timeout elapses.
            // Polling with yield return null ensures pending coroutines get a chance to run
            // on the next Editor frame(s) and avoids brittle reliance on exact timings.
            float deadline = Time.realtimeSinceStartup + tile.deactivationDebounceSeconds + 0.5f; // margin
            while (Time.realtimeSinceStartup < deadline)
            {
                var mr = tile.GetComponent<MeshRenderer>();
                if (mr != null && !mr.enabled) break;
                yield return null; // allow coroutine to run on next frame
            }

            Debug.Log($"{Time.realtimeSinceStartup} Asserts (waited: {Time.realtimeSinceStartup - startDebounce})");
            // Assert: tile visuals should be hidden within the timeout but GameObject and collider remain active
            Assert.IsTrue(tile.gameObject.activeSelf, $"Tile GameObject should remain active so colliders are raycastable");
            Assert.IsFalse(tile.GetComponent<MeshRenderer>() != null && tile.GetComponent<MeshRenderer>().enabled, $"Tile visuals should be hidden after {tile.deactivationDebounceSeconds} seconds of no hits");

        }
        
        [UnityTest]
        public IEnumerator RaycastHits_ShouldReactivateTile_AfterSelfDeactivation()
        {

            // Arrange: create manager and planet root
            var mgrGO = new GameObject("PTVM_Manager_RaycastTest");
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();
            var planet = mgrGO.AddComponent<Planet>();

            mgr.config = ScriptableObject.CreateInstance<TerrainConfig>();
            mgr.config.baseRadius = 30f;
            mgr.config.seaLevel = 0f;
            mgr.config.heightProvider = new SimplePerlinHeightProvider();

            // Create a camera to enable raycasting from
            var cameraGO = new GameObject("Camera");
            // Ensure a Camera component exists so the visibility manager's raycasts have a valid Camera
            cameraGO.AddComponent<Camera>();
            var cameraController = cameraGO.AddComponent<CameraController>();
            cameraController.target = planet.transform;
            cameraController.distance = 60f;
            cameraController.minDistance = 10f;
            cameraController.maxDistance = 100f;
            cameraController.enabled = true;

            cameraController.transform.position = new Vector3(0, 0, -60f);
            cameraController.transform.LookAt(planet.transform);

            mgr.GameCamera = cameraController;

            // Wait a couple frames
            yield return null;
            yield return null;

            var activeTiles = mgr.GetActiveTiles();
            Assert.IsTrue(activeTiles.Count > 0, "No active tiles spawned");

            // Enable debug logging to see what's happening
            foreach (var t in activeTiles)
            {
                t.debug = true;
            }

            foreach (var t in activeTiles)
            {
                // Shorten debounce for test
                t.deactivationDebounceSeconds = 0.5f;
                t.RefreshActivity();
            }
            yield return null; // startcoroutine

            Debug.Log($"Waiting 1 second for debounce test. Raycast heuristic should keep tiles visible.");
            yield return new WaitForSecondsRealtime(1f); // Test debounce

            bool anyVisible = false;
            foreach (var tile in activeTiles)
            {
                Debug.Log($"Tile {tile.name}: gameObject.activeInHierarchy={tile.gameObject.activeInHierarchy}, isVisible={tile.isVisible}, meshRenderer.enabled={tile.meshRenderer?.enabled}");
                Assert.IsTrue(tile.gameObject.activeInHierarchy, "Tile must be in scene");
                anyVisible |= tile.isVisible && tile.meshRenderer != null && tile.meshRenderer.enabled;
            }
            Debug.Log($"Any visible tiles: {anyVisible}");
            Assert.IsTrue(anyVisible, "Tiles must be kept visible while raycasts are running");


            // Stop raycast loop
            mgr.StopRaycastHeuristicLoop();
            yield return null; // stop coroutine

            yield return new WaitForSecondsRealtime(1f); // Wait for deactivation
            foreach (var t in activeTiles)
            {  // Assert all tiles hidden
                Assert.IsTrue(t.gameObject.activeInHierarchy, "Tile must remain in scene after deactivation");
                Assert.IsTrue(!t.isVisible, "Tile isVisible must be false after deactivation");
                Assert.IsTrue(t.meshRenderer != null && !t.meshRenderer.enabled, "Tile meshRenderer must be disabled after deactivation");
            }

            mgr.StartRaycastHeuristicLoop();
            yield return null; // startcoroutine
            yield return new WaitForSecondsRealtime(0.5f); // Let raycast loop run a few times

            // Expectation: If raycasts were wired to refresh the tile, it should remain active.
            // Current implementation does not tie raycasts to RestartAutoDeactivate, so this will fail
            // and thus expose the cause: raycast hits are not resetting the tile's auto-deactivate timer.
            anyVisible = false;
            foreach (var tile in activeTiles)
            {
                Assert.IsTrue(tile.gameObject.activeInHierarchy, "Tile was not reactivated after being hidden");
                anyVisible |= tile.isVisible && tile.meshRenderer != null && tile.meshRenderer.enabled;
            }

            Assert.IsTrue(anyVisible, "No tiles were reactivated after being hidden");

            Object.DestroyImmediate(mgrGO);
        }
    }
}
