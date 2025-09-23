using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Unit tests for tile visibility selection logic to catch missing/incorrect tile culling.
    /// </summary>
    public class TileVisibilitySelectionTests
    {
        [Test]
        public void VisibilityLogic_CenterDotThreshold_DoesNotExcludeFacingTiles()
        {
            // Test that tiles directly facing the camera are always considered visible
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 1f;
            
            var go = new GameObject("TestVisibilityManager");
            var manager = go.AddComponent<PlanetTileVisibilityManager>();
            manager.config = config;
            
            // Create a mock camera pointing at planet center
            var camGo = new GameObject("TestCamera");
            var camera = camGo.AddComponent<Camera>();
            var cameraController = camGo.AddComponent<CameraController>();
            manager.GameCamera = cameraController;
            
            // Position camera to look directly at face 0 (first icosphere face)
            var planetCenter = Vector3.zero;
            var face0Direction = IcosphereMapping.IcosahedronVertices[IcosphereMapping.IcosahedronFaces[0, 0]];
            camGo.transform.position = planetCenter + face0Direction * 3f; // 3 units away
            camGo.transform.LookAt(planetCenter);
            
            // Set depth to 1 so we have multiple tiles per face
            int testDepth = 1;
            manager.SetDepth(testDepth);
            
            // Let manager process one frame using reflection
            var updateVisibilityMethod = typeof(PlanetTileVisibilityManager)
                .GetMethod("UpdateVisibilityMathBased", BindingFlags.NonPublic | BindingFlags.Instance);
            updateVisibilityMethod.Invoke(manager, null);
            
            // Get all spawned tiles
            var spawnedTiles = new List<PlanetTerrainTile>();
            for (int i = 0; i < manager.transform.childCount; i++)
            {
                var tile = manager.transform.GetChild(i).GetComponent<PlanetTerrainTile>();
                if (tile != null && tile.gameObject.activeInHierarchy)
                    spawnedTiles.Add(tile);
            }
            
            // Assert: At least one tile from face 0 should be visible since camera is pointing at it
            var face0Tiles = spawnedTiles.Where(t => t.tileId.face == 0).ToList();
            Assert.Greater(face0Tiles.Count, 0, 
                "No tiles from face 0 were spawned despite camera pointing directly at face 0");
            
            // Assert: The tile at (0,0) on face 0 should definitely be visible (center of face)
            var centerTile = spawnedTiles.FirstOrDefault(t => 
                t.tileId.face == 0 && t.tileId.x == 0 && t.tileId.y == 0 && t.tileId.depth == testDepth);
            Assert.IsNotNull(centerTile, 
                "Center tile (0,0) of face 0 was not spawned despite camera pointing at face center");
                
            // Cleanup
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }
        
        [Test]
        public void VisibilityLogic_PlanetFillsView_UsesCorrectThreshold()
        {
            // Test the center-dot threshold logic when planet fills the camera view
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 1f;
            
            var go = new GameObject("TestVisibilityManager");
            var manager = go.AddComponent<PlanetTileVisibilityManager>();
            manager.config = config;
            
            var camGo = new GameObject("TestCamera");
            var camera = camGo.AddComponent<Camera>();
            var cameraController = camGo.AddComponent<CameraController>();
            manager.GameCamera = cameraController;
            
            // Position camera very close so planet fills the view
            var planetCenter = Vector3.zero;
            camGo.transform.position = planetCenter + Vector3.forward * 1.5f; // Close to surface
            camGo.transform.LookAt(planetCenter);
            
            int testDepth = 1;
            manager.SetDepth(testDepth);
            
            // Use reflection to call private UpdateVisibilityMathBased method
            var updateVisibilityMethod = typeof(PlanetTileVisibilityManager)
                .GetMethod("UpdateVisibilityMathBased", BindingFlags.NonPublic | BindingFlags.Instance);
            updateVisibilityMethod.Invoke(manager, null);
            
            // Get visible tiles
            var visibleTiles = new List<PlanetTerrainTile>();
            for (int i = 0; i < manager.transform.childCount; i++)
            {
                var tile = manager.transform.GetChild(i).GetComponent<PlanetTerrainTile>();
                if (tile != null && tile.gameObject.activeInHierarchy)
                    visibleTiles.Add(tile);
            }
            
            // When planet fills view, we should still have reasonable coverage
            // (not too restrictive that it creates black areas)
            Assert.Greater(visibleTiles.Count, 0, 
                "No tiles visible when planet fills view - threshold may be too restrictive");
            
            // Should have tiles from the facing hemisphere
            var facingTiles = visibleTiles.Where(t => {
                var tileCenter = t.tileData.center;
                var tileDir = (tileCenter - planetCenter).normalized;
                return Vector3.Dot(tileDir, Vector3.forward) > 0;
            }).ToList();
            
            Assert.Greater(facingTiles.Count, 0,
                "No tiles from facing hemisphere visible when camera looks at planet center");
            
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }
    }
}