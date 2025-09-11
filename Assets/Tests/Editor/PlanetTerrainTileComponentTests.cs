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
            terrainTile.Initialize(testTileId, testTileData);

            // Assert
            Assert.IsNotNull(terrainTile.meshFilter, "MeshFilter component should be added");
            Assert.IsNotNull(terrainTile.meshRenderer, "MeshRenderer component should be added");
            
            // Verify components are actually on the GameObject
            Assert.IsNotNull(testGameObject.GetComponent<MeshFilter>(), "GameObject should have MeshFilter component");
            Assert.IsNotNull(testGameObject.GetComponent<MeshRenderer>(), "GameObject should have MeshRenderer component");
        }

        [Test]
        public void Initialize_ShouldSetTileData()
        {
            // Act
            terrainTile.Initialize(testTileId, testTileData);

            // Assert
            Assert.AreEqual(testTileId, terrainTile.tileId, "TileId should be set correctly");
            Assert.AreEqual(testTileData, terrainTile.tileData, "TileData should be set correctly");
            Assert.AreEqual(testTileData.center, testGameObject.transform.position, "GameObject position should match tile center");
        }

        [Test]
        public void ConfigureMaterialAndLayer_ShouldAssignMaterial()
        {
            // Arrange
            terrainTile.Initialize(testTileId, testTileData);

            // Act
            terrainTile.ConfigureMaterialAndLayer(testMaterial);

            // Assert
            Assert.IsNotNull(terrainTile.meshRenderer.sharedMaterial, "MeshRenderer should have a material");
            Assert.AreEqual("TestMaterial (Tile Instance)", terrainTile.meshRenderer.sharedMaterial.name, "Material should be an instance with correct name");
            Assert.AreEqual(testMaterial, terrainTile.baseMaterial, "Base material should be stored correctly");
        }

        [Test]
        public void GetDiagnosticInfo_ShouldReturnCorrectMeshInfo()
        {
            // Arrange
            terrainTile.Initialize(testTileId, testTileData);
            terrainTile.ConfigureMaterialAndLayer(testMaterial);

            terrainTile.meshFilter.mesh = testMesh; // Assign test mesh to MeshFilter

            // Act
            var diagnostics = terrainTile.GetDiagnosticInfo();

            // Assert
            Assert.AreEqual(testGameObject.name, diagnostics.tileName, "Diagnostic should have correct tile name");
            Assert.AreEqual(4, diagnostics.vertexCount, "Diagnostic should show correct vertex count");
            Assert.AreEqual(2, diagnostics.triangleCount, "Diagnostic should show correct triangle count");
            Assert.AreEqual("TestMaterial (Tile Instance)", diagnostics.materialName, "Diagnostic should show correct material name");
            Assert.IsTrue(diagnostics.rendererEnabled, "Diagnostic should show renderer is enabled");
        }

        [Test]
        public void Initialize_WithNullTileData_ShouldNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => {
                terrainTile.Initialize(testTileId, null);
            }, "Initialize should handle null TileData gracefully");
        }

        [Test]
        public void ConfigureMaterialAndLayer_WithNullMaterial_ShouldNotThrow()
        {
            // Arrange
            terrainTile.Initialize(testTileId, testTileData);

            // Act & Assert
            Assert.DoesNotThrow(() => {
                terrainTile.ConfigureMaterialAndLayer(null);
            }, "ConfigureMaterialAndLayer should handle null material gracefully");
        }
    }
}
