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

        private bool ShouldSplit(bool isSplit, float distance)
        {
            return !isSplit && distance < SplitDistanceThreshold;
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
                if (ShouldSplit(isSplit, distance))
                {
                    SplitParent(parent);
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
                // Restore: do not reposition, let mesh builder/spawner set correct surface position
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
            // Gather child tile GameObjects
            var childGOs = new List<GameObject>();
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                {
                    var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
                    if (ChildTileObjects.TryGetValue(cid, out var childGO) && childGO != null)
                    {
                        childGOs.Add(childGO);
                        childGO.SetActive(true);
                    }
                }
            // Start parent fade out and child fade in simultaneously (true cross-fade)
            float duration = manager.SplitFadeDuration;
            float elapsed = 0f;
            Vector3 planetCenter = manager.PlanetSphereTransform.position;
            // Store original positions
            Vector3 parentOrig = TileObjects[parent.id].transform.position;
            List<Vector3> childOrigs = new List<Vector3>();
            foreach (var childGO in childGOs) childOrigs.Add(childGO.transform.position);
            // Animate movement vertically relative to ocean sphere
            yield return manager.StartCoroutine(SlideTilesVerticallyCoroutine(parent.id, childGOs, parentOrig, childOrigs, duration));
            // Reset positions
            if (TileObjects.TryGetValue(parent.id, out var parentGO3) && parentGO3 != null)
                parentGO3.transform.position = parentOrig;
            for (int i = 0; i < childGOs.Count; i++)
                childGOs[i].transform.position = childOrigs[i];
            // Deactivate parent after animation
            if (TileObjects.TryGetValue(parent.id, out var parentGO2) && parentGO2 != null)
                parentGO2.SetActive(false);
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
            var childGOs = new List<GameObject>();
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                {
                    var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
                    if (ChildTileObjects.TryGetValue(cid, out var go) && go != null)
                    {
                        childFadeCoroutines.Add(fadeAnimator.FadeOut(go, manager.SplitFadeDuration * 0.5f));
                        childIds.Add(cid);
                        childGOs.Add(go);
                    }
                }
            // Start parent fade in simultaneously with child fade out (true cross-fade)
            float duration = manager.SplitFadeDuration;
            float elapsed = 0f;
            Vector3 planetCenter = PlanetTransform.position;
            // Store original positions
            Vector3 parentOrig = TileObjects[parent.id].transform.position;
            List<Vector3> childOrigs = new List<Vector3>();
            foreach (var go in childGOs) childOrigs.Add(go.transform.position);
            // Animate movement vertically relative to ocean sphere
            yield return manager.StartCoroutine(SlideTilesVerticallyCoroutine(parent.id, childGOs, parentOrig, childOrigs, duration, true));
            // Reset parent position and ensure it's active
            if (TileObjects.TryGetValue(parent.id, out var parentGO2) && parentGO2 != null)
            {
                parentGO2.transform.position = parentOrig;
                parentGO2.SetActive(true);
            }
            for (int i = 0; i < childGOs.Count; i++)
                childGOs[i].transform.position = childOrigs[i];
            // Deactivate child tile GameObjects for reuse
            for (int i = 0; i < childGOs.Count; i++)
                childGOs[i].SetActive(false);

            // Remove child tile entries after merge animation completes
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                {
                    var cid = new TileId(parent.id.face, (byte)(parent.id.depth + 1), (ushort)(parent.id.x * 2 + cx), (ushort)(parent.id.y * 2 + cy));
                    ChildTiles.Remove(cid);
                    ChildTileObjects.Remove(cid);
                }
        }
        // Coroutine to slide parent and child tiles vertically relative to the ocean sphere
        private System.Collections.IEnumerator SlideTilesVerticallyCoroutine(TileId parentId, List<GameObject> childGOs, Vector3 parentOrig, List<Vector3> childOrigs, float duration, bool isMerge = false)
        {
            Debug.Log($"[TileAnim] SlideTilesVerticallyCoroutine started for parent {parentId}, duration={duration}");
            float elapsed = 0f;
            Vector3 planetCenter = manager.PlanetSphereTransform.position;
            Debug.Log($"[TileAnim] Initial parentOrig: {parentOrig}, planetCenter: {planetCenter}, parentRadial: {(parentOrig - planetCenter).normalized}");
            for (int i = 0; i < childOrigs.Count; i++)
                Debug.Log($"[TileAnim] Initial child {i} orig: {childOrigs[i]}, childRadial: {(childOrigs[i] - planetCenter).normalized}");
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float slideAmount = 0.5f * Mathf.Sin(Mathf.PI * t); // smooth ease
                // Parent movement (radial direction from original position)
                if (TileObjects.TryGetValue(parentId, out var parentGO) && parentGO != null)
                {
                    Vector3 parentRadial = parentOrig.normalized;
                    float parentDir = isMerge ? 1f : -1f;
                    parentGO.transform.position = parentOrig + parentRadial * parentDir * slideAmount;
                    Debug.Log($"[TileAnim] Parent {parentId} pos: {parentGO.transform.position} (slideAmount={slideAmount})");
                }
                // Children movement (radial direction from original position)
                for (int i = 0; i < childGOs.Count; i++)
                {
                    Vector3 childRadial = childOrigs[i].normalized;
                    float childDir = isMerge ? -1f : 1f;
                    childGOs[i].transform.position = childOrigs[i] + childRadial * childDir * slideAmount;
                    Debug.Log($"[TileAnim] Child {i} pos: {childGOs[i].transform.position} (slideAmount={slideAmount})");
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            Debug.Log($"[TileAnim] SlideTilesVerticallyCoroutine finished for parent {parentId}");
        }
    // ...existing code...
    }
}

