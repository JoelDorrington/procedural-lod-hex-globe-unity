using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Simple test to debug PlanetTerrainTile component creation issues.
    /// </summary>
    public class TileComponentDebugTests
    {
        [Test]
        public void PlanetTerrainTile_Initialize_ShouldAddComponents()
        {
            // Arrange
            var testGameObject = new GameObject("TestTile");
            var terrainTile = testGameObject.AddComponent<PlanetTerrainTile>();
            var testTileId = new TileId(0, 0, 0, 1);

            // Create test mesh
            var testMesh = new Mesh();
            testMesh.name = "TestMesh";
            testMesh.vertices = new Vector3[] { Vector3.zero, Vector3.right, Vector3.up };
            testMesh.triangles = new int[] { 0, 1, 2 };

            // Create test TileData
            var testTileData = new TileData();
            testTileData.mesh = testMesh;
            testTileData.center = Vector3.zero;

            // Act
            terrainTile.Initialize(testTileId, testTileData, _ => testMesh);

            // Assert
            
            Assert.IsNotNull(terrainTile.meshFilter, "MeshFilter should be created");
            Assert.IsNotNull(terrainTile.meshRenderer, "MeshRenderer should be created");
            Assert.IsNotNull(terrainTile.meshCollider, "MeshCollider should be created");

            // Check if components are actually on the GameObject
            var meshFilter = testGameObject.GetComponent<MeshFilter>();
            var meshRenderer = testGameObject.GetComponent<MeshRenderer>();
            var meshCollider = testGameObject.GetComponent<MeshCollider>();

            Assert.IsNotNull(meshFilter, "GameObject should have MeshFilter component");
            Assert.IsNotNull(meshRenderer, "GameObject should have MeshRenderer component");
            Assert.IsNotNull(meshCollider, "GameObject should have MeshCollider component");

            // Check mesh assignment
            Assert.AreEqual(testMesh, meshFilter.sharedMesh, "MeshFilter should have correct mesh");
            Assert.AreEqual(testMesh, meshCollider.sharedMesh, "MeshCollider should have correct mesh");

            // Cleanup
            Object.DestroyImmediate(testGameObject);
            Object.DestroyImmediate(testMesh);
        }
    }
}
