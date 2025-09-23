using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.PlayMode
{
    /// <summary>
    /// Conservative diagnostic to test if visibility threshold values are too restrictive.
    /// </summary>
    public class VisibilityThresholdDiagnosticTests
    {
        [UnityTest]
        public IEnumerator Diagnostics_VisibilityThresholds_TestDifferentValues()
        {
            // Create minimal setup
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
            
            // Position camera to look directly at face 0
            var planetCenter = Vector3.zero;
            var face0Direction = IcosphereMapping.IcosahedronVertices[IcosphereMapping.IcosahedronFaces[0, 0]];
            camGo.transform.position = planetCenter + face0Direction * 2.5f;
            camGo.transform.LookAt(planetCenter);
            
            int testDepth = 1;
            manager.SetDepth(testDepth);
            
            yield return null;
            
            // Get the private threshold fields using reflection
            var minThresholdField = typeof(PlanetTileVisibilityManager)
                .GetField("minCenterDotThreshold", BindingFlags.NonPublic | BindingFlags.Instance);
            var maxThresholdField = typeof(PlanetTileVisibilityManager)
                .GetField("maxDotProductThreshold", BindingFlags.NonPublic | BindingFlags.Instance);
            var clampFactorField = typeof(PlanetTileVisibilityManager)
                .GetField("visibilityConeClampFactor", BindingFlags.NonPublic | BindingFlags.Instance);
            
            var updateVisibilityMethod = typeof(PlanetTileVisibilityManager)
                .GetMethod("UpdateVisibilityMathBased", BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Record original values
            float originalMinThreshold = (float)minThresholdField.GetValue(manager);
            float originalMaxThreshold = (float)maxThresholdField.GetValue(manager);
            float originalClampFactor = (float)clampFactorField.GetValue(manager);
            
            Debug.Log($"=== ORIGINAL THRESHOLD VALUES ===");
            Debug.Log($"minCenterDotThreshold: {originalMinThreshold}");
            Debug.Log($"maxDotProductThreshold: {originalMaxThreshold}");
            Debug.Log($"visibilityConeClampFactor: {originalClampFactor}");
            
            // Test different threshold values
            var testValues = new[]
            {
                new { min = 0.35f, max = 0.8f, clamp = 1.25f, name = "Original" },
                new { min = 0.1f, max = 0.8f, clamp = 1.25f, name = "Relaxed Min" },
                new { min = 0.0f, max = 0.8f, clamp = 1.25f, name = "Zero Min" },
                new { min = 0.0f, max = 1.0f, clamp = 1.0f, name = "Very Permissive" }
            };
            
            foreach (var test in testValues)
            {
                Debug.Log($"\n=== TESTING: {test.name} ===");
                Debug.Log($"Setting min={test.min}, max={test.max}, clamp={test.clamp}");
                
                // Set test values
                minThresholdField.SetValue(manager, test.min);
                maxThresholdField.SetValue(manager, test.max);
                clampFactorField.SetValue(manager, test.clamp);
                
                // Clear any existing tiles
                for (int i = managerGo.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(managerGo.transform.GetChild(i).gameObject);
                }
                
                // Call visibility update
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
                
                Debug.Log($"Result: {tileCount} tiles spawned");
                
                if (tileCount > 0)
                {
                    Debug.Log($"SUCCESS: {test.name} configuration spawned tiles!");
                    break; // Found working config
                }
            }
            
            // Restore original values
            minThresholdField.SetValue(manager, originalMinThreshold);
            maxThresholdField.SetValue(manager, originalMaxThreshold);
            clampFactorField.SetValue(manager, originalClampFactor);
            
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(managerGo);
            Object.DestroyImmediate(config);
        }
        
        [UnityTest]
        public IEnumerator Diagnostics_ManualTileSpawn_VerifyMechanics()
        {
            // Test if the manual tile spawning works independently of visibility
            var managerGo = new GameObject("VisibilityManager");
            var manager = managerGo.AddComponent<PlanetTileVisibilityManager>();
            
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 1f;
            config.baseResolution = 8;
            manager.config = config;
            
            int testDepth = 1;
            manager.SetDepth(testDepth);
            
            yield return null;
            
            Debug.Log("=== MANUAL TILE SPAWN TEST ===");
            
            // Try spawning a few specific tiles manually
            var testTiles = new[]
            {
                new TileId(0, 0, 0, testDepth),
                new TileId(0, 1, 0, testDepth),
                new TileId(1, 0, 0, testDepth)
            };
            
            int successCount = 0;
            foreach (var tileId in testTiles)
            {
                var go = manager.TrySpawnTile(tileId);
                if (go != null)
                {
                    successCount++;
                    Debug.Log($"Manual spawn SUCCESS: {tileId}");
                }
                else
                {
                    Debug.LogError($"Manual spawn FAILED: {tileId}");
                }
            }
            
            Debug.Log($"Manual spawn results: {successCount}/{testTiles.Length} successful");
            
            Assert.Greater(successCount, 0, "Manual tile spawning is completely broken");
            
            Object.DestroyImmediate(managerGo);
            Object.DestroyImmediate(config);
        }
    }
}