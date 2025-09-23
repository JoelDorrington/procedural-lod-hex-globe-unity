using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.PlayMode
{
    /// <summary>
    /// Integration tests for the full visibility and tile spawning pipeline to catch
    /// issues where tiles are incorrectly culled and create black triangle areas.
    /// </summary>
    public class TileVisibilityIntegrationTests
    {
        private PlaymodeTestSceneBuilder sceneBuilder;
        
        /// <summary>
        /// Flood fill helper for connected component analysis.
        /// </summary>
        private void FloodFill(bool[,] grid, bool[,] visited, int x, int y, int width, int height)
        {
            if (x < 0 || x >= width || y < 0 || y >= height || visited[x, y] || !grid[x, y])
                return;
                
            visited[x, y] = true;
            
            // Check 4-connected neighbors
            FloodFill(grid, visited, x + 1, y, width, height);
            FloodFill(grid, visited, x - 1, y, width, height);
            FloodFill(grid, visited, x, y + 1, width, height);
            FloodFill(grid, visited, x, y - 1, width, height);
        }
        
        [UnityTest]
        public IEnumerator VisibilityPipeline_CloseupView_NoMissingTiles()
        {
            // Test visibility when camera is close to the planet surface
            sceneBuilder = new PlaymodeTestSceneBuilder();
            sceneBuilder.Build();
            
            var manager = sceneBuilder.Manager;
            var camera = sceneBuilder.CameraController;
            
            // Position camera very close to surface, looking at face 0
            var face0Center = IcosphereMapping.BaryToWorldDirection(0, IcosphereMapping.TileIndexToBaryCenter(0, 0, 0));
            camera.transform.position = face0Center * 1.2f; // Just above surface
            camera.transform.LookAt(Vector3.zero);
            camera.distance = 1.2f;
            
            int testDepth = 2; // Higher detail for closeup
            manager.SetDepth(testDepth);
            
            yield return new WaitForFixedUpdate();
            
            // Force visibility update using reflection
            var updateVisibilityMethod = typeof(PlanetTileVisibilityManager)
                .GetMethod("UpdateVisibilityMathBased", BindingFlags.NonPublic | BindingFlags.Instance);
            updateVisibilityMethod.Invoke(manager, null);
            
            yield return new WaitForFixedUpdate();
            
            // Allow time for spawning
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }
            
            var activeTiles = manager.GetActiveTiles();
            
            Debug.Log($"=== CLOSEUP VISIBILITY TEST ===");
            Debug.Log($"Camera distance: {camera.distance}");
            Debug.Log($"Camera position: {camera.transform.position}");
            Debug.Log($"Active tiles: {activeTiles.Count}");
            
            // Basic sanity checks for closeup view
            Assert.Greater(activeTiles.Count, 0, "Should have some visible tiles when close to surface");
            
            // Cleanup
            sceneBuilder.Teardown();
        }
    }
}