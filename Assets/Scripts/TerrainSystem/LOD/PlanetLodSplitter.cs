using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public interface IPlanetTileTransition
    {
        void ApplyTransition(GameObject fromTile, GameObject toTile, float duration);
    }

    public class FadeTileTransition : IPlanetTileTransition
    {
        public void ApplyTransition(GameObject fromTile, GameObject toTile, float duration)
        {
            if (fromTile == null || toTile == null) return;
            // Find an always-active MonoBehaviour to start the coroutine
            var manager = GameObject.FindFirstObjectByType<PlanetLodManager>();
            if (manager != null)
                manager.StartCoroutine(FadeCoroutine(fromTile, toTile, duration));
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

    public System.Collections.IEnumerator FadeCoroutine(GameObject fromTile, GameObject toTile, float duration)
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
            // Set shared fade seed for stochastic fade
            float fadeSeed = Random.Range(0f, 10000f);
            if (fromMat.HasProperty("_FadeSeed")) fromMat.SetFloat("_FadeSeed", fadeSeed);
            if (toMat.HasProperty("_FadeSeed")) toMat.SetFloat("_FadeSeed", fadeSeed);
            float t = 0f;
            fromTile.SetActive(true);
            toTile.SetActive(true);
            float startTime = Time.unscaledTime;
            while (t < duration)
            {
                t = Time.unscaledTime - startTime;
                float outgoingAlpha = 1f - Mathf.Clamp01(t / duration);
                float incomingAlpha = Mathf.Clamp01(t / duration);
                // Animate both _Color.a and _Morph
                fromMat.SetColor("_Color", new Color(fromBaseColor.r, fromBaseColor.g, fromBaseColor.b, outgoingAlpha));
                toMat.SetColor("_Color", new Color(toBaseColor.r, toBaseColor.g, toBaseColor.b, incomingAlpha));
                if (fromMat.HasProperty("_Morph")) fromMat.SetFloat("_Morph", outgoingAlpha);
                if (toMat.HasProperty("_Morph")) toMat.SetFloat("_Morph", incomingAlpha);
                yield return new WaitForEndOfFrame();
            }
            fromMat.SetColor("_Color", new Color(fromBaseColor.r, fromBaseColor.g, fromBaseColor.b, 0f));
            toMat.SetColor("_Color", new Color(toBaseColor.r, toBaseColor.g, toBaseColor.b, 1f));
            if (fromMat.HasProperty("_Morph")) fromMat.SetFloat("_Morph", 0f);
            if (toMat.HasProperty("_Morph")) toMat.SetFloat("_Morph", 1f);
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
                var meshBuilder = new PlanetTileMeshBuilder(Config, Config.heightProvider, manager.OctaveWrapper, manager.HierarchicalAlignedSampling, manager.EnableEdgeConstraint, manager.BakedDepth, manager.SplitChildResolutionMultiplier, manager.ChildHeightEnhancement, manager.EdgePromotionRebuild);
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
                        tileTransition.ApplyTransition(TileObjects[parent.id], childGO, manager.SplitFadeDuration);
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
            var meshBuilder = new PlanetTileMeshBuilder(Config, Config.heightProvider, manager.OctaveWrapper, manager.HierarchicalAlignedSampling, manager.EnableEdgeConstraint, manager.BakedDepth, manager.SplitChildResolutionMultiplier, manager.ChildHeightEnhancement, manager.EdgePromotionRebuild);
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
                var meshBuilder = new PlanetTileMeshBuilder(Config, Config.heightProvider, manager.OctaveWrapper, manager.HierarchicalAlignedSampling, manager.EnableEdgeConstraint, manager.BakedDepth, manager.SplitChildResolutionMultiplier, manager.ChildHeightEnhancement, manager.EdgePromotionRebuild);
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
            if (parentGO != null)
            {
                parentGO.SetActive(true);
            }
            // Remove all child tiles and their GameObjects
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                {
                    var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
                    if (ChildTileObjects.TryGetValue(cid, out var go) && go != null)
                    {
                        // Start fade transition, then destroy after fade completes
                        manager.StartCoroutine(DestroyAfterFade(go, TileObjects[parent.id], manager.SplitFadeDuration, cid));
                    }
                    if (ChildTiles.ContainsKey(cid))
                    {
                        ChildTiles.Remove(cid);
                    }
                }
        }

        private System.Collections.IEnumerator DestroyAfterFade(GameObject fromTile, GameObject toTile, float duration, TileId cid)
        {
            // Use FadeTileTransition to run fade coroutine
            var transition = tileTransition as FadeTileTransition;
            if (transition != null)
                yield return transition.FadeCoroutine(fromTile, toTile, duration);
            fromTile.SetActive(false);
            if (Application.isPlaying) Object.Destroy(fromTile); else Object.DestroyImmediate(fromTile);
            ChildTileObjects.Remove(cid);
        }
    }
}
