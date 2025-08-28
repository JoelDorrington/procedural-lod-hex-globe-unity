using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class PlanetTileSpawner
    {
        // Spawns or updates a base tile GameObject
        public void SpawnOrUpdateTileGO(TileData td, Dictionary<TileId, GameObject> tileObjects, Material terrainMaterial, Transform parent)
        {
            if (tileObjects.TryGetValue(td.id, out var existing) && existing != null)
            {
                var mf = existing.GetComponent<MeshFilter>();
                if (mf.sharedMesh != td.mesh) mf.sharedMesh = td.mesh;
                return;
            }
            var go = new GameObject($"Tile_{td.id.face}_d{td.id.depth}_{td.id.x}_{td.id.y}");
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = td.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = terrainMaterial;
            tileObjects[td.id] = go;
        }

        // Clears all active base tile GameObjects
        public void ClearActiveTiles(Dictionary<TileId, GameObject> tileObjects)
        {
            foreach (var kv in tileObjects)
            {
                var go = kv.Value;
                if (go == null) continue;
                if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
            }
            tileObjects.Clear();
        }

        // Child tile spawning/updating routines
        public void SpawnOrUpdateChildTileGO(TileData td, Dictionary<TileId, GameObject> childTileObjects, Material terrainMaterial, Transform parent, bool invisible = false)
        {
            // ...existing child tile spawn logic goes here...
        }
    }
}
