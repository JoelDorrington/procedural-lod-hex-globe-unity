using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public interface IPlanetTileTransition
    {
        void ApplyTransition(GameObject fromTile, GameObject toTile, float duration, TileData parentTileData);
    }

    public class FadeTileTransition : IPlanetTileTransition
    {
        public void ApplyTransition(GameObject fromTile, GameObject toTile, float duration, TileData parentTileData)
        {
            if (fromTile == null || toTile == null) return;
            var manager = GameObject.FindFirstObjectByType<PlanetLodManager>();
            if (manager != null)
                manager.StartCoroutine(FadeOutThenInCoroutine(fromTile, toTile, duration, parentTileData));
        }

        public System.Collections.IEnumerator FadeOutThenInCoroutine(GameObject fromTile, GameObject toTile, float duration, TileData parentTileData)
        {
            // Fade out outgoing tile
            yield return FadeOutCoroutine(fromTile, duration * 0.5f);
            if (fromTile != null && fromTile.TryGetComponent<Transform>(out var tr) && tr.gameObject != null)
                fromTile.SetActive(false);
            // Fade in incoming tile
            toTile.SetActive(true);
            yield return FadeInCoroutine(toTile, duration * 0.5f);
        }

    public System.Collections.IEnumerator FadeOutCoroutine(GameObject tile, float duration)
        {
            var renderer = tile.GetComponent<Renderer>();
            if (renderer == null) yield break;
            Material mat = renderer.material;
            SetMaterialTransparent(mat);
            float t = 0f;
            float startTime = Time.unscaledTime;
            while (t < duration)
            {
                t = Time.unscaledTime - startTime;
                float fadeProgress = 1f - Mathf.Clamp01(t / duration);
                if (mat.HasProperty("_FadeProgress")) mat.SetFloat("_FadeProgress", fadeProgress);
                yield return new WaitForEndOfFrame();
            }
            if (mat.HasProperty("_FadeProgress")) mat.SetFloat("_FadeProgress", 0f);
        }

    public System.Collections.IEnumerator FadeInCoroutine(GameObject tile, float duration)
        {
            var renderer = tile.GetComponent<Renderer>();
            if (renderer == null) yield break;
            Material mat = renderer.material;
            SetMaterialTransparent(mat);
            float t = 0f;
            float startTime = Time.unscaledTime;
            while (t < duration)
            {
                t = Time.unscaledTime - startTime;
                float fadeProgress = Mathf.Clamp01(t / duration);
                if (mat.HasProperty("_FadeProgress")) mat.SetFloat("_FadeProgress", fadeProgress);
                yield return new WaitForEndOfFrame();
            }
            if (mat.HasProperty("_FadeProgress")) mat.SetFloat("_FadeProgress", 1f);
            SetMaterialOpaque(mat);
        }

        private void SetMaterialTransparent(Material mat)
        {
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

        private void SetMaterialOpaque(Material mat)
        {
            mat.SetFloat("_Mode", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
        }

        public System.Collections.IEnumerator FadeCoroutine(GameObject fromTile, GameObject toTile, float duration, TileData parentTileData)
        {
            var fromRenderer = fromTile.GetComponent<Renderer>();
            var toRenderer = toTile.GetComponent<Renderer>();
            if (fromRenderer == null || toRenderer == null) yield break;
            Material fromMat = fromRenderer.material;
            Material toMat = toRenderer.material;
            SetMaterialTransparent(fromMat);
            SetMaterialTransparent(toMat);
            Color fromBaseColor = fromMat.HasProperty("_Color") ? fromMat.GetColor("_Color") : Color.white;
            Color toBaseColor = toMat.HasProperty("_Color") ? toMat.GetColor("_Color") : Color.white;

            float t = 0f;
            fromTile.SetActive(true);
            toTile.SetActive(true);
            float startTime = Time.unscaledTime;
            Debug.Log($"Fade START: fromTile={fromTile.name}, toTile={toTile.name}, duration={duration}, parentId={parentTileData?.id}");
            float halfDuration = duration * 0.5f;
            // Phase 1: fade out outgoing tile to transparent, incoming stays transparent
            while (t < halfDuration)
            {
                t = Time.unscaledTime - startTime;
                float fadeProgress = Mathf.Clamp01(t / halfDuration);
                if (fromMat.HasProperty("_FadeProgress")) fromMat.SetFloat("_FadeProgress", 1f - fadeProgress);
                if (toMat.HasProperty("_FadeProgress")) toMat.SetFloat("_FadeProgress", 0f);
                // Blend outgoing tile color to transparent
                if (fromMat.HasProperty("_Color"))
                    fromMat.SetColor("_Color", new Color(fromBaseColor.r, fromBaseColor.g, fromBaseColor.b, 1f - fadeProgress));
                // Keep incoming tile color fully transparent
                if (toMat.HasProperty("_Color"))
                    toMat.SetColor("_Color", new Color(toBaseColor.r, toBaseColor.g, toBaseColor.b, 0f));
                yield return new WaitForEndOfFrame();
            }
            // Phase 2: fade in incoming tile from transparent to opaque, outgoing stays transparent
            float t2 = 0f;
            float phase2Start = Time.unscaledTime;
            while (t2 < halfDuration)
            {
                t2 = Time.unscaledTime - phase2Start;
                float fadeProgress = Mathf.Clamp01(t2 / halfDuration);
                if (fromMat.HasProperty("_FadeProgress")) fromMat.SetFloat("_FadeProgress", 0f);
                if (toMat.HasProperty("_FadeProgress")) toMat.SetFloat("_FadeProgress", fadeProgress);
                // Outgoing tile stays transparent
                if (fromMat.HasProperty("_Color"))
                    fromMat.SetColor("_Color", new Color(fromBaseColor.r, fromBaseColor.g, fromBaseColor.b, 0f));
                // Blend incoming tile color from transparent to opaque
                if (toMat.HasProperty("_Color"))
                    toMat.SetColor("_Color", new Color(toBaseColor.r, toBaseColor.g, toBaseColor.b, fadeProgress));
                yield return new WaitForEndOfFrame();
            }
            Debug.Log($"Fade END: fromTile={fromTile.name}, toTile={toTile.name}");
            if (fromMat.HasProperty("_FadeProgress")) fromMat.SetFloat("_FadeProgress", 0f);
            if (toMat.HasProperty("_FadeProgress")) toMat.SetFloat("_FadeProgress", 1f);
            // Finalize colors
            if (fromMat.HasProperty("_Color"))
                fromMat.SetColor("_Color", new Color(fromBaseColor.r, fromBaseColor.g, fromBaseColor.b, 0f));
            if (toMat.HasProperty("_Color"))
                toMat.SetColor("_Color", new Color(toBaseColor.r, toBaseColor.g, toBaseColor.b, 1f));
            fromTile.SetActive(false);
            SetMaterialOpaque(toMat);
        }
    }

    public class PlanetLodSplitter
    {
        private PlanetLodManager manager;
        private IPlanetTileTransition tileTransition;

        public PlanetLodSplitter(PlanetLodManager manager, IPlanetTileTransition transition = null)
        {
            this.manager = manager;
            this.tileTransition = transition ?? new FadeTileTransition();
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
            if (parentGO != null)
            {
                parentGO.SetActive(false);
            }
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                {
                    var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
                    if (!ChildTiles.ContainsKey(cid))
                        SpawnChildTile(parent, cx, cy);
                    // Always ensure child tile is visible
                    if (ChildTileObjects.TryGetValue(cid, out var childGO) && childGO != null)
                    {
                        childGO.SetActive(true);
                        tileTransition.ApplyTransition(TileObjects[parent.id], childGO, manager.SplitFadeDuration, parent);
                    }
                }
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
            spawner.SpawnOrUpdateChildTileGO(td, ChildTileObjects, TerrainMaterial, PlanetTransform, invisible: false);
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

        private System.Collections.IEnumerator FadeOutChildrenThenFadeInParent(TileData parent)
        {
            // Fade out all child tiles in parallel, then fade in parent tile
            var fadeOutCoroutines = new List<System.Collections.IEnumerator>();
            var childGOs = new List<GameObject>();
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                {
                    var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
                    if (ChildTileObjects.TryGetValue(cid, out var go) && go != null)
                    {
                        fadeOutCoroutines.Add((tileTransition as FadeTileTransition)?.FadeOutCoroutine(go, manager.SplitFadeDuration * 0.5f));
                        childGOs.Add(go);
                    }
                }
            // Run all fade out coroutines in parallel
            float fadeOutTime = manager.SplitFadeDuration * 0.5f;
            float startTime = Time.unscaledTime;
            while (Time.unscaledTime - startTime < fadeOutTime)
            {
                foreach (var co in fadeOutCoroutines)
                {
                    if (co != null && co.MoveNext()) { }
                }
                yield return new WaitForEndOfFrame();
            }
            // Deactivate and destroy child tiles
            foreach (var go in childGOs)
            {
                if (go != null)
                {
                    go.SetActive(false);
                    if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
                }
            }
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                {
                    var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
                    if (ChildTiles.ContainsKey(cid))
                        ChildTiles.Remove(cid);
                    if (ChildTileObjects.ContainsKey(cid))
                        ChildTileObjects.Remove(cid);
                }
            // Fade in parent tile
            if (TileObjects.TryGetValue(parent.id, out var parentGO) && parentGO != null)
            {
                parentGO.SetActive(true);
                yield return (tileTransition as FadeTileTransition)?.FadeInCoroutine(parentGO, manager.SplitFadeDuration * 0.5f);
            }
        }

        private System.Collections.IEnumerator DestroyAfterFade(GameObject fromTile, GameObject toTile, float duration, TileData parentTileData)
        {
            // Use FadeTileTransition to run sequential fade-out-then-fade-in coroutine
            var transition = tileTransition as FadeTileTransition;
            if (transition != null)
                yield return transition.FadeOutThenInCoroutine(fromTile, toTile, duration, parentTileData);
            if (fromTile != null && fromTile.TryGetComponent<Transform>(out var tr) && tr.gameObject != null)
                fromTile.SetActive(false);
            if (Application.isPlaying) Object.Destroy(fromTile); else Object.DestroyImmediate(fromTile);
            if (parentTileData != null && ChildTileObjects.ContainsKey(parentTileData.id))
                ChildTileObjects.Remove(parentTileData.id);
        }
    }
}

