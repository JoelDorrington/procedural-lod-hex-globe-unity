using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class TileFadeHelper : MonoBehaviour {}

    public class PlanetTileSpawner
    {
        // Spawns or updates a base tile GameObject
        public void SpawnOrUpdateTileGO(TileData td, Dictionary<TileId, GameObject> tileObjects, Material terrainMaterial, Transform parent)
        {
            if (tileObjects.TryGetValue(td.id, out var existing) && existing != null)
            {
                var mf = existing.GetComponent<MeshFilter>();
                if (mf.sharedMesh != td.mesh) mf.sharedMesh = td.mesh;
                var existingRenderer = existing.GetComponent<MeshRenderer>();
                if (existingRenderer != null && existingRenderer.sharedMaterial.HasProperty("_Color"))
                    existingRenderer.sharedMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
                return;
            }
            var go = new GameObject($"Tile_{td.id.face}_d{td.id.depth}_{td.id.x}_{td.id.y}");
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = td.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.material = new Material(terrainMaterial);
            if (renderer.material.HasProperty("_Color"))
                renderer.material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            go.AddComponent<TileFadeHelper>();
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
            if (childTileObjects.TryGetValue(td.id, out var existing) && existing != null)
            {
                var mf = existing.GetComponent<MeshFilter>();
                if (mf.sharedMesh != td.mesh) mf.sharedMesh = td.mesh;
                var existingRenderer = existing.GetComponent<MeshRenderer>();
                if (existingRenderer != null && existingRenderer.sharedMaterial.HasProperty("_Color"))
                    existingRenderer.sharedMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
                existing.SetActive(!invisible);
                return;
            }
            var go = new GameObject($"ChildTile_{td.id.face}_d{td.id.depth}_{td.id.x}_{td.id.y}");
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = td.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.material = new Material(terrainMaterial);
            if (renderer.material.HasProperty("_Color"))
                renderer.material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            go.AddComponent<TileFadeHelper>();
            go.SetActive(!invisible);
            childTileObjects[td.id] = go;
        }
    }
}
