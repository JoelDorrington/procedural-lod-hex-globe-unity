using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.PlayMode
{
    /// <summary>
    /// Diagnostic tests to isolate exactly why visibility selection is failing.
    /// </summary>
    public class VisibilityDiagnosticsTests
    {
        [UnityTest]
        public IEnumerator Diagnostics_VisibilitySetup_ValidatesComponents()
        {
            // Create a minimal but complete setup
            var managerGo = new GameObject("VisibilityManager");
            var manager = managerGo.AddComponent<PlanetTileVisibilityManager>();
            
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 1f;
            config.baseResolution = 8;
            manager.config = config;
            
            var camGo = new GameObject("Camera");
            var camera = camGo.AddComponent<Camera>();
            var cameraController = camGo.AddComponent<CameraController>();
            manager.GameCamera = cameraController;
            
            // Position camera to look at face 0
            var planetCenter = Vector3.zero;
            var face0Direction = IcosphereMapping.IcosahedronVertices[IcosphereMapping.IcosahedronFaces[0, 0]];
            camGo.transform.position = planetCenter + face0Direction * 3f;
            camGo.transform.LookAt(planetCenter);
            
            int testDepth = 1;
            manager.SetDepth(testDepth);
            
            yield return null; // Allow one frame for setup
            
            // DIAGNOSTIC 1: Verify basic component setup
            Debug.Log($"=== VISIBILITY DIAGNOSTICS ===");
            Debug.Log($"GameCamera: {manager.GameCamera}");
            Debug.Log($"Camera component: {manager.GameCamera?.GetComponent<Camera>()}");
            Debug.Log($"Camera position: {camGo.transform.position}");
            Debug.Log($"Camera forward: {camGo.transform.forward}");
            Debug.Log($"Planet center: {planetCenter}");
            Debug.Log($"Camera distance: {Vector3.Distance(camGo.transform.position, planetCenter)}");
            
            // DIAGNOSTIC 2: Check if tile registry has tiles
            var registryField = typeof(PlanetTileVisibilityManager)
                .GetField("tileRegistry", BindingFlags.NonPublic | BindingFlags.Instance);
            var tileRegistry = registryField.GetValue(manager) as Dictionary<int, TerrainTileRegistry>;
            
            if (tileRegistry != null && tileRegistry.TryGetValue(testDepth, out var registry))
            {
                Debug.Log($"Registry for depth {testDepth}: {registry}");
                Debug.Log($"Registry tile count: {registry.tiles.Count}");
                
                // Sample a few tiles to verify they exist
                int sampleCount = 0;
                foreach (var kvp in registry.tiles)
                {
                    if (sampleCount++ >= 3) break;
                    var tileData = kvp.Value;
                    Debug.Log($"  Sample tile {kvp.Key}: face={tileData.face}, center={tileData.centerWorld}");
                }
            }
            else
            {
                Debug.LogError("No tile registry found for current depth!");
            }
            
            // DIAGNOSTIC 3: Manually test visibility calculation parameters
            var camPos = camGo.transform.position;
            var camDir = (camPos - planetCenter).normalized;
            Debug.Log($"Camera direction: {camDir}");
            
            // Test if planet fills view calculation
            float planetRadius = config.baseRadius;
            float dist = (camPos - planetCenter).magnitude;
            float angularRadius = Mathf.Asin(Mathf.Min(1f, planetRadius / Mathf.Max(float.MinValue, dist)));
            bool planetFillsView = angularRadius > (Mathf.PI / 4f);
            Debug.Log($"Planet radius: {planetRadius}, Distance: {dist}, Angular radius: {angularRadius}, Planet fills view: {planetFillsView}");
            
            // DIAGNOSTIC 4: Call visibility update and see what happens
            var updateVisibilityMethod = typeof(PlanetTileVisibilityManager)
                .GetMethod("UpdateVisibilityMathBased", BindingFlags.NonPublic | BindingFlags.Instance);
            
            Debug.Log("Calling UpdateVisibilityMathBased...");
            updateVisibilityMethod.Invoke(manager, null);
            
            yield return null; // Allow spawning to process
            
            // DIAGNOSTIC 5: Check what tiles were spawned
            var spawnedTiles = new List<PlanetTerrainTile>();
            for (int i = 0; i < managerGo.transform.childCount; i++)
            {
                var child = managerGo.transform.GetChild(i);
                var tile = child.GetComponent<PlanetTerrainTile>();
                if (tile != null && child.gameObject.activeInHierarchy)
                {
                    spawnedTiles.Add(tile);
                }
            }
            
            Debug.Log($"Spawned tiles count: {spawnedTiles.Count}");
            foreach (var tile in spawnedTiles)
            {
                Debug.Log($"  Spawned tile: {tile.tileId}, position: {tile.transform.position}");
            }
            
            // DIAGNOSTIC 6: If no tiles spawned, try manual spawning to verify TrySpawnTile works
            if (spawnedTiles.Count == 0)
            {
                Debug.Log("No tiles spawned by visibility. Testing manual spawn...");
                var testTileId = new TileId(0, 0, 0, testDepth);
                var manualTile = manager.TrySpawnTile(testTileId);
                if (manualTile != null)
                {
                    Debug.Log($"Manual spawn successful: {testTileId}");
                }
                else
                {
                    Debug.LogError("Manual spawn also failed!");
                }
            }
            
            // The test itself - we expect SOME tiles to be visible from this position
            Assert.Greater(spawnedTiles.Count, 0, 
                "No tiles spawned despite proper setup. Check diagnostics above for details.");
            
            // Cleanup
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(managerGo);
            Object.DestroyImmediate(config);
        }
        
        [UnityTest]
        public IEnumerator Diagnostics_VisibilityThresholds_CheckCalculations()
        {
            // This test specifically focuses on the threshold calculations
            var managerGo = new GameObject("VisibilityManager");
            var manager = managerGo.AddComponent<PlanetTileVisibilityManager>();
            
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 1f;
            config.baseResolution = 8;
            manager.config = config;
            
            var camGo = new GameObject("Camera");
            var camera = camGo.AddComponent<Camera>();
            var cameraController = camGo.AddComponent<CameraController>();
            manager.GameCamera = cameraController;
            
            // Test different camera distances
            var planetCenter = Vector3.zero;
            var testPositions = new[]
            {
                planetCenter + Vector3.forward * 1.5f,  // Very close
                planetCenter + Vector3.forward * 2.5f,  // Medium distance
                planetCenter + Vector3.forward * 5.0f,  // Far away
            };
            
            int testDepth = 1;
            manager.SetDepth(testDepth);
            
            yield return null;
            
            foreach (var pos in testPositions)
            {
                camGo.transform.position = pos;
                camGo.transform.LookAt(planetCenter);
                
                float dist = Vector3.Distance(pos, planetCenter);
                float angularRadius = Mathf.Asin(Mathf.Min(1f, config.baseRadius / Mathf.Max(float.MinValue, dist)));
                bool planetFillsView = angularRadius > (Mathf.PI / 4f);
                
                Debug.Log($"=== DISTANCE TEST: {dist:F2} units ===");
                Debug.Log($"Angular radius: {angularRadius:F3} rad ({angularRadius * Mathf.Rad2Deg:F1}°)");
                Debug.Log($"Planet fills view: {planetFillsView}");
                
                // Call visibility update
                var updateVisibilityMethod = typeof(PlanetTileVisibilityManager)
                    .GetMethod("UpdateVisibilityMathBased", BindingFlags.NonPublic | BindingFlags.Instance);
                updateVisibilityMethod.Invoke(manager, null);
                
                yield return null;
                
                // Count spawned tiles
                int tileCount = 0;
                for (int i = 0; i < managerGo.transform.childCount; i++)
                {
                    var child = managerGo.transform.GetChild(i);
                    var tile = child.GetComponent<PlanetTerrainTile>();
                    if (tile != null && child.gameObject.activeInHierarchy)
                        tileCount++;
                }
                
                Debug.Log($"Tiles spawned: {tileCount}");
                
                // At least one position should spawn tiles
                if (tileCount > 0)
                {
                    Debug.Log($"SUCCESS: Found working distance at {dist:F2} units");
                }
            }
            
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(managerGo);
            Object.DestroyImmediate(config);
        }
    }
}