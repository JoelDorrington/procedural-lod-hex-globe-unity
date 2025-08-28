using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HexGlobeProject.TerrainSystem; // shared utilities
using HexGlobeProject.TerrainSystem.LOD; // TileFade

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Simplified single-depth planet terrain manager.
    /// Bakes and displays a single base depth (config.bakeDepth) tiles, with optional proximity split
    /// to a deeper target (config.splitTargetDepth).
    /// </summary>
    public class PlanetLodManager : MonoBehaviour
    {
        [SerializeField] private TerrainConfig config;
        [SerializeField] private TerrainHeightProviderBase heightProvider;
        [SerializeField] private Camera targetCamera;
    [SerializeField] private Material terrainMaterial;

    [Header("Bake / Debug")]
    [Tooltip("Automatically bake on Start")] [SerializeField] private bool autoBakeOnStart = true;
    [Tooltip("Draw tile wireframes with Gizmos")] [SerializeField] private bool gizmoWireTiles = true;
    [Tooltip("Show height range spheres (min/max)")] [SerializeField] private bool gizmoHeightRanges = false;
    [Tooltip("Log bake progress & height ranges")] [SerializeField] private bool debugLogBake = false;
    [Tooltip("Tint tiles (only one depth) for debugging")] [SerializeField] private bool debugTint = false;

    // Dynamic high-res patch removed per user request; keeping placeholder comment for clarity.

    [Header("Edge Continuity (Seam Step A)")]
    [Tooltip("Constrain child tile outer ring to parent-level sampling.")]
    [SerializeField] private bool enableEdgeConstraint = true;
    [Tooltip("Reserved for future blend band width (rings inward from edge).")]
    [SerializeField] private int childEdgeBlendRings = 3;
    [Tooltip("Enable Step B: interior blend band that gradually introduces higher-octave/full detail moving inward from constrained edge.")]
    [SerializeField] private bool enableInteriorBlendBand = true;
    [Tooltip("If true, child tiles exactly preserve parent-sampled vertices (alignment) and only insert new in-between samples for added detail.")]
    [SerializeField] private bool hierarchicalAlignedSampling = true;
    [Tooltip("After parent fades out, rebuild first-split child tiles to promote constrained edge vertices to full detail (restores shoreline/mountain detail).")]
    [SerializeField] private bool promoteConstrainedEdgesPostFade = true;
    public enum RefinementRatio { Double = 0, OnePointFive = 1 }
    [Tooltip("Hierarchical refinement growth factor. Double = (N-1)*2+1, OnePointFive = (N-1)*1.5+1 when divisible (falls back to Double if (N-1) is odd).")]
    [SerializeField] private RefinementRatio refinementRatio = RefinementRatio.Double;


    // Performance caps removed.


    [Header("Proximity Split LOD")]
    [Tooltip("Enable smooth splitting of baked tiles into higher-depth tiles near camera.")] [SerializeField] private bool enableProximitySplit = true;
    [Tooltip("Override split target depth (defaults to config.splitTargetDepth if <0)."), SerializeField] private int splitTargetDepthOverride = -1;
    [Tooltip("Enter split when angle to tile center < tileAngularSpan * this factor.")] [Range(0.1f,1.5f)] [SerializeField] private float splitEnterFactor = 0.6f;
    [Tooltip("Exit split when angle exceeds tileAngularSpan * this factor (should be > enter factor)."), Range(0.1f,2f)] [SerializeField] private float splitExitFactor = 1.1f;
    [Tooltip("Seconds for cross-fade between parent and children.")] [SerializeField] private float splitFadeDuration = 0.35f;
    [Tooltip("Max parent splits (new child sets) started per frame to cap cost.")] [SerializeField] private int splitMaxPerFrame = 2;
    [Tooltip("Destroy child tiles when far to reclaim memory.")] [SerializeField] private bool destroyChildrenOnMerge = true;
    [Tooltip("Debug: draw active split child tile wireframes.")] [SerializeField] private bool debugSplitChildrenGizmo = false;
    [Tooltip("Disable splitting when camera distance (zoom) exceeds this. -1 = never disable by distance.")] [SerializeField] private float splitDisableBeyondDistance = 40f;
    [Tooltip("Multiplier applied to the resolved resolution for split child tiles (>=1 for higher detail)."), SerializeField] private float splitChildResolutionMultiplier = 1f;
    [Tooltip("Fraction of fade duration to delay parent fade once children begin (0=simultaneous)."), SerializeField] private float splitParentFadeDelayFrac = 0.35f;
    [Tooltip("Fade curve for child tiles.")] [SerializeField] private TileFade.FadeCurve splitChildFadeCurve = TileFade.FadeCurve.EaseOut;
    [Tooltip("Fade curve for parent tiles.")] [SerializeField] private TileFade.FadeCurve splitParentFadeCurve = TileFade.FadeCurve.EaseIn;

    // Split LOD state
    private readonly Dictionary<TileId, List<TileId>> _parentToChildren = new();
    private readonly HashSet<TileId> _activeSplitParents = new();
    private readonly Dictionary<TileId, Coroutine> _parentSplitCoroutines = new();
    private readonly Dictionary<TileId, TileData> _childTiles = new(); // keyed by child id
    private readonly Dictionary<TileId, GameObject> _childTileObjects = new();
    private int _bakedTileResolution = -1; // cached baked tile resolution for consistent child detail
    // Internal flag used during edge promotion rebuild pass to disable perimeter parent constraint.
    private bool _edgePromotionRebuild = false;

        [Header("Runtime State (read-only)")] public bool bakeInProgress;        
        public float bakeProgress; // 0..1 across current phase
        public int bakedTileCount;
    public int bakedDepth = -1; // single depth baked
    [SerializeField, Tooltip("Approx active vertex count of enabled tile meshes.")] private int activeVertexCount;
    [SerializeField, Tooltip("Approx active triangle count of enabled tile meshes.")] private int activeTriangleCount;
    // Split debug instrumentation removed with performance caps.
    private readonly Dictionary<TileId, TileData> _tiles = new();
    private readonly Dictionary<TileId, GameObject> _tileObjects = new();
    // (Removed dynamic patch state)

        // Temporary list to avoid GC during generation
        private readonly List<Vector3> _verts = new();
        private readonly List<int> _tris = new();
        private readonly List<Vector3> _normals = new();
    // (Removed refinement state)

        // Public entry point
        [ContextMenu("Bake Base Depth")]
        public void BakeBaseDepthContextMenu()
        {
            if (bakeInProgress) return;
            StartCoroutine(BakeBaseDepth());
        }

        public IEnumerator BakeBaseDepth()
        {
            EnsureHeightProvider();
            bakeInProgress = true;
            bakeProgress = 0f;
            bakedTileCount = 0;
            _tiles.Clear();
            ClearActiveTiles();
            int depth = Mathf.Max(0, config.bakeDepth);
            PrepareOctaveMaskForDepth(depth);
            yield return StartCoroutine(BakeDepthSingle(depth));
            bakedDepth = depth;
            SpawnAllTiles();
            bakeInProgress = false;
            bakeProgress = 1f;
            if (debugLogBake)
                Debug.Log($"[PlanetLod] Baked base depth {depth} tiles={_tiles.Count}");
        }

    // Removed full-depth pre-bake helper

        private IEnumerator BakeDepthSingle(int depth)
        {
            // Depth means quadtree depth per face. depth=0 means single tile (the whole face).
            int tilesPerEdge = 1 << depth; // 2^depth
            int totalTilesThisDepth = 6 * tilesPerEdge * tilesPerEdge; // 6 cube faces
            var levelDict = _tiles; // reuse single dictionary
            levelDict.Clear();

            int resolution = ResolveResolutionForDepth(depth, tilesPerEdge);

            int processed = 0;
            float globalMin = float.MaxValue;
            float globalMax = float.MinValue;
            float rawMin = float.MaxValue; // pre-remap, pre-scale clamp values after heightScale but before remap clamp
            float rawMax = float.MinValue;
            for (int face = 0; face < 6; face++)
            {
                for (int y = 0; y < tilesPerEdge; y++)
                {
                    for (int x = 0; x < tilesPerEdge; x++)
                    {
                        var id = new TileId((byte)face, (byte)depth, (ushort)x, (ushort)y);
                        var data = new TileData { id = id, resolution = resolution, isBaked = true };
                        BuildTileMesh(data, ref rawMin, ref rawMax);
                        levelDict[id] = data;
                        if (data.minHeight < globalMin) globalMin = data.minHeight;
                        if (data.maxHeight > globalMax) globalMax = data.maxHeight;
                        bakedTileCount++;
                        processed++;
                        bakeProgress = processed / (float)totalTilesThisDepth;
                        if ((processed & 7) == 0) // yield every 8 tiles
                            yield return null;
                    }
                }
            }
            if (debugLogBake)
                Debug.Log($"[PlanetLod] Depth {depth} res={resolution} tiles={totalTilesThisDepth} rawRange=({rawMin:F3},{rawMax:F3}) remapRange=({globalMin:F3},{globalMax:F3}) span={(globalMax - globalMin):F3}");
        }

    private PlanetTileMeshBuilder meshBuilder;
    private OctaveMaskHeightProvider _octaveWrapper;
    [SerializeField] private float childHeightEnhancement = 1.0f;

    private void Awake()
    {
        meshBuilder = new PlanetTileMeshBuilder(
            config,
            heightProvider,
            _octaveWrapper,
            hierarchicalAlignedSampling,
            enableEdgeConstraint,
            bakedDepth,
            splitChildResolutionMultiplier,
            childHeightEnhancement,
            _edgePromotionRebuild
        );
    }

    // Instead of a persistent meshBuilder, create a new one for each mesh build to ensure up-to-date config and runtime values
    private void BuildTileMesh(TileData data, ref float rawMin, ref float rawMax)
    {
        var meshBuilder = new PlanetTileMeshBuilder(
            config,
            heightProvider,
            _octaveWrapper,
            hierarchicalAlignedSampling,
            enableEdgeConstraint,
            bakedDepth,
            splitChildResolutionMultiplier,
            childHeightEnhancement,
            _edgePromotionRebuild
        );
        meshBuilder.BuildTileMesh(data, ref rawMin, ref rawMax);
    }

    private void BuildTileMesh(TileData data)
    {
        float rmin = float.MaxValue; float rmax = float.MinValue;
        BuildTileMesh(data, ref rmin, ref rmax);
    }

        private void PrepareOctaveMaskForDepth(int depth)
        {
            int maxOct = (depth == config.bakeDepth) ? config.maxOctaveBake : config.maxOctaveSplit;
            if (maxOct == -1)
            {
                // restore original heightProvider if wrapper was used previously
                if (_octaveWrapper != null && heightProvider == _octaveWrapper && _octaveWrapper.inner != null)
                    heightProvider = _octaveWrapper.inner;
                return;
            }
            if (_octaveWrapper == null)
            {
                _octaveWrapper = new OctaveMaskHeightProvider();
            }
            if (heightProvider is OctaveMaskHeightProvider existingWrap)
            {
                existingWrap.maxOctave = maxOct;
            }
            else
            {
                _octaveWrapper.inner = heightProvider;
                _octaveWrapper.maxOctave = maxOct;
                heightProvider = _octaveWrapper;
            }
        }

        private int ResolveResolutionForDepth(int depth, int tilesPerEdge)
        {
            // Depth 'd' splits face into (2^d)^2 tiles; derive resolution to keep roughly constant density.
            int baseRes = config.baseResolution;
            return Mathf.Max(2, baseRes / tilesPerEdge);
        }

    // (Removed CubeFaceUvToDir; replaced by CubeSphere.FaceLocalToUnit)

        private void SpawnAllTiles()
        {
            ClearActiveTiles();
            foreach (var kv in _tiles)
            {
                var td = kv.Value;
                if (td.mesh == null) continue;
                SpawnOrUpdateTileGO(td);
            }
            TerrainShaderGlobals.Apply(config, terrainMaterial);
            RecomputeActiveCounts();
        }

        private void ClearActiveTiles()
        {
            foreach (var kv in _tileObjects)
            {
                var go = kv.Value;
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
            _tileObjects.Clear();
        }

    // (Removed PushShaderGlobals; using TerrainShaderGlobals.Apply)

        private void Update()
        {
            if (autoBakeOnStart && !bakeInProgress && bakedDepth < 0)
            {
                autoBakeOnStart = false;
                StartCoroutine(BakeBaseDepth());
            }
            else if (bakedDepth < 0)
            {
                EnsureHeightProvider(false);
            }
            // (Dynamic patch removed)
            if (enableProximitySplit && bakedDepth >= 0 && targetCamera != null)
            {
                float camDist = Vector3.Distance(targetCamera.transform.position, transform.position);
                if (splitDisableBeyondDistance > 0f && camDist > splitDisableBeyondDistance)
                {
                    // Merge any active splits if distance now beyond threshold
                    if (_activeSplitParents.Count > 0)
                        MergeAllActiveSplits();
                }
                else
                {
                    UpdateProximitySplits();
                }
            }
        }

        private bool _warnedMissingProvider;
        private void EnsureHeightProvider(bool logOnBind = true)
        {
            if (heightProvider == null && config != null && config.heightProvider != null)
            {
                heightProvider = config.heightProvider;
                if (logOnBind && debugLogBake)
                    Debug.Log("[PlanetLod] Bound heightProvider from TerrainConfig: " + config.heightProvider.GetType().Name);
            }
            if (heightProvider == null && !_warnedMissingProvider)
            {
                _warnedMissingProvider = true;
                Debug.LogWarning("[PlanetLod] No heightProvider assigned (and none in config). Terrain will be flat.");
            }
        }

    // (Removed selection & refinement data structures)

        private void SpawnOrUpdateTileGO(TileData td)
        {
            if (_tileObjects.TryGetValue(td.id, out var existing) && existing != null)
            {
                var mf = existing.GetComponent<MeshFilter>();
                if (mf.sharedMesh != td.mesh) mf.sharedMesh = td.mesh;
        if (debugTint)
                {
                    var r = existing.GetComponent<MeshRenderer>();
                    if (r != null)
                    {
            Color c = Color.Lerp(Color.white, Color.red, 0.5f);
                        r.sharedMaterial.SetColor("_ColorHigh", c);
                    }
                }
                return;
            }
            var go = new GameObject($"Tile_{td.id.face}_d{td.id.depth}_{td.id.x}_{td.id.y}");
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = td.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = terrainMaterial;
        if (debugTint)
            {
        Color c = Color.Lerp(Color.white, Color.red, 0.5f);
                renderer.sharedMaterial = new Material(terrainMaterial);
                renderer.sharedMaterial.SetColor("_ColorHigh", c);
            }
            _tileObjects[td.id] = go;
        }

    // (Removed fade & cross-fade logic)

        // Optional gizmos for debugging baked tiles
        private void OnDrawGizmosSelected()
        {
            if (!gizmoWireTiles && !gizmoHeightRanges) return;
            foreach (var kv in _tiles)
            {
                var data = kv.Value;
                if (data.mesh == null) continue;
                if (gizmoWireTiles)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireMesh(data.mesh);
                }
                if (gizmoHeightRanges)
                {
                    float rMin = config.baseRadius + data.minHeight;
                    float rMax = config.baseRadius + data.maxHeight;
                    Vector3 center = Vector3.zero;
                    Gizmos.color = new Color(1,1,0,0.1f);
                    Gizmos.DrawWireSphere(center, rMin);
                    Gizmos.color = new Color(1,0,0,0.1f);
                    Gizmos.DrawWireSphere(center, rMax);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (debugSplitChildrenGizmo)
            {
                Gizmos.color = Color.magenta;
                foreach (var kv in _parentToChildren)
                {
                    foreach (var cid in kv.Value)
                    {
                        if (_childTileObjects.TryGetValue(cid, out var go) && go != null)
                        {
                            var mf = go.GetComponent<MeshFilter>();
                            if (mf != null && mf.sharedMesh != null)
                                Gizmos.DrawWireMesh(mf.sharedMesh, go.transform.position, go.transform.rotation, go.transform.lossyScale);
                        }
                    }
                }
            }
        }


        // ===== Proximity Split Implementation =====
        private void UpdateProximitySplits()
        {
            int targetDepth = splitTargetDepthOverride >= 0 ? splitTargetDepthOverride : config.splitTargetDepth;
            if (targetDepth < 0 || targetDepth <= bakedDepth) return; // need deeper depth available
            // distance gating handled in Update
            float tileSpanDeg = 90f / (1 << bakedDepth); // each medium tile angular span (approx per face)
            Vector3 camDir = (targetCamera.transform.position - transform.position).normalized;

            int splitsStarted = 0;
            // Evaluate each medium tile for enter/exit
            var toUnspliList = new List<TileId>();
            foreach (var kv in _tiles)
            {
                var parent = kv.Value;
                if (parent.id.depth != bakedDepth) continue;
                // Direction to tile center (normalize center)
                Vector3 dir = parent.center.sqrMagnitude > 0.0001f ? parent.center.normalized : Vector3.zero;
                if (dir == Vector3.zero) continue;
                float ang = Vector3.Angle(camDir, dir);
                bool isSplit = _activeSplitParents.Contains(parent.id);
                if (!isSplit)
                {
                    if (ang < tileSpanDeg * splitEnterFactor && splitsStarted < splitMaxPerFrame)
                    {
                        if (CanAffordSplit(parent))
                        {
                            StartParentSplit(parent.id, targetDepth);
                            splitsStarted++;
                        }
                    }
                }
                else
                {
                    if (ang > tileSpanDeg * splitExitFactor)
                    {
                        toUnspliList.Add(parent.id);
                    }
                }
            }
            foreach (var pid in toUnspliList)
            {
                StartParentMerge(pid);
            }
        }

        private void StartParentSplit(TileId parentId, int targetDepth)
        {
            if (_parentSplitCoroutines.ContainsKey(parentId)) return; // already transitioning
            var co = StartCoroutine(CoSplitParent(parentId, targetDepth));
            _parentSplitCoroutines[parentId] = co;
        }
        private void StartParentMerge(TileId parentId)
        {
            if (_parentSplitCoroutines.ContainsKey(parentId)) return;
            var co = StartCoroutine(CoMergeParent(parentId));
            _parentSplitCoroutines[parentId] = co;
        }

        private IEnumerator CoSplitParent(TileId parentId, int targetDepth)
        {
            if (!_tiles.TryGetValue(parentId, out var parentData)) { _parentSplitCoroutines.Remove(parentId); yield break; }
            if (_bakedTileResolution <= 0) _bakedTileResolution = parentData.resolution; // record once
            // Build children (parent depth +1 each level until targetDepth)
            int currentDepth = parentId.depth;
            var frontier = new List<TileId> { parentId };
            while (currentDepth < targetDepth)
            {
                // Option A: Update octave cap/wrapper for the depth we're about to generate (children at currentDepth+1)
                PrepareOctaveMaskForDepth(currentDepth + 1);
                var next = new List<TileId>();
                foreach (var pid in frontier)
                {
                    int d = pid.depth + 1;
                    for (int cy = 0; cy < 2; cy++)
                    {
                        for (int cx = 0; cx < 2; cx++)
                        {
                            var cid = new TileId(pid.face, (byte)d, (ushort)(pid.x * 2 + cx), (ushort)(pid.y * 2 + cy));
                            if (!_childTiles.ContainsKey(cid))
                            {
                                var td = new TileData { id = cid, resolution = GetSplitChildResolution(d), isBaked = true };
                                BuildTileMesh(td); // uses current octave cap (same as parent)
                                _childTiles[cid] = td;
                                SpawnOrUpdateChildGO(td, invisible:true);
                            }
                            next.Add(cid);
                        }
                    }
                }
                frontier = next;
                currentDepth++;
                // yield to avoid spike
                yield return null;
            }
            _parentToChildren[parentId] = frontier; // leaves contain target depth tiles

            // Fade in children, fade out parent
            foreach (var cid in frontier)
            {
                if (_childTileObjects.TryGetValue(cid, out var go) && go != null)
                {
                    var mr = go.GetComponent<MeshRenderer>(); mr.enabled = true;
                    var fade = go.GetComponent<TileFade>(); if (fade == null) fade = go.AddComponent<TileFade>();
                    fade.Begin(true, splitFadeDuration, 0f, splitChildFadeCurve);
                }
            }
            if (_tileObjects.TryGetValue(parentId, out var parentGO) && parentGO != null)
            {
                var fade = parentGO.GetComponent<TileFade>(); if (fade == null) fade = parentGO.AddComponent<TileFade>();
                float delay = Mathf.Clamp01(splitParentFadeDelayFrac) * splitFadeDuration;
                fade.Begin(false, splitFadeDuration, delay, splitParentFadeCurve);
            }
            _activeSplitParents.Add(parentId);
            yield return new WaitForSeconds(splitFadeDuration);
            // Disable parent renderer once children visible
            if (_tileObjects.TryGetValue(parentId, out var parentGO2) && parentGO2 != null)
            {
                var mr = parentGO2.GetComponent<MeshRenderer>(); if (mr != null) mr.enabled = false;
            }
            _parentSplitCoroutines.Remove(parentId);
            // Optional: promote constrained perimeter edge vertices to full detail after parent fully hidden.
            if (promoteConstrainedEdgesPostFade && hierarchicalAlignedSampling)
            {
                _edgePromotionRebuild = true;
                foreach (var cid in frontier)
                {
                    if (_childTiles.TryGetValue(cid, out var td))
                    {
                        float dMin = 0f, dMax = 0f;
                        BuildTileMesh(td, ref dMin, ref dMax);
                        SpawnOrUpdateChildGO(td);
                    }
                }
                _edgePromotionRebuild = false;
            }
        }

        private IEnumerator CoMergeParent(TileId parentId)
        {
            if (!_parentToChildren.TryGetValue(parentId, out var children)) { _parentSplitCoroutines.Remove(parentId); yield break; }
            // Enable parent renderer and fade in
            if (_tileObjects.TryGetValue(parentId, out var parentGO) && parentGO != null)
            {
                var mr = parentGO.GetComponent<MeshRenderer>(); if (mr != null) mr.enabled = true;
                var fade = parentGO.GetComponent<TileFade>(); if (fade == null) fade = parentGO.AddComponent<TileFade>();
                fade.Begin(true, splitFadeDuration, 0f, splitParentFadeCurve);
            }
            // Fade out children
            foreach (var cid in children)
            {
                if (_childTileObjects.TryGetValue(cid, out var go) && go != null)
                {
                    var fade = go.GetComponent<TileFade>(); if (fade == null) fade = go.AddComponent<TileFade>();
                    float delay = Mathf.Clamp01(splitParentFadeDelayFrac) * 0.25f * splitFadeDuration; // slight stagger for child fade out
                    fade.Begin(false, splitFadeDuration, delay, splitChildFadeCurve);
                }
            }
            yield return new WaitForSeconds(splitFadeDuration);
            // After fade, disable or destroy children
            foreach (var cid in children)
            {
                if (_childTileObjects.TryGetValue(cid, out var go) && go != null)
                {
                    var mr = go.GetComponent<MeshRenderer>(); if (mr != null) mr.enabled = false;
                    if (destroyChildrenOnMerge)
                    {
                        if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
                        _childTileObjects.Remove(cid); _childTiles.Remove(cid);
                    }
                }
            }
            _parentToChildren.Remove(parentId);
            _activeSplitParents.Remove(parentId);
            _parentSplitCoroutines.Remove(parentId);
        }

        private void MergeAllActiveSplits()
        {
            var list = new List<TileId>(_activeSplitParents);
            foreach (var pid in list)
            {
                StartParentMerge(pid);
            }
        }

        private void SpawnOrUpdateChildGO(TileData td, bool invisible=false)
        {
            if (_childTileObjects.TryGetValue(td.id, out var existing) && existing != null)
            {
                var mf = existing.GetComponent<MeshFilter>(); if (mf.sharedMesh != td.mesh) mf.sharedMesh = td.mesh;
                return;
            }
            var go = new GameObject($"SplitChild_{td.id.face}_d{td.id.depth}_{td.id.x}_{td.id.y}");
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>(); filter.sharedMesh = td.mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = terrainMaterial; renderer.enabled = !invisible;
            _childTileObjects[td.id] = go;
        }

        private int GetSplitChildResolution(int depth)
        {
            if (!hierarchicalAlignedSampling)
            {
                int resBase = _bakedTileResolution > 0 ? _bakedTileResolution : config.baseResolution;
                if (splitChildResolutionMultiplier > 1.001f)
                    resBase = Mathf.RoundToInt(resBase * splitChildResolutionMultiplier);
                return Mathf.Max(4, resBase);
            }
            // Hierarchical rule with selectable refinement ratio.
            // We need the immediate parent resolution; if depth == bakedDepth+1 we use baked tile resolution
            int parentDepth = depth - 1;
            int parentRes = (parentDepth == bakedDepth) ?
                (_bakedTileResolution > 0 ? _bakedTileResolution : Mathf.Max(2, config.baseResolution / (1 << bakedDepth))) :
                // Reconstruct parent res by inverting formula: parentRes = ((childRes-1)/2)+1; since we don't have child yet, recursively compute upward.
                ReconstructParentRes(depth - 1);
            int baseChildRes = GetBaseChildResForParent(parentRes);
            int res = baseChildRes;
            if (splitChildResolutionMultiplier > 1.001f)
                res = Mathf.RoundToInt(res * splitChildResolutionMultiplier);
            // Keep odd to preserve perfect parent alignment pattern after multiplier
            if ((res & 1) == 0) res += 1; // keep odd for alignment
            return Mathf.Max(4, res);
        }

        private int ReconstructParentRes(int depth)
        {
            if (depth <= bakedDepth)
                return (depth == bakedDepth) ? (_bakedTileResolution > 0 ? _bakedTileResolution : Mathf.Max(2, config.baseResolution / (1 << bakedDepth))) : Mathf.Max(2, config.baseResolution / (1 << depth));
            // compute recursively from baked depth upward
            int res = ReconstructParentRes(depth - 1);
            return GetBaseChildResForParent(res);
        }

        private int GetBaseChildResForParent(int parentRes)
        {
            int intervals = parentRes - 1;
            if (refinementRatio == RefinementRatio.OnePointFive && (intervals % 2) == 0)
            {
                int newIntervals = (intervals * 3) / 2; // 1.5x
                return newIntervals + 1;
            }
            // Fallback to doubling for odd interval count or explicit Double mode
            return intervals * 2 + 1;
        }

        private float SampleHeightPipeline(Vector3 dir)
        {
            float raw = heightProvider != null ? heightProvider.Sample(dir) : 0f;
            raw *= config.heightScale;
            float hSample = raw; // realistic height remap removed
            hSample *= Mathf.Max(0.0001f, config.debugElevationMultiplier);
            if (config.shorelineDetail)
            {
                float seaR = config.baseRadius + config.seaLevel;
                float finalR = config.baseRadius + hSample;
                if (Mathf.Abs(finalR - seaR) <= config.shorelineBand)
                {
                    Vector3 sp = dir * config.shorelineDetailFrequency + new Vector3(12.345f,45.67f,89.01f);
                    float n = Mathf.PerlinNoise(sp.x, sp.y) * 2f - 1f;
                    float bandT = 1f - Mathf.Clamp01(Mathf.Abs(finalR - seaR) / Mathf.Max(0.0001f, config.shorelineBand));
                    float add = n * config.shorelineDetailAmplitude * bandT;
                    if (config.shorelinePreserveSign)
                    {
                        float before = finalR - seaR; float after = before + add;
                        if (Mathf.Sign(before) != 0 && Mathf.Sign(after) != Mathf.Sign(before)) add *= 0.3f;
                    }
                    hSample += add;
                }
            }
            if (config.cullBelowSea && !config.removeFullySubmergedTris && !config.debugDisableUnderwaterCulling)
            {
                float seaR = config.baseRadius + config.seaLevel; float finalR = config.baseRadius + hSample;
                if (finalR < seaR) hSample = (seaR + config.seaClampEpsilon) - config.baseRadius;
            }
            return hSample;
        }

        private float SampleRawWithOctaveCap(Vector3 dir, int maxOctave)
        {
            if (heightProvider is IOctaveSampler sampler && maxOctave >= 0)
                return sampler.SampleOctaveMasked(dir, maxOctave);
            return heightProvider != null ? heightProvider.Sample(dir) : 0f;
        }

        // --- Performance helpers ---
        private bool CanAffordSplit(TileData parentData)
        {
            return parentData != null && parentData.mesh != null; // always allow now
        }

        [ContextMenu("Force First Parent Split (Debug)")]
        private void ForceFirstParentSplitDebug()
        {
            if (bakedDepth < 0) return;
            foreach (var kv in _tiles)
            {
                var td = kv.Value;
                if (td.id.depth == bakedDepth)
                {
                    StartParentSplit(td.id, splitTargetDepthOverride >= 0 ? splitTargetDepthOverride : config.splitTargetDepth);
                    break;
                }
            }
        }

        [ContextMenu("Rebuild Active Child Tiles (Promote Detail)")]
        private void RebuildActiveChildrenContextMenu()
        {
            // Rebuild meshes for all current child tiles to promote previously constrained interior vertices to full detail under new rules.
            var list = new List<TileId>(_childTiles.Keys);
            foreach (var cid in list)
            {
                if (_childTiles.TryGetValue(cid, out var td))
                {
                    float dummyMin = 0f, dummyMax = 0f;
                    BuildTileMesh(td, ref dummyMin, ref dummyMax);
                    SpawnOrUpdateChildGO(td);
                }
            }
            RecomputeActiveCounts();
        }

        private void RecomputeActiveCounts()
        {
            int v = 0; int t = 0;
            foreach (var kv in _tileObjects)
            {
                var go = kv.Value; if (go == null) continue;
                var mr = go.GetComponent<MeshRenderer>(); if (mr == null || !mr.enabled) continue;
                var mf = go.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var m = mf.sharedMesh; v += m.vertexCount; t += (m.triangles?.Length ?? 0) / 3;
            }
            foreach (var kv in _childTileObjects)
            {
                var go = kv.Value; if (go == null) continue;
                var mr = go.GetComponent<MeshRenderer>(); if (mr == null || !mr.enabled) continue;
                var mf = go.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var m = mf.sharedMesh; v += m.vertexCount; t += (m.triangles?.Length ?? 0) / 3;
            }
            activeVertexCount = v; activeTriangleCount = t;
        }

    // Seam-fix helpers removed
    }
}
