using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Reproduces the hide/show lifecycle of a PlanetTerrainTile and verifies
    /// the visual mesh instance and vertex count are preserved after re-show.
    /// This should expose bugs where HideVisualMesh/ShowVisualMesh replace or
    /// destroy the cached mesh (which often manifests as a tiny triangle).
    /// </summary>
    public class PlanetTerrainTileHideShowTests
    {
        private GameObject testGameObject;
        private PlanetTerrainTile terrainTile;
        private TileData testTileData;
        private TileId testTileId;
        private Mesh testMesh;

        [SetUp]
        public void SetUp()
        {
            testGameObject = new GameObject("HideShowTestTile");
            terrainTile = testGameObject.AddComponent<PlanetTerrainTile>();

            testTileId = new TileId(0, 0, 0, 1);

            testMesh = new Mesh();
            testMesh.name = "HideShowTestMesh";
            var verts = new Vector3[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one };
            var tris = new int[] { 0, 1, 2, 1, 3, 2 };
            testMesh.vertices = verts;
            testMesh.triangles = tris;
            testMesh.RecalculateNormals();
            testMesh.RecalculateBounds();

            testTileData = new TileData();
            testTileData.mesh = testMesh;
            testTileData.center = Vector3.zero;
            testTileData.id = testTileId;
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null) Object.DestroyImmediate(testGameObject);
            if (testMesh != null) Object.DestroyImmediate(testMesh);
        }

        [Test]
        public void HideThenShow_ShouldRestoreOriginalMeshInstanceAndVertexCount()
        {
            // Initialize tile and ensure the visual mesh is assigned
            terrainTile.Initialize(testTileId, testTileData, _ => testMesh);

            Assert.IsNotNull(terrainTile.meshFilter, "MeshFilter must be present after Initialize");
            Assert.IsNotNull(terrainTile.meshFilter.sharedMesh, "MeshFilter should have a mesh assigned after Initialize");
            Assert.AreEqual(4, terrainTile.meshFilter.sharedMesh.vertexCount, "Initial mesh vertex count mismatch");

            var originalMesh = terrainTile.meshFilter.sharedMesh;

            // Ensure renderer is enabled and visible
            if (terrainTile.meshRenderer != null)
                terrainTile.meshRenderer.enabled = true;

            // Hide then show the visual mesh using the component API
            // (tests assume these methods exist on the component)
            terrainTile.HideVisualMesh();
            // After hiding the renderer may be disabled but the cached mesh should still be preserved or restorable
            terrainTile.ShowVisualMesh();

            Assert.IsNotNull(terrainTile.meshFilter.sharedMesh, "MeshFilter must have a mesh after ShowVisualMesh");
            Assert.AreEqual(4, terrainTile.meshFilter.sharedMesh.vertexCount, "Vertex count should be preserved after hide/show");

            // Prefer reference equality to detect accidental mesh replacement.
            // If implementation intentionally clones meshes, this assertion can be relaxed to structural equality.
            Assert.AreSame(originalMesh, terrainTile.meshFilter.sharedMesh, "ShowVisualMesh should restore the original mesh instance (no replacement/destruction)");
        }
    }
}
