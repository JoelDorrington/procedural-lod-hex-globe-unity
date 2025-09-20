using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileVisibilityManagerIntegrationTests
    {
        [Test]
        public void PTVM_DoesNotSpawnAllTilesUpfront_OnDepthTransition()
        {
            // Arrange
            var managerGO = new GameObject("PTVM_TestManager");
            var manager = managerGO.AddComponent<PlanetTileVisibilityManager>();

            // Create a simple planet transform
            var planetGO = new GameObject("Planet");
            planetGO.transform.position = Vector3.zero;
            managerGO.transform.SetParent(null);
            manager.GetType().GetField("planetTransform", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, planetGO.transform);

            // Assign a TerrainConfig
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 1f;
            config.baseResolution = 16;
            manager.config = config;

            // Create a camera + CameraController and assign to manager
            var camGO = new GameObject("TestCamera");
            var cam = camGO.AddComponent<Camera>();
            var camController = camGO.AddComponent<CameraController>();
            // Place the camera at a reasonable distance so the planet is small in view
            camController.distance = 30f;
            camController.minDistance = 5f;
            camController.maxDistance = 80f;
            camController.target = planetGO.transform;
            manager.GameCamera = camController;

            try
            {
                // Act: request a deep depth (e.g., 4) which would generate many tiles
                int targetDepth = 4;
                manager.SetDepth(targetDepth);

                // Invoke the private visibility update method to simulate one visibility pass
                var method = typeof(PlanetTileVisibilityManager).GetMethod("UpdateVisibilityMathBased", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(method, "UpdateVisibilityMathBased method must exist");
                method.Invoke(manager, null);

                // Assert: the manager should not have spawned all tiles for the depth
                int perFace = HexGlobeProject.Tests.Editor.IcosphereTestHelpers.GetValidTileCountForDepth(targetDepth);
                int expectedTotal = 20 * perFace;

                var activeTiles = manager.GetActiveTiles();
                int spawned = activeTiles != null ? activeTiles.Count : 0;

                // It is sufficient that not all tiles are spawned upfront (spawned < total)
                Assert.Less(spawned, expectedTotal, $"Manager spawned {spawned} tiles which should be less than the full set {expectedTotal}");
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(managerGO);
                Object.DestroyImmediate(planetGO);
                Object.DestroyImmediate(camGO);
            }
        }
    }
}
