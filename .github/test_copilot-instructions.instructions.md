// C#
using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
  public class PlanetTerrainTileHideShowTests
  {
    private GameObject go;
    private PlanetTerrainTile tile;
    private Mesh testMesh;
    private TileData tileData;
    private TileId tileId;

    [SetUp]
    public void SetUp()
    {
      go = new GameObject("test_tile_hide_show");
      tile = go.AddComponent<PlanetTerrainTile>();

      // Create a simple quad-like mesh (4 verts, 2 tris)
      testMesh = new Mesh { name = "TestMesh_HideShow" };
      testMesh.vertices = new Vector3[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one };
      testMesh.triangles = new int[] { 0, 1, 2, 1, 3, 2 };
      testMesh.RecalculateNormals();
      testMesh.RecalculateBounds();

      tileId = new TileId(0, 0, 0, 0);
      tileData = new TileData { id = tileId, mesh = testMesh, center = Vector3.zero, resolution = 4, isBaked = true };
    }

    [TearDown]
    public void TearDown()
    {
      if (go != null) Object.DestroyImmediate(go);
      if (testMesh != null) Object.DestroyImmediate(testMesh);
    }

    [Test]
    public void HideThenShow_ShouldRestoreOriginalVisualMeshInstanceAndVertexCount()
    {
      // Initialize with collider mesh generator that returns the same mesh
      tile.Initialize(tileId, tileData, _ => testMesh);

      // Ensure initial assignment ok
      Assert.IsNotNull(tile.meshFilter, "MeshFilter should exist after Initialize");
      Assert.IsNotNull(tile.meshFilter.sharedMesh, "Initial visual mesh should be assigned");
      Assert.AreEqual(testMesh, tile.meshFilter.sharedMesh, "Initial sharedMesh should match test mesh");
      Assert.AreEqual(4, tile.meshFilter.sharedMesh.vertexCount, "Initial vertex count should be 4");

      // Ensure visual mesh is cached/generated and shown
      tile.GenerateAndCacheVisualMesh(_ => testMesh);
      tile.ShowVisualMesh();
      Assert.IsTrue(tile.meshRenderer.enabled, "Renderer should be enabled after ShowVisualMesh");
      Assert.IsNotNull(tile.meshFilter.sharedMesh, "Visual mesh should be present after ShowVisualMesh");
      Assert.AreEqual(4, tile.meshFilter.sharedMesh.vertexCount, "Vertex count after first show should be 4");

      // Hide visual mesh (bug likely happens here)
      tile.HideVisualMesh();
      Assert.IsFalse(tile.meshRenderer.enabled, "Renderer should be disabled after HideVisualMesh");

      // Show again and verify mesh restored correctly
      tile.ShowVisualMesh();
      Assert.IsTrue(tile.meshRenderer.enabled, "Renderer should be enabled after second ShowVisualMesh");
      Assert.IsNotNull(tile.meshFilter.sharedMesh, "Visual mesh should not be null after re-show");
      Assert.AreEqual(4, tile.meshFilter.sharedMesh.vertexCount, "Vertex count after re-show should be 4");
      Assert.AreEqual(testMesh, tile.meshFilter.sharedMesh, "Re-shown sharedMesh should be the original mesh instance");
    }
  }
}