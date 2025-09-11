using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem;

namespace HexGlobeProject.Tests.PlayMode
{
    public class TerrainShaderParameterPlayModeTests
    {
        [UnityTest]
        public IEnumerator TerrainTile_ShouldSetPlanetCenterParameter()
        {
            // Create a test material with our terrain shader
            var shader = Shader.Find("HexGlobe/PlanetTerrain");
            Assert.IsNotNull(shader, "PlanetTerrain shader should be available");
            
            var testMaterial = new Material(shader);
            testMaterial.name = "TestTerrainMaterial";

            // Create a test tile
            var testGO = new GameObject("TestTile");
            var meshRenderer = testGO.AddComponent<MeshRenderer>();
            var meshFilter = testGO.AddComponent<MeshFilter>();
            
            // Create a simple mesh for the tile
            var mesh = new Mesh();
            mesh.vertices = new Vector3[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new int[] { 0, 1, 2 };
            meshFilter.mesh = mesh;

            var terrainTile = testGO.AddComponent<PlanetTerrainTile>();
            
            // Wait a frame for Awake to be called
            yield return null;
            
            // Configure with a specific planet center
            Vector3 expectedPlanetCenter = new Vector3(100f, 200f, 300f);
            terrainTile.ConfigureMaterialAndLayer(testMaterial, expectedPlanetCenter);

            // Verify the material instance was created and has the correct planet center
            Assert.IsNotNull(terrainTile.meshRenderer, "MeshRenderer should exist");
            Assert.IsNotNull(terrainTile.meshRenderer.material, "Material should be assigned");
            
            var material = terrainTile.meshRenderer.material;
            if (material.HasProperty("_PlanetCenter"))
            {
                Vector4 actualPlanetCenter = material.GetVector("_PlanetCenter");
                Assert.AreEqual(expectedPlanetCenter.x, actualPlanetCenter.x, 0.001f, "Planet center X should match");
                Assert.AreEqual(expectedPlanetCenter.y, actualPlanetCenter.y, 0.001f, "Planet center Y should match");
                Assert.AreEqual(expectedPlanetCenter.z, actualPlanetCenter.z, 0.001f, "Planet center Z should match");
            }
            else
            {
                Assert.Fail("Material should have _PlanetCenter property");
            }

            // Cleanup
            Object.Destroy(testGO);
            Object.Destroy(testMaterial);
        }

        [UnityTest]
        public IEnumerator VisibilityManager_ShouldSetPlanetCenterOnSpawnedTiles()
        {
            // Create a terrain config
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 10f;
            config.heightScale = 1f;
            config.baseResolution = 8;
            config.heightProvider = new SimplePerlinHeightProvider();

            // Create a test material with terrain shader
            var shader = Shader.Find("HexGlobe/PlanetTerrain");
            Assert.IsNotNull(shader, "PlanetTerrain shader should be available");
            var testMaterial = new Material(shader);
            
            // Debug: Check if the base material has the property
            Assert.IsTrue(testMaterial.HasProperty("_PlanetCenter"), "Shader must expose _PlanetCenter property");
            Vector4 defaultValue = testMaterial.GetVector("_PlanetCenter");
            Assert.AreEqual(Vector4.zero, defaultValue, "Default _PlanetCenter should be (0,0,0,0)");

            // Create a visibility manager at a specific position
            var managerGO = new GameObject("TestManager");
            managerGO.SetActive(false);
            Vector3 planetCenter = new Vector3(50f, 25f, 75f);
            managerGO.transform.position = planetCenter;
            
            var manager = managerGO.AddComponent<PlanetTileVisibilityManager>();
            manager.config = config;
            
            // Set the terrain material using reflection
            var terrainMaterialField = typeof(PlanetTileVisibilityManager).GetField("terrainMaterial", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            terrainMaterialField.SetValue(manager, testMaterial);
            
            // Verify the material was set correctly
            var retrievedMaterial = terrainMaterialField.GetValue(manager) as Material;
            Assert.IsNotNull(retrievedMaterial, "Terrain material should be set on visibility manager");

            yield return null;
            managerGO.SetActive(true);
            yield return null; // Let Awake run
            // Set depth to spawn tiles
            manager.SetDepth(0);
            
            yield return new WaitForSeconds(0.1f); // Let tiles spawn

            // Get the spawned tiles
            var activeTiles = manager.GetActiveTiles();
            Assert.Greater(activeTiles.Count, 0, "Should have spawned some tiles");

            // Check that at least one tile has the correct planet center set
            bool foundCorrectPlanetCenter = false;
            foreach (var tile in activeTiles)
            {
                if (tile.meshRenderer != null && tile.meshRenderer.material != null)
                {
                    var material = tile.meshRenderer.material;
                    if (material.HasProperty("_PlanetCenter"))
                    {
                        Vector4 actualPlanetCenter = material.GetVector("_PlanetCenter");
                        
                        if (Mathf.Approximately(actualPlanetCenter.x, planetCenter.x) &&
                            Mathf.Approximately(actualPlanetCenter.y, planetCenter.y) &&
                            Mathf.Approximately(actualPlanetCenter.z, planetCenter.z))
                        {
                            foundCorrectPlanetCenter = true;
                            break;
                        }
                    }
                    else
                    {
                            // material doesn't support _PlanetCenter
                    }
                }
                else
                {
                        // missing renderer or material
                }
            }

            Assert.IsTrue(foundCorrectPlanetCenter, $"At least one tile should have planet center set to {planetCenter}");

            // Cleanup
            Object.Destroy(managerGO);
            Object.Destroy(testMaterial);
            Object.Destroy(config);
        }
    }
}
