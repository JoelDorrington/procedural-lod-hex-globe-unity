using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Integration tests for PlanetTileVisibilityManager tile spawning and component setup.
    /// </summary>
    public class TileSpawningIntegrationTests
    {
        private GameObject managerGameObject;
        private PlanetTileVisibilityManager visibilityManager;
        private GameObject planetGameObject;
        private GameObject cameraGameObject;
        private CameraController cameraController;
        private Material testMaterial;
        private TerrainConfig testConfig;
        private TileId testTileId;

        [SetUp]
        public void SetUp()
        {
            // Create camera GameObject with CameraController
            cameraGameObject = new GameObject("TestCamera");
            cameraGameObject.AddComponent<Camera>();
            cameraController = cameraGameObject.AddComponent<CameraController>();

            // Create manager GameObject
            managerGameObject = new GameObject("TestVisibilityManager");
            visibilityManager = managerGameObject.AddComponent<PlanetTileVisibilityManager>();

            // Create planet GameObject
            planetGameObject = new GameObject("TestPlanet");
            
            // Create test material
            testMaterial = new Material(Shader.Find("Standard"));
            testMaterial.name = "TestTerrainMaterial";

            // Create TerrainConfig
            testConfig = ScriptableObject.CreateInstance<TerrainConfig>();
            testConfig.baseRadius = 10f; // Set a reasonable radius
            testConfig.baseResolution = 32; // ensure precompute matches mesh builder sampling

            // Setup visibility manager using public properties and SerializedObject for private fields
            visibilityManager.GameCamera = cameraController;
            visibilityManager.config = testConfig;

            // Clear any cached meshes from previous tests to avoid stale center/mesh reuse
            var cacheField = typeof(PlanetTileMeshBuilder).GetField("s_meshCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (cacheField != null)
            {
                var cacheObj = cacheField.GetValue(null) as System.Collections.IDictionary;
                try { cacheObj?.Clear(); } catch { }
            }

            // Use SerializedObject to set private fields
            var serializedObject = new SerializedObject(visibilityManager);
            
            var planetTransformProperty = serializedObject.FindProperty("planetTransform");
            if (planetTransformProperty != null)
                planetTransformProperty.objectReferenceValue = planetGameObject.transform;
                
            var terrainMaterialProperty = serializedObject.FindProperty("terrainMaterial");
            if (terrainMaterialProperty != null)
                terrainMaterialProperty.objectReferenceValue = testMaterial;
                
            var terrainTileLayerProperty = serializedObject.FindProperty("terrainTileLayer");
            if (terrainTileLayerProperty != null)
                terrainTileLayerProperty.intValue = 3; // TerrainTiles layer

            serializedObject.ApplyModifiedProperties();

            // Create test TileId
            testTileId = new TileId(0, 0, 0, 1);

            // Precompute tile normals for the test depth
            var precomputeMethod = typeof(PlanetTileVisibilityManager).GetMethod("PrecomputeTileNormalsForDepth",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (precomputeMethod != null)
            {
                precomputeMethod.Invoke(visibilityManager, new object[] { testTileId.depth });
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up any spawned tiles - use public API if available
            // For now, just destroy the manager which should clean up tiles
            if (managerGameObject != null)
                Object.DestroyImmediate(managerGameObject);
            if (planetGameObject != null)
                Object.DestroyImmediate(planetGameObject);
            if (cameraGameObject != null)
                Object.DestroyImmediate(cameraGameObject);
            if (testMaterial != null)
                Object.DestroyImmediate(testMaterial);
            if (testConfig != null)
                Object.DestroyImmediate(testConfig);
        }

        [Test]
        public void TrySpawnTile_ShouldCreateTileWithComponents()
        {
            // Act - Use the public TrySpawnTile method
            var tileGameObject = visibilityManager.TrySpawnTile(testTileId, 32);

            // Assert
            Assert.IsNotNull(tileGameObject, "Tile GameObject should be created");
            
            // Check for PlanetTerrainTile component
            var terrainTile = tileGameObject.GetComponent<PlanetTerrainTile>();
            Assert.IsNotNull(terrainTile, "Tile should have PlanetTerrainTile component");
            
            // Check for required components
            Assert.IsNotNull(terrainTile.meshFilter, "Tile should have MeshFilter");
            Assert.IsNotNull(terrainTile.meshRenderer, "Tile should have MeshRenderer");
            Assert.IsNotNull(terrainTile.meshCollider, "Tile should have MeshCollider");
            
            // Check that components are actually on the GameObject
            Assert.IsNotNull(tileGameObject.GetComponent<MeshFilter>(), "GameObject should have MeshFilter component");
            Assert.IsNotNull(tileGameObject.GetComponent<MeshRenderer>(), "GameObject should have MeshRenderer component");
            Assert.IsNotNull(tileGameObject.GetComponent<MeshCollider>(), "GameObject should have MeshCollider component");
        }

        [Test]
        public void TrySpawnTile_ShouldAssignCorrectParent()
        {
            // Act
            var tileGameObject = visibilityManager.TrySpawnTile(testTileId, 32);

            // Assert
            Assert.AreEqual(planetGameObject.transform, tileGameObject.transform.parent, "Tile should be parented to planet GameObject");
        }

        [Test]
        public void TrySpawnTile_ShouldAssignCorrectLayer()
        {
            // Act
            var tileGameObject = visibilityManager.TrySpawnTile(testTileId, 32);

            // Assert
            Assert.AreEqual(3, tileGameObject.layer, "Tile should be on TerrainTiles layer (3)");
        }

        [Test]
        public void TrySpawnTile_ShouldAssignMaterial()
        {
            // Act
            var tileGameObject = visibilityManager.TrySpawnTile(testTileId, 32);

            // Assert
            var terrainTile = tileGameObject.GetComponent<PlanetTerrainTile>();
            Assert.IsNotNull(terrainTile.meshRenderer.sharedMaterial, "Tile should have a material assigned");
            
            // Should be a material instance, not the original
            Assert.AreNotSame(testMaterial, terrainTile.meshRenderer.sharedMaterial, "Should use a material instance, not the original");
            Assert.IsTrue(terrainTile.meshRenderer.sharedMaterial.name.Contains("TestTerrainMaterial"), "Material name should contain base material name");
        }

        [Test]
        public void TrySpawnTile_ShouldGenerateMesh()
        {
            // This test verifies that the mesh generation pipeline is called
            // The actual mesh content depends on the mesh builder implementation
            
            // Act
            var tileGameObject = visibilityManager.TrySpawnTile(testTileId, 32);

            // Assert
            var terrainTile = tileGameObject.GetComponent<PlanetTerrainTile>();
            
            // The tile should have been initialized - exact mesh content depends on mesh builder
            Assert.AreEqual(testTileId, terrainTile.tileId, "Tile should have correct TileId");
            Assert.IsNotNull(terrainTile.tileData, "Tile should have TileData");
        }

        [Test]
        public void TrySpawnTile_MultipleCalls_ShouldReturnSameObject()
        {
            // Act - Call TrySpawnTile twice with same TileId
            var tileGameObject1 = visibilityManager.TrySpawnTile(testTileId, 32);
            var tileGameObject2 = visibilityManager.TrySpawnTile(testTileId, 32);

            // Assert - Should return the same object, not create a new one
            Assert.AreSame(tileGameObject1, tileGameObject2, "Multiple calls with same TileId should return same GameObject");
        }

        [Test]
        public void TrySpawnTile_ShouldSetCorrectTileId()
        {
            // Act
            var tileGameObject = visibilityManager.TrySpawnTile(testTileId, 32);

            // Assert
            var terrainTile = tileGameObject.GetComponent<PlanetTerrainTile>();
            Assert.AreEqual(testTileId, terrainTile.tileId, "Tile should have correct TileId assigned");
        }

        [Test]
        public void TrySpawnTile_ShouldPositionGameObjectAtPrecomputedCenter()
        {
            // Act
            var tileGameObject = visibilityManager.TrySpawnTile(testTileId, 32);

            // Assert: the spawned GameObject.transform.position must equal the precomputed entry.centerWorld
            var terrainTile = tileGameObject.GetComponent<PlanetTerrainTile>();
            Assert.IsNotNull(terrainTile, "Tile should have PlanetTerrainTile component");
            // The authoritative placement is the TileData.center produced during mesh build.
            Assert.AreEqual(terrainTile.tileData.center, tileGameObject.transform.position, "Tile GameObject should be positioned at the tileData.center used when building the mesh");
        }

        [Test]
        public void TrySpawnTile_MeshShouldBeLocalCentered()
        {
            // Act
            var tileGameObject = visibilityManager.TrySpawnTile(testTileId, 32);
            var terrainTile = tileGameObject.GetComponent<PlanetTerrainTile>();

            // Assert: mesh bounds center should be approximately zero (vertices converted to local-space)
            var mesh = terrainTile.meshFilter.sharedMesh;
            Assert.IsNotNull(mesh, "Spawned tile should have a mesh assigned");

            // Robust check: compute the mesh's world-space vertex centroid and ensure it matches
            // the precomputed registry centerWorld used for spawning. This avoids brittle
            // assumptions about mesh.bounds.center which can differ based on winding/triangulation.
            var verts = mesh.vertices;
            Assert.IsTrue(verts.Length > 0, "Mesh should contain vertices to compute centroid");
            Vector3 avg = Vector3.zero;
            foreach (var v in verts) avg += terrainTile.transform.TransformPoint(v);
            avg /= verts.Length;

            float tol = 0.2f; // Tolerance for terrain sampling differences between precompute and mesh generation
            Assert.AreEqual(terrainTile.tileData.center.x, avg.x, tol, $"Mesh world-centroid.x should match tileData.center.x (tol={tol})");
            Assert.AreEqual(terrainTile.tileData.center.y, avg.y, tol, $"Mesh world-centroid.y should match tileData.center.y (tol={tol})");
            Assert.AreEqual(terrainTile.tileData.center.z, avg.z, tol, $"Mesh world-centroid.z should match tileData.center.z (tol={tol})");
        }
    }
}
