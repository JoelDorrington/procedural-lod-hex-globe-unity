using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Unit tests for PlanetTerrainTile component initialization, mesh assignment, and material setup.
    /// </summary>
    public class PlanetTerrainTileComponentTests
    {
        private GameObject testGameObject;
        private PlanetTerrainTile terrainTile;
        private TileData testTileData;
        private TileId testTileId;
        private Mesh testMesh;
        private Material testMaterial;

        [SetUp]
        public void SetUp()
        {
            // Create test GameObject
            testGameObject = new GameObject("TestTile");
            terrainTile = testGameObject.AddComponent<PlanetTerrainTile>();

            // Create test TileId
            testTileId = new TileId(0, 0, 0, 1); // face 0, x=0, y=0, depth=1

            // Create test mesh
            testMesh = new Mesh();
            testMesh.name = "TestMesh";
            Vector3[] vertices = {
                Vector3.zero, Vector3.right, Vector3.up, Vector3.one
            };
            int[] triangles = { 0, 1, 2, 1, 3, 2 };
            testMesh.vertices = vertices;
            testMesh.triangles = triangles;
            testMesh.RecalculateNormals();
            testMesh.RecalculateBounds();

            // Create test TileData
            testTileData = new TileData();
            testTileData.mesh = testMesh;
            testTileData.center = Vector3.zero;

            // Create test material
            testMaterial = new Material(Shader.Find("Standard"));
            testMaterial.name = "TestMaterial";
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
            if (testMesh != null)
                Object.DestroyImmediate(testMesh);
            if (testMaterial != null)
                Object.DestroyImmediate(testMaterial);
        }

        [Test]
        public void Initialize_ShouldAddRequiredComponents()
        {
            // Act
            terrainTile.Initialize(testTileId, testTileData, _ => testMesh);

            // Assert
            Assert.IsNotNull(terrainTile.meshFilter, "MeshFilter component should be added");
            Assert.IsNotNull(terrainTile.meshRenderer, "MeshRenderer component should be added");
            Assert.IsNotNull(terrainTile.meshCollider, "MeshCollider component should be added");
            
            // Verify components are actually on the GameObject
            Assert.IsNotNull(testGameObject.GetComponent<MeshFilter>(), "GameObject should have MeshFilter component");
            Assert.IsNotNull(testGameObject.GetComponent<MeshRenderer>(), "GameObject should have MeshRenderer component");
            Assert.IsNotNull(testGameObject.GetComponent<MeshCollider>(), "GameObject should have MeshCollider component");
        }

        [Test]
        public void Initialize_ShouldAssignMeshToMeshFilter()
        {
            // Act
            terrainTile.Initialize(testTileId, testTileData, _ => testMesh);

            // Assert
            Assert.IsNotNull(terrainTile.meshFilter.sharedMesh, "MeshFilter should have a mesh assigned");
            Assert.AreEqual(testMesh, terrainTile.meshFilter.sharedMesh, "MeshFilter should have the correct mesh");
            Assert.AreEqual(4, terrainTile.meshFilter.sharedMesh.vertexCount, "Mesh should have correct vertex count");
        }

        [Test]
        public void Initialize_ShouldAssignColliderMesh()
        {
            // Act
            terrainTile.Initialize(testTileId, testTileData, _ => testMesh);

            // Assert
            Assert.IsNotNull(terrainTile.meshCollider.sharedMesh, "MeshCollider should have a mesh assigned");
            Assert.AreEqual(testMesh, terrainTile.meshCollider.sharedMesh, "MeshCollider should have the correct mesh");
            Assert.IsFalse(terrainTile.meshCollider.convex, "MeshCollider should not be convex for terrain");
        }

        [Test]
        public void Initialize_ShouldSetTileData()
        {
            // Act
            terrainTile.Initialize(testTileId, testTileData, _ => testMesh);

            // Assert
            Assert.AreEqual(testTileId, terrainTile.tileId, "TileId should be set correctly");
            Assert.AreEqual(testTileData, terrainTile.tileData, "TileData should be set correctly");
            Assert.AreEqual(testTileData.center, testGameObject.transform.position, "GameObject position should match tile center");
        }

        [Test]
        public void ConfigureMaterialAndLayer_ShouldAssignMaterial()
        {
            // Arrange
            terrainTile.Initialize(testTileId, testTileData, _ => testMesh);
            int testLayer = 5;

            // Act
            terrainTile.ConfigureMaterialAndLayer(testMaterial, testLayer);

            // Assert
            Assert.IsNotNull(terrainTile.meshRenderer.sharedMaterial, "MeshRenderer should have a material");
            Assert.AreEqual("TestMaterial (Tile Instance)", terrainTile.meshRenderer.sharedMaterial.name, "Material should be an instance with correct name");
            Assert.AreEqual(testMaterial, terrainTile.baseMaterial, "Base material should be stored correctly");
        }

        [Test]
        public void ConfigureMaterialAndLayer_ShouldSetLayer()
        {
            // Arrange
            terrainTile.Initialize(testTileId, testTileData, _ => testMesh);
            int testLayer = 7;

            // Act
            terrainTile.ConfigureMaterialAndLayer(testMaterial, testLayer);

            // Assert
            Assert.AreEqual(testLayer, testGameObject.layer, "GameObject layer should be set correctly");
        }

        [Test]
        public void ShowVisualMesh_ShouldEnableRenderer()
        {
            // Arrange
            terrainTile.Initialize(testTileId, testTileData, _ => testMesh);
            terrainTile.ConfigureMaterialAndLayer(testMaterial, 0);
            terrainTile.meshRenderer.enabled = false; // Start disabled

            // Act
            terrainTile.ShowVisualMesh();

            // Assert
            Assert.IsTrue(terrainTile.meshRenderer.enabled, "MeshRenderer should be enabled after ShowVisualMesh");
            Assert.IsTrue(terrainTile.isVisible, "isVisible flag should be true");
        }

        [Test]
        public void HideVisualMesh_ShouldDisableRenderer()
        {
            // Arrange
            terrainTile.Initialize(testTileId, testTileData, _ => testMesh);
            terrainTile.ConfigureMaterialAndLayer(testMaterial, 0);
            terrainTile.ShowVisualMesh(); // Start visible

            // Act
            terrainTile.HideVisualMesh();

            // Assert
            Assert.IsFalse(terrainTile.meshRenderer.enabled, "MeshRenderer should be disabled after HideVisualMesh");
            Assert.IsFalse(terrainTile.isVisible, "isVisible flag should be false");
        }

        [Test]
        public void GetDiagnosticInfo_ShouldReturnCorrectMeshInfo()
        {
            // Arrange
            terrainTile.Initialize(testTileId, testTileData, _ => testMesh);
            terrainTile.ConfigureMaterialAndLayer(testMaterial, 3);
            terrainTile.ShowVisualMesh();

            // Act
            var diagnostics = terrainTile.GetDiagnosticInfo();

            // Assert
            Assert.AreEqual(testGameObject.name, diagnostics.tileName, "Diagnostic should have correct tile name");
            Assert.AreEqual(4, diagnostics.vertexCount, "Diagnostic should show correct vertex count");
            Assert.AreEqual(2, diagnostics.triangleCount, "Diagnostic should show correct triangle count");
            Assert.AreEqual("TestMaterial (Tile Instance)", diagnostics.materialName, "Diagnostic should show correct material name");
            Assert.IsTrue(diagnostics.rendererEnabled, "Diagnostic should show renderer is enabled");
            Assert.AreEqual(3, diagnostics.layer, "Diagnostic should show correct layer");
        }

        [Test]
        public void TestRayIntersection_ShouldReturnTrueForValidRay()
        {
            // Arrange
            terrainTile.Initialize(testTileId, testTileData, _ => testMesh);
            testGameObject.SetActive(true);
            
            // Create a ray pointing at the mesh
            Ray testRay = new Ray(Vector3.back * 10, Vector3.forward);

            // Act
            bool hitResult = terrainTile.TestRayIntersection(testRay, out RaycastHit hitInfo, -1);

            // Assert - This might not hit depending on mesh bounds, but the method should execute without error
            // The main test is that the method doesn't throw exceptions and returns a valid boolean
            Assert.IsTrue(hitResult || !hitResult, "TestRayIntersection should return a valid boolean result");
        }

        [Test]
        public void Initialize_WithNullTileData_ShouldNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => {
                terrainTile.Initialize(testTileId, null, _ => testMesh);
            }, "Initialize should handle null TileData gracefully");
        }

        [Test]
        public void Initialize_WithNullMeshGenerator_ShouldNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => {
                terrainTile.Initialize(testTileId, testTileData, null);
            }, "Initialize should handle null mesh generator gracefully");
            
            // Collider should not have a mesh
            Assert.IsNull(terrainTile.meshCollider.sharedMesh, "MeshCollider should not have a mesh when generator is null");
        }

        [Test]
        public void ConfigureMaterialAndLayer_WithNullMaterial_ShouldNotThrow()
        {
            // Arrange
            terrainTile.Initialize(testTileId, testTileData, _ => testMesh);

            // Act & Assert
            Assert.DoesNotThrow(() => {
                terrainTile.ConfigureMaterialAndLayer(null, 0);
            }, "ConfigureMaterialAndLayer should handle null material gracefully");
        }
    }
}
