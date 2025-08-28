using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class PlanetLodSplitter
    {
        // Cached manager properties
        private float splitThreshold;
        private float mergeThreshold;
        private int bakedDepth;
        private int splitTargetDepthOverride;
        private int configSplitTargetDepth;
        private Camera targetCamera;
        private Transform planetTransform;
        private Dictionary<TileId, TileData> tiles;
        private Dictionary<TileId, TileData> childTiles;
        private Dictionary<TileId, GameObject> childTileObjects;
        private Material terrainMaterial;
        private TerrainConfig config;

        public PlanetLodSplitter()
        {
            // Default constructor
        }
        public PlanetLodSplitter(PlanetLodManager manager)
        {
            splitThreshold = manager.splitAngleThreshold;
            mergeThreshold = manager.mergeAngleThreshold;
            bakedDepth = manager.BakedDepth;
            splitTargetDepthOverride = manager.SplitTargetDepthOverride;
            configSplitTargetDepth = manager.Config.splitTargetDepth;
            targetCamera = manager.TargetCamera;
            planetTransform = manager.transform;
            tiles = manager.Tiles;
            childTiles = manager.ChildTiles;
            childTileObjects = manager.ChildTileObjects;
            terrainMaterial = manager.TerrainMaterial;
            config = manager.Config;
        }

        public void SetParameters(PlanetLodManager manager)
        {
            splitThreshold = manager.splitAngleThreshold;
            mergeThreshold = manager.mergeAngleThreshold;
            bakedDepth = manager.BakedDepth;
            splitTargetDepthOverride = manager.SplitTargetDepthOverride;
            configSplitTargetDepth = manager.Config.splitTargetDepth;
            targetCamera = manager.TargetCamera;
            planetTransform = manager.transform;
            tiles = manager.Tiles;
            childTiles = manager.ChildTiles;
            childTileObjects = manager.ChildTileObjects;
            terrainMaterial = manager.TerrainMaterial;
            config = manager.Config;
        }

        private bool ShouldSplit(bool isSplit, float ang, int splitsStarted)
        {
            return !isSplit && ang < splitThreshold && splitsStarted < 2;
        }

        private bool ShouldMerge(bool isSplit, float ang)
        {
            return isSplit && ang > mergeThreshold;
        }

        public void UpdateProximitySplits()
        {
            if (targetCamera == null || bakedDepth < 0)
                return;
            int targetDepth = splitTargetDepthOverride >= 0 ? splitTargetDepthOverride : configSplitTargetDepth;
            if (targetDepth < 0 || targetDepth <= bakedDepth) return;
            Vector3 camPos = targetCamera.transform.position;
            Vector3 planetPos = planetTransform.position;
            Vector3 camDir = (camPos - planetPos).normalized;
            int splitsStarted = 0;
            foreach (var kv in tiles)
            {
                var parent = kv.Value;
                if (parent.id.depth != bakedDepth) continue;
                Vector3 dir = parent.center.sqrMagnitude > 0.0001f ? parent.center.normalized : Vector3.zero;
                if (dir == Vector3.zero) continue;
                float ang = Vector3.Angle(camDir, dir);
                bool isSplit = childTiles.ContainsKey(parent.id);
                if (ShouldSplit(isSplit, ang, splitsStarted))
                {
                    SplitParent(parent);
                    splitsStarted++;
                }
                else if (ShouldMerge(isSplit, ang))
                {
                    MergeParent(parent);
                }
            }
        }

        private void SpawnChildTile(TileData parent, int cx, int cy)
        {
            var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
            var td = new TileData
            {
                id = cid,
                resolution = config.baseResolution / (1 << (parent.id.depth + 1)),
                isBaked = true
            };
            // You may need to call a mesh builder here if not static
            childTiles[cid] = td;
            var spawner = new PlanetTileSpawner();
            spawner.SpawnOrUpdateChildTileGO(td, childTileObjects, terrainMaterial, planetTransform, invisible: false);
        }

        private void SplitParent(TileData parent)
        {
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                {
                    var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
                    if (!childTiles.ContainsKey(cid))
                        SpawnChildTile(parent, cx, cy);
                }
        }

        private void MergeParent(TileData parent)
        {
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                {
                    var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
                    if (childTileObjects.TryGetValue(cid, out var go) && go != null)
                    {
                        if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
                        childTileObjects.Remove(cid);
                        childTiles.Remove(cid);
                    }
                }
        }
    }
}
