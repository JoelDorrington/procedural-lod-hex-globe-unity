using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.Tests.PlayMode;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Unit test that exposes missing-spawn behavior after a depth transition when a Camera is assigned.
    /// The test calls SetDepth then drives the lightweight visibility update to simulate the Update loop
    /// and asserts that at least one tile was spawned for the new depth.
    /// </summary>
    public class DepthTransitionSpawnTests
    {
    private PlaymodeTestSceneBuilder sceneBuilder;
    private PlanetTileVisibilityManager visibilityManager;

        [SetUp]
        public void SetUp()
        {
            sceneBuilder = new PlaymodeTestSceneBuilder();
            sceneBuilder.Build();
            visibilityManager = sceneBuilder.Manager;
        }

        [TearDown]
        public void TearDown()
        {
            if (sceneBuilder != null)
            {
                sceneBuilder.Teardown();
                sceneBuilder = null;
            }
        }

        [Test]
        public void SetDepth_WithCamera_ShouldSpawnTiles_AfterVisibilityUpdate()
        {
            // Arrange - choose a non-zero depth to exercise depth transition logic
            int initialDepth = 0;
            int newDepth = 1;

            // Ensure registry exists for the initial depth
            visibilityManager.SetDepth(initialDepth);

            // Act - transition to new depth
            visibilityManager.SetDepth(newDepth);

            // Simulate a single visibility update that would normally run in Update()
            // This should cause the manager to compute candidates and spawn tiles synchronously
            var updateMethod = typeof(PlanetTileVisibilityManager).GetMethod("UpdateVisibilityMathBased", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(updateMethod, "Could not find UpdateVisibilityMathBased method on PlanetTileVisibilityManager");
            updateMethod.Invoke(visibilityManager, null);

            // Assert - at least one active tile for the new depth should exist
            var active = visibilityManager.GetActiveTiles();
            Assert.IsNotNull(active, "GetActiveTiles should not return null");
            Assert.IsTrue(active.Count > 0, "After changing depth and running the visibility update, at least one tile should be active for the new depth");
        }
    }
}
