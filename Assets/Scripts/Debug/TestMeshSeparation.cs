using UnityEngine;
using UnityEditor;
using HexGlobeProject.TerrainSystem.LOD;

public class TestMeshSeparation : MonoBehaviour
{
    [MenuItem("Debug/Test Mesh Separation")]
    public static void TestSeparateMeshes()
    {
        // Find the PlanetTileVisibilityManager in the scene
        var manager = FindFirstObjectByType<PlanetTileVisibilityManager>();
        if (manager == null)
        {
            Debug.LogError("No PlanetTileVisibilityManager found in scene");
            return;
        }

        // Spawn a test tile
        var testTileId = new TileId(0, 0, 0, 1);
        var tileGO = manager.TrySpawnTile(testTileId, 32);
        
        if (tileGO != null)
        {
            var terrainTile = tileGO.GetComponent<PlanetTerrainTile>();
            if (terrainTile != null)
            {
                var visualMesh = terrainTile.meshFilter?.sharedMesh;
                var colliderMesh = terrainTile.meshCollider?.sharedMesh;
                
                
                if (visualMesh != null && colliderMesh != null && visualMesh != colliderMesh)
                {
                }
                else
                {
                    Debug.LogWarning("PROBLEM: Visual and collider meshes are the same reference");
                }
            }
        }
    }
}
