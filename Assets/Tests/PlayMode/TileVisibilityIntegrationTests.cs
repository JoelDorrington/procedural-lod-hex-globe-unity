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

        [UnityTest]
        public IEnumerator VisibilityPipeline_FullPlanetScan_NoBlackAreas()
        {
            // Use standardized scene builder for consistent setup
            sceneBuilder = new PlaymodeTestSceneBuilder();
            sceneBuilder.Build();
            
            var manager = sceneBuilder.Manager;
            var camera = sceneBuilder.CameraController;
            
            // Setup camera for close planet view (should see face 0 tiles clearly)
            camera.distance = 2.0f; // Close but not too close
            
            // Position camera to look directly at face 0 (towards +Z)
            camera.transform.position = new Vector3(0f, 0f, 3f);
            camera.transform.LookAt(Vector3.zero);
            
            yield return new WaitForFixedUpdate();
            
            // Enable depth 1 for reasonable tile count
            manager.SetDepth(1);
            yield return new WaitForFixedUpdate();
            
            // Force manual visibility update using reflection
            var visibilityMethod = typeof(PlanetTileVisibilityManager).GetMethod("UpdateVisibilityMathBased", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            visibilityMethod?.Invoke(manager, null);
            
            yield return new WaitForFixedUpdate();
            
            // Allow time for spawning
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }
            
            // Analysis: check what tiles are actually visible
            var activeTiles = manager.GetActiveTiles();
            Debug.Log($"=== VISIBILITY ANALYSIS (Standardized Setup) ===");
            Debug.Log($"Active tiles: {activeTiles.Count}");
            Debug.Log($"Camera position: {camera.transform.position}");
            Debug.Log($"Camera direction: {camera.transform.forward}");
            Debug.Log($"Camera alignment to planet: {Vector3.Dot(camera.transform.forward, Vector3.back):F3}");
            
            // Count tiles by face and analyze coverage
            var faceCount = new int[20];
            var face0Tiles = new List<TileId>();
            
            foreach (var tile in activeTiles)
            {
                var tileId = tile.tileData.id;
                faceCount[tileId.face]++;
                if (tileId.face == 0)
                {
                    face0Tiles.Add(tileId);
                }
            }
            
            Debug.Log($"Face distribution:");
            for (int f = 0; f < 20; f++)
            {
                if (faceCount[f] > 0)
                    Debug.Log($"  Face {f}: {faceCount[f]} tiles");
            }
            
            // Expected tiles for face 0 at depth 1 (4x4 grid = 16 tiles)
            var expectedFace0Tiles = new List<TileId>();
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    expectedFace0Tiles.Add(new TileId(0, x, y, 1));
                }
            }
            
            Debug.Log($"Expected face 0 tiles: {expectedFace0Tiles.Count}");
            Debug.Log($"Actual face 0 tiles: {face0Tiles.Count}");
            
            // Check center coverage for face 0
            float centerCoverage = face0Tiles.Count / (float)expectedFace0Tiles.Count;
            Debug.Log($"Face 0 center coverage: {centerCoverage:P1}");
            
            // Count unexpected tiles from other faces
            int unexpectedTiles = activeTiles.Count - face0Tiles.Count;
            Debug.Log($"Unexpected tiles from other faces: {unexpectedTiles}");
            
            // CRITICAL: Test spatial coherence - detect checkerboard/scattered patterns
            // Create a 4x4 grid to map which face 0 tiles are actually visible
            bool[,] face0Grid = new bool[4, 4];
            var face0TileSet = new HashSet<TileId>(face0Tiles);
            
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    var tileId = new TileId(0, x, y, 1);
                    face0Grid[x, y] = face0TileSet.Contains(tileId);
                }
            }
            
            // Visualize the grid pattern for debugging
            Debug.Log("Face 0 tile visibility grid (X = visible, . = missing):");
            for (int y = 3; y >= 0; y--) // Print top to bottom
            {
                string row = "";
                for (int x = 0; x < 4; x++)
                {
                    row += face0Grid[x, y] ? "X" : ".";
                }
                Debug.Log($"  Y={y}: {row}");
            }
            
            // Test 1: Detect checkerboard pattern (alternating visible/invisible)
            int checkerboardMatches = 0;
            int totalPositions = 0;
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    bool expectedCheckerboard = ((x + y) % 2) == 0;
                    if (face0Grid[x, y] == expectedCheckerboard)
                        checkerboardMatches++;
                    totalPositions++;
                }
            }
            float checkerboardRatio = checkerboardMatches / (float)totalPositions;
            Debug.Log($"Checkerboard pattern match: {checkerboardRatio:P1} ({checkerboardMatches}/{totalPositions})");
            
            // Test 2: Measure spatial connectedness using flood fill
            int connectedComponents = CountConnectedComponents(face0Grid);
            Debug.Log($"Connected components in face 0: {connectedComponents}");
            
            // Test 3: Check if we have a reasonable center cluster
            // The camera is pointing directly at face 0, so center tiles should be prioritized
            int centerTilesVisible = 0;
            for (int x = 1; x <= 2; x++) // Center 2x2 region
            {
                for (int y = 1; y <= 2; y++)
                {
                    if (face0Grid[x, y]) centerTilesVisible++;
                }
            }
            float centerClusterRatio = centerTilesVisible / 4.0f; // 4 center tiles
            Debug.Log($"Center cluster coverage: {centerClusterRatio:P1} ({centerTilesVisible}/4 center tiles)");
            
            // ASSERTIONS: Fail the test if unwanted behavior is detected
            
            // Fail if it looks like a checkerboard (>75% match to checkerboard pattern)
            Assert.Less(checkerboardRatio, 0.75f, 
                $"Tile pattern appears to be a checkerboard ({checkerboardRatio:P1} match) - this indicates broken visibility selection");
            
            // Fail if tiles are too fragmented (more than 2 disconnected regions)
            Assert.LessOrEqual(connectedComponents, 2, 
                $"Face 0 tiles are too fragmented ({connectedComponents} disconnected regions) - should form mostly contiguous areas");
            
            // Fail if the center region (where camera points) is mostly empty
            Assert.GreaterOrEqual(centerClusterRatio, 0.5f,
                $"Poor center region coverage ({centerClusterRatio:P1}) - camera pointing directly at face 0 should prioritize center tiles");
            
            // Basic coverage requirements (kept from original test)
            Assert.Greater(centerCoverage, 0.25f, "Should see at least 25% of face 0 tiles when camera is directly aligned");
            Assert.Less(unexpectedTiles, face0Tiles.Count * 2, "Should not see dramatically more tiles from other faces than face 0");
            
            // Cleanup
            sceneBuilder.Teardown();
        }
        
        /// <summary>
        /// Count connected components in a 2D boolean grid using flood fill.
        /// Returns the number of separate connected regions of 'true' values.
        /// </summary>
        private int CountConnectedComponents(bool[,] grid)
        {
            int width = grid.GetLength(0);
            int height = grid.GetLength(1);
            bool[,] visited = new bool[width, height];
            int components = 0;
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (grid[x, y] && !visited[x, y])
                    {
                        components++;
                        FloodFill(grid, visited, x, y, width, height);
                    }
                }
            }
            
            return components;
        }
        
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