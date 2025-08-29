using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class PlanetLodSplitter
    {
        private PlanetLodManager manager;
        // Track running fade coroutines for parent tiles
        private Dictionary<TileId, Coroutine> parentFadeCoroutines = new Dictionary<TileId, Coroutine>();

        public PlanetLodSplitter(PlanetLodManager manager)
        {
            this.manager = manager;
        }

        private float SplitDistanceThreshold => manager.splitDistanceThreshold;
        private float MergeDistanceThreshold => manager.mergeDistanceThreshold;
        private int BakedDepth => manager.BakedDepth;
        private int SplitTargetDepthOverride => manager.SplitTargetDepthOverride;
        private int ConfigSplitTargetDepth => manager.Config.splitTargetDepth;
        private Camera TargetCamera => manager.TargetCamera;
        private Transform PlanetTransform => manager.transform;
        private Dictionary<TileId, TileData> Tiles => manager.Tiles;
        private Dictionary<TileId, TileData> ChildTiles => manager.ChildTiles;
        private Dictionary<TileId, GameObject> ChildTileObjects => manager.ChildTileObjects;
        private Dictionary<TileId, GameObject> TileObjects => manager.TileObjects;
        private Material TerrainMaterial => manager.TerrainMaterial;
        private TerrainConfig Config => manager.Config;

        private bool ShouldSplit(bool isSplit, float distance, int splitsStarted)
        {
            return !isSplit && distance < SplitDistanceThreshold && splitsStarted < 2;
        }

        private bool ShouldMerge(bool isSplit, float distance)
        {
            // Returns true only if the tile is currently split and the camera is farther than the merge threshold
            // If merge is never triggered, check that isSplit is true for the correct tile and that mergeDistanceThreshold is set lower than the camera distance when zoomed out
            return isSplit && distance > MergeDistanceThreshold;
        }

        public void UpdateProximitySplits()
        {
            if (TargetCamera == null || BakedDepth < 0)
                return;
            int targetDepth = SplitTargetDepthOverride >= 0 ? SplitTargetDepthOverride : ConfigSplitTargetDepth;
            if (targetDepth < 0 || targetDepth <= BakedDepth) return;
            Vector3 camPos = TargetCamera.transform.position;
            int splitsStarted = 0;
            foreach (var kv in Tiles)
            {
                var parent = kv.Value;
                if (parent.id.depth != BakedDepth) continue;
                float distance = Vector3.Distance(camPos, parent.center);
                // isSplit is true if any child tiles exist for this parent
                bool isSplit = false;
                for (int cy = 0; cy < 2; cy++)
                    for (int cx = 0; cx < 2; cx++)
                    {
                        var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
                        if (ChildTiles.ContainsKey(cid))
                        {
                            isSplit = true;
                            break;
                        }
                    }
                if (ShouldSplit(isSplit, distance, splitsStarted))
                {
                    SplitParent(parent);
                    splitsStarted++;
                }
                else if (ShouldMerge(isSplit, distance))
                {
                    MergeParent(parent);
                }
            }
        }

        private void SplitParent(TileData parent)
        {
            // Ensure parent mesh is assigned
            if (parent.mesh == null)
            {
                var meshBuilder = new PlanetTileMeshBuilder(Config, Config.heightProvider, manager.OctaveWrapper, manager.BakedDepth, manager.SplitChildResolutionMultiplier, manager.ChildHeightEnhancement, manager.EdgePromotionRebuild);
                float dummyMin = 0, dummyMax = 0;
                meshBuilder.BuildTileMesh(parent, ref dummyMin, ref dummyMax);
            }
            if (!TileObjects.TryGetValue(parent.id, out var parentGO) || parentGO == null)
            {
                // Respawn parent tile GameObject if missing
                var spawner = new PlanetTileSpawner();
                spawner.SpawnOrUpdateTileGO(parent, TileObjects, TerrainMaterial, PlanetTransform);
                TileObjects.TryGetValue(parent.id, out parentGO);
            }
            // Spawn child tiles if missing
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                {
                    var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
                    if (!ChildTiles.ContainsKey(cid))
                        SpawnChildTile(parent, cx, cy);
                }
            // Start fade out parent, fade in children coroutine
            // Guard against duplicate fade coroutines for parent tiles
            if (parentFadeCoroutines.TryGetValue(parent.id, out var runningCo) && runningCo != null)
            {
                manager.StopCoroutine(runningCo);
            }
            var newCo = manager.StartCoroutine(FadeOutParentThenFadeInChildren(parent));
            parentFadeCoroutines[parent.id] = newCo;
            bool isRunning = parentFadeCoroutines.ContainsKey(parent.id) && parentFadeCoroutines[parent.id] != null;
            UnityEngine.Debug.Log($"[SplitParent] Parent {parent.id}: Coroutine running = {isRunning}");
        }

        private void SpawnChildTile(TileData parent, int cx, int cy)
        {
            var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
            var td = new TileData
            {
                id = cid,
                resolution = Mathf.RoundToInt(Config.baseResolution / (1 << (parent.id.depth + 1)) * manager.SplitChildResolutionMultiplier),
                isBaked = true
            };
            // Generate mesh for child tile
            var meshBuilder = new PlanetTileMeshBuilder(Config, Config.heightProvider, manager.OctaveWrapper, manager.BakedDepth, manager.SplitChildResolutionMultiplier, manager.ChildHeightEnhancement, manager.EdgePromotionRebuild);
            float dummyMin = 0, dummyMax = 0;
            meshBuilder.BuildTileMesh(td, ref dummyMin, ref dummyMax);
            ChildTiles[cid] = td;
            var spawner = new PlanetTileSpawner();
            spawner.SpawnOrUpdateChildTileGO(td, ChildTileObjects, TerrainMaterial, PlanetTransform);
            if (ChildTileObjects.TryGetValue(cid, out var go) && go != null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = renderer.material;
                    mat.SetFloat("_Mode", 3);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }
            }
        }

        private void MergeParent(TileData parent)
        {
            // Ensure parent mesh is assigned
            if (parent.mesh == null)
            {
                var meshBuilder = new PlanetTileMeshBuilder(Config, Config.heightProvider, manager.OctaveWrapper, manager.BakedDepth, manager.SplitChildResolutionMultiplier, manager.ChildHeightEnhancement, manager.EdgePromotionRebuild);
                float dummyMin = 0, dummyMax = 0;
                meshBuilder.BuildTileMesh(parent, ref dummyMin, ref dummyMax);
            }
            // Show parent tile GameObject (use base tile dictionary)
            if (!TileObjects.TryGetValue(parent.id, out var parentGO) || parentGO == null)
            {
                // Respawn parent tile GameObject if missing
                var spawner = new PlanetTileSpawner();
                spawner.SpawnOrUpdateTileGO(parent, TileObjects, TerrainMaterial, PlanetTransform);
                TileObjects.TryGetValue(parent.id, out parentGO);
            }
            // Start independent merge coroutine for this parent
            manager.StartCoroutine(FadeOutChildrenThenFadeInParent(parent));
        }

        // Coroutine: Fade out parent, then fade in children
        private System.Collections.IEnumerator FadeOutParentThenFadeInChildren(TileData parent)
        {
            // Log the number of running coroutines for this parent tile
            bool isRunning = parentFadeCoroutines.ContainsKey(parent.id) && parentFadeCoroutines[parent.id] != null;
            UnityEngine.Debug.Log($"[FadeOutParentThenFadeInChildren] Parent {parent.id}: Coroutine running = {isRunning}");
            var fadeAnimator = manager.GetComponent<TileFadeAnimator>();
            if (fadeAnimator == null)
                fadeAnimator = manager.gameObject.AddComponent<TileFadeAnimator>();
            // Fade out parent tile
            if (TileObjects.TryGetValue(parent.id, out var parentGO) && parentGO != null)
            {
                yield return fadeAnimator.FadeOut(parentGO, manager.SplitFadeDuration * 0.5f);
                parentGO.SetActive(false);
            }
            // Fade in children simultaneously
            var childCoroutines = new List<Coroutine>();
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                {
                    var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
                    if (ChildTileObjects.TryGetValue(cid, out var childGO) && childGO != null)
                    {
                        childGO.SetActive(true);
                        childCoroutines.Add(fadeAnimator.FadeIn(childGO, manager.SplitFadeDuration * 0.5f));
                    }
                }
            foreach (var co in childCoroutines)
            {
                yield return co;
            }
            // Remove coroutine tracking when done
            if (parentFadeCoroutines.ContainsKey(parent.id))
                parentFadeCoroutines[parent.id] = null;
    }

        private System.Collections.IEnumerator FadeOutChildrenThenFadeInParent(TileData parent)
        {
            // Use TileFadeAnimator for child fade-out and parent fade-in
            var fadeAnimator = manager.GetComponent<TileFadeAnimator>();
            if (fadeAnimator == null)
                fadeAnimator = manager.gameObject.AddComponent<TileFadeAnimator>();
            var childFadeCoroutines = new List<Coroutine>();
            var childIds = new List<TileId>();
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                {
                    var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
                    if (ChildTileObjects.TryGetValue(cid, out var go) && go != null)
                    {
                        childFadeCoroutines.Add(fadeAnimator.FadeOut(go, manager.SplitFadeDuration * 0.5f));
                        childIds.Add(cid);
                    }
                }
            // Wait for all child fades to complete
            foreach (var co in childFadeCoroutines)
            {
                yield return co;
            }
            // Remove child tile data
            foreach (var cid in childIds)
            {
                if (ChildTiles.ContainsKey(cid)) ChildTiles.Remove(cid);
                if (ChildTileObjects.ContainsKey(cid)) ChildTileObjects.Remove(cid);
            }
            // Fade in parent tile
            if (TileObjects.TryGetValue(parent.id, out var parentGO) && parentGO != null)
            {
                parentGO.SetActive(true);
                yield return fadeAnimator.FadeIn(parentGO, manager.SplitFadeDuration * 0.5f);
            }
        }

    }
}

