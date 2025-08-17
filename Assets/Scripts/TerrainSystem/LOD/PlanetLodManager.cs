using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HexGlobeProject.TerrainSystem; // shared utilities
using HexGlobeProject.TerrainSystem.LOD; // TileFade

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Orchestrates four-tier LOD system (initial implementation stage):
    /// - Bakes Low / Medium / High levels (quadtree depths from config) via coroutine.
    /// - Creates a MeshRenderer per baked tile for visualization (currently shows highest baked depth).
    /// - Computes simple per-tile min/max and a placeholder error metric (height range) for future SSE selection.
    /// - Extreme streaming NOT implemented yet (stub for future work).
    /// </summary>
    public class PlanetLodManager : MonoBehaviour
    {
        [SerializeField] private TerrainConfig config;
        [SerializeField] private TerrainHeightProviderBase heightProvider;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Material terrainMaterial;

        [Header("Display / Debug")]
        [Tooltip("Automatically show highest baked level after bake")] [SerializeField] private bool autoShowHighest = true;
    [Tooltip("Automatically start a bake when the component Starts")] [SerializeField] private bool autoBakeOnStart = true;
    [Tooltip("If auto baking, bake all configured depths (else Low only)")] [SerializeField] private bool autoBakeAllDepths = true;
        [Tooltip("Draw tile wireframes with Gizmos")] [SerializeField] private bool gizmoWireTiles = true;
        [Tooltip("Show bounds spheres (min/max radius)")] [SerializeField] private bool gizmoHeightRanges = false;
    [Tooltip("Continuously pick tiles via screen-space error (SSE) each frame")] [SerializeField] private bool enableDynamicSelection = true;
    [Tooltip("Factor applied to baseScreenError to broaden or narrow refinement")] [SerializeField] private float sseMultiplier = 1f;
    [Tooltip("Log LOD selection changes & depths (debug)")] [SerializeField] private bool debugLogTransitions = false;
    [Tooltip("Tint tiles by depth for debugging (temporary visualization)")] [SerializeField] private bool debugTintByDepth = false;
    [Header("Selection Modes")] 
    [Tooltip("Keep full low-depth coverage always, refining only a single stable patch under camera.")] [SerializeField] private bool singlePatchRefinement = true;
    [Tooltip("Camera distance at which highest baked depth is reached (world units from center). Closer than this => deepest.")] [SerializeField] private float deepestDetailDistance = 50f;
    [Tooltip("Camera distance where refinement begins (beyond this only low depth). Must be > deepestDetailDistance.")] [SerializeField] private float startRefineDistance = 120f;
    [Tooltip("Degrees camera must rotate before re-centering refined patch (prevents jitter).")] [Range(1f,45f)] [SerializeField] private float patchRecenteringAngle = 10f;
    [Tooltip("Fractional hysteresis for distance-based depth changes (0.1 = 10% slack).")] [Range(0f,0.5f)] [SerializeField] private float depthDistanceHysteresis = 0.15f;
    [Header("Visibility Culling")] 
    [Tooltip("Hide tiles on the back half of the planet relative to camera to reduce draw calls.")] [SerializeField] private bool cullBackHemisphere = true;
    [Tooltip("Dot threshold for culling: dot(cameraDir, tileCenterDir) < threshold => culled. 0 = exact hemisphere, negative expands.")] [Range(-0.5f,0.3f)] [SerializeField] private float backCullDotThreshold = 0f;

        [Header("Runtime State (read-only)")] public bool bakeInProgress;        
        public float bakeProgress; // 0..1 across current phase
        public int bakedTileCount;
        public int highestBakedDepth = -1;
    private float _lastSelectionTime;
    private readonly List<TileData> _selectionBuffer = new();
    private readonly Dictionary<TileId, GameObject> _tileObjects = new();

        // Internal storage: per baked depth dictionary of tiles
        private readonly Dictionary<int, Dictionary<TileId, TileData>> _bakedLevels = new();
        // Active instantiated GameObjects for currently displayed depth
        private readonly List<GameObject> _activeTileObjects = new();

        // Temporary list to avoid GC during generation
        private readonly List<Vector3> _verts = new();
        private readonly List<int> _tris = new();
        private readonly List<Vector3> _normals = new();
    // Stable refinement state
    private TileId? _focusLowTile; // currently refined low tile id
    private int _currentRefinedDepth; // depth we are refining to (target depth actually displayed)
    private float _lastFocusDot; // dot between camera dir and focus tile center at last recenter

        // Public entry point
        [ContextMenu("Bake Planet (Low Only)")] public void BakePlanetLowOnlyContextMenu()
        {
            if (bakeInProgress) return;
            StartCoroutine(BakePlanetLowOnly());
        }

        [ContextMenu("Bake Planet (All Baked Levels)")] public void BakePlanetAllContextMenu()
        {
            if (bakeInProgress) return;
            StartCoroutine(BakeAllBakedLevels());
        }

        public IEnumerator BakePlanetLowOnly()
        {
            EnsureHeightProvider();
            bakeInProgress = true;
            bakeProgress = 0;
            bakedTileCount = 0;
            _bakedLevels.Clear();
            highestBakedDepth = -1;
            ClearActiveTiles();

            int lowDepth = Mathf.Max(0, config.lowDepth);
            PrepareOctaveMaskForDepth(lowDepth);
            yield return StartCoroutine(BakeDepth(lowDepth));

            if (autoShowHighest)
                ShowDepth(lowDepth);

            bakeInProgress = false;
            bakeProgress = 1f;
        }

        public IEnumerator BakeAllBakedLevels()
        {
            EnsureHeightProvider();
            bakeInProgress = true;
            bakeProgress = 0f;
            bakedTileCount = 0;
            _bakedLevels.Clear();
            highestBakedDepth = -1;
            ClearActiveTiles();

            int low = Mathf.Max(0, config.lowDepth);
            int med = Mathf.Max(low, config.mediumDepth);
            int high = Mathf.Max(med, config.highDepth);
            int ultra = Mathf.Max(high, config.ultraDepth);
            bool useUltra = config.ultraDepth > config.highDepth; // only if strictly deeper

            PrepareOctaveMaskForDepth(low);
            // Compute total tile count for progress normalization
            int TotalTiles(int depth) => 6 * (1 << depth) * (1 << depth);
            float total = TotalTiles(low) + (med != low ? TotalTiles(med) : 0) + (high != med ? TotalTiles(high) : 0) + (useUltra ? TotalTiles(ultra) : 0);
            float processedSoFar = 0f;

            yield return StartCoroutine(BakeDepth(low, progress => bakeProgress = (processedSoFar + progress * TotalTiles(low)) / total));
            processedSoFar += TotalTiles(low);
            if (med != low)
            {
                PrepareOctaveMaskForDepth(med);
                yield return StartCoroutine(BakeDepth(med, progress => bakeProgress = (processedSoFar + progress * TotalTiles(med)) / total));
                processedSoFar += TotalTiles(med);
            }
            if (high != med)
            {
                PrepareOctaveMaskForDepth(high);
                yield return StartCoroutine(BakeDepth(high, progress => bakeProgress = (processedSoFar + progress * TotalTiles(high)) / total));
                processedSoFar += TotalTiles(high);
            }
            if (useUltra)
            {
                PrepareOctaveMaskForDepth(ultra);
                yield return StartCoroutine(BakeDepth(ultra, progress => bakeProgress = (processedSoFar + progress * TotalTiles(ultra)) / total));
                processedSoFar += TotalTiles(ultra);
            }

            if (autoShowHighest)
                ShowDepth(useUltra ? ultra : high);

            bakeInProgress = false;
            bakeProgress = 1f;

            if (enableDynamicSelection)
                UpdateSelectionImmediate();
        }

        private IEnumerator BakeDepth(int depth, System.Action<float> phaseProgress = null)
        {
            // Depth means quadtree depth per face. depth=0 means single tile (the whole face).
            int tilesPerEdge = 1 << depth; // 2^depth
            int totalTilesThisDepth = 6 * tilesPerEdge * tilesPerEdge; // 6 cube faces

            var levelDict = new Dictionary<TileId, TileData>(totalTilesThisDepth);
            _bakedLevels[depth] = levelDict;

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
                        float local = processed / (float)totalTilesThisDepth;
                        if (phaseProgress != null) phaseProgress(local); else bakeProgress = local;
                        if ((processed & 7) == 0) // yield every 8 tiles
                            yield return null;
                    }
                }
            }
            if (depth > highestBakedDepth) highestBakedDepth = depth;
            if (debugLogTransitions)
            {
                Debug.Log($"[PlanetLod] Baked depth {depth} res={resolution} tiles={totalTilesThisDepth} rawRange=({rawMin:F3},{rawMax:F3}) remapRange=({globalMin:F3},{globalMax:F3}) span={(globalMax - globalMin):F3}");
            }
        }

        private void BuildTileMesh(TileData data)
        {
            float rmin = float.MaxValue; float rmax = float.MinValue;
            BuildTileMesh(data, ref rmin, ref rmax);
        }

        private void BuildTileMesh(TileData data, ref float rawMin, ref float rawMax)
        {
            _verts.Clear();
            _tris.Clear();
            _normals.Clear();

            int res = data.resolution;
            float inv = 1f / (res - 1);
            float radius = config.baseRadius;
            float minH = float.MaxValue;
            float maxH = float.MinValue;
            Vector3 centerAccum = Vector3.zero;
            int vertCounter = 0;

            // Build a patch of the cube face then project to sphere and apply height
            bool doCull = config.cullBelowSea && !config.debugDisableUnderwaterCulling;
            bool removeTris = doCull && config.removeFullySubmergedTris;
            float seaR = config.baseRadius + config.seaLevel;
            float eps = config.seaClampEpsilon;
            var submergedFlags = removeTris ? new List<bool>(res * res) : null;
            for (int j = 0; j < res; j++)
            {
                for (int i = 0; i < res; i++)
                {
                    // Local UV within tile
                    float u = (i * inv + data.id.x) / (1 << data.id.depth); // 0..1 across full face
                    float v = (j * inv + data.id.y) / (1 << data.id.depth);

                    // Map cube face to direction via shared helper
                    Vector3 dir = CubeSphere.FaceLocalToUnit(data.id.face, u * 2f - 1f, v * 2f - 1f);

                    // Raw noise sample (provider units)
                    float raw = heightProvider != null ? heightProvider.Sample(dir) : 0f;
                    // Apply explicit height scale from config (previously ignored)
                    raw *= config.heightScale;
                    if (raw < rawMin) rawMin = raw;
                    if (raw > rawMax) rawMax = raw;
                    float hSample;
                    // Only perform physically-bounded remap when realisticHeights (or other realism toggles) are enabled.
                    if (config.realisticHeights)
                        hSample = HeightRemapper.MinimalRemap(raw, config);
                    else
                        hSample = raw; // allow large exaggerated relief when realism disabled

                    hSample *= Mathf.Max(0.0001f, config.debugElevationMultiplier);
                    float finalR = radius + hSample;
                    // Shoreline high-frequency refinement: only deeper depths & within band
                    if (config.shorelineDetail && data.id.depth >= config.shorelineDetailMinDepth && Mathf.Abs(finalR - (config.baseRadius + config.seaLevel)) <= config.shorelineBand)
                    {
                        // Sample a simple extra Perlin based on position (direction) scaled by frequency multiplier
                        Vector3 sp = dir * config.shorelineDetailFrequency + new Vector3(12.345f, 45.67f, 89.01f);
                        float n = Mathf.PerlinNoise(sp.x, sp.y) * 2f - 1f;
                        // Taper by proximity to sea surface (0 at band edge -> 1 at sea level)
                        float seaRLocal = config.baseRadius + config.seaLevel;
                        float bandT = 1f - Mathf.Clamp01(Mathf.Abs(finalR - seaRLocal) / Mathf.Max(0.0001f, config.shorelineBand));
                        float add = n * config.shorelineDetailAmplitude * bandT;
                        if (config.shorelinePreserveSign)
                        {
                            float before = finalR - seaRLocal; // original signed offset from sea
                            float after = before + add;
                            // If sign flips (would alter silhouette), damp adjustment to keep sign
                            if (Mathf.Sign(before) != 0 && Mathf.Sign(after) != Mathf.Sign(before))
                            {
                                add *= 0.3f; // heavy damp to avoid crossing
                            }
                        }
                        finalR += add;
                        hSample = finalR - radius; // update stored sample
                    }
                    bool submerged = doCull && finalR < seaR;
                    if (submerged && !removeTris)
                    {
                        // Clamp up slightly so we keep a continuous surface when not removing tris.
                        finalR = seaR + eps;
                    }
                    minH = Mathf.Min(minH, hSample);
                    maxH = Mathf.Max(maxH, hSample);
                    _verts.Add(dir * finalR);
                    _normals.Add(dir); // approximate
                    centerAccum += dir * finalR;
                    vertCounter++;
                    if (removeTris) submergedFlags.Add(submerged);
                }
            }

            // Triangles
            for (int j = 0; j < res - 1; j++)
            {
                for (int i = 0; i < res - 1; i++)
                {
                    int idx = j * res + i;
                    int i0 = idx;
                    int i1 = idx + 1;
                    int i2 = idx + res;
                    int i3 = idx + res + 1;
                    if (removeTris)
                    {
                        bool sub0 = submergedFlags[i0];
                        bool sub1 = submergedFlags[i1];
                        bool sub2 = submergedFlags[i2];
                        bool sub3 = submergedFlags[i3];
                        bool tri1Skip = sub0 && sub2 && sub1; // i0,i2,i1
                        bool tri2Skip = sub1 && sub2 && sub3; // i1,i2,i3
                        if (!tri1Skip) { _tris.Add(i0); _tris.Add(i2); _tris.Add(i1); }
                        if (!tri2Skip) { _tris.Add(i1); _tris.Add(i2); _tris.Add(i3); }
                    }
                    else
                    {
                        // two tris (i0,i2,i1) and (i1,i2,i3) for consistent winding (depends on face orientation)
                        _tris.Add(i0); _tris.Add(i2); _tris.Add(i1);
                        _tris.Add(i1); _tris.Add(i2); _tris.Add(i3);
                    }
                }
            }

            // Orientation correction: if triangle winding ended up inward (normal pointing toward center), flip all.
            if (_tris.Count >= 3)
            {
                Vector3 va = _verts[_tris[0]];
                Vector3 vb = _verts[_tris[1]];
                Vector3 vc = _verts[_tris[2]];
                Vector3 triN = Vector3.Cross(vb - va, vc - va); // current winding normal
                // Outward should roughly align with vertex position (planet centered at origin)
                if (Vector3.Dot(triN, va) < 0f)
                {
                    for (int t = 0; t < _tris.Count; t += 3)
                    {
                        int tmp = _tris[t + 1];
                        _tris[t + 1] = _tris[t + 2];
                        _tris[t + 2] = tmp; // swap to flip winding
                    }
                    if (debugLogTransitions)
                        Debug.Log($"[PlanetLod] Flipped winding for tile depth {data.id.depth} face {data.id.face} to face outward.");
                }
            }

            var mesh = new Mesh();
            mesh.indexFormat = (res * res > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(_verts);
            mesh.SetTriangles(_tris, 0, true);
            mesh.SetNormals(_normals);
            if (config.recalcNormals)
            {
                // Recalculate for geometric shading so elevation visible from view direction
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();

            data.mesh = mesh;
            data.minHeight = minH;
            data.maxHeight = maxH;
            data.error = maxH - minH; // simple height range as placeholder geometric error
            if (vertCounter > 0)
            {
                data.center = centerAccum / vertCounter;
                data.boundsRadius = 0.5f * ( (radius + maxH) - (radius + minH) ) + (radius + (minH+maxH)*0.5f); // crude, will refine
            }
        }

        private OctaveMaskHeightProvider _octaveWrapper; // reused wrapper instance
        private void PrepareOctaveMaskForDepth(int depth)
        {
            int maxOct = -1;
            if (depth == config.lowDepth) maxOct = config.lowMaxOctave;
            else if (depth == config.mediumDepth) maxOct = config.mediumMaxOctave;
            else if (depth == config.highDepth) maxOct = config.highMaxOctave;
            else if (depth == config.ultraDepth) maxOct = config.ultraMaxOctave;
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
            // Prefer explicit per-level settings else derive from baseResolution scaled by tile size.
            int explicitRes = 0;
            if (depth == config.lowDepth && config.lowResolution > 0) explicitRes = config.lowResolution;
            else if (depth == config.mediumDepth && config.mediumResolution > 0) explicitRes = config.mediumResolution;
            else if (depth == config.highDepth && config.highResolution > 0) explicitRes = config.highResolution;
            else if (depth == config.ultraDepth && config.ultraResolution > 0) explicitRes = config.ultraResolution;
            if (explicitRes > 0) return Mathf.Max(2, explicitRes);
            // Fallback heuristic: maintain roughly constant vertex density per face.
            // A depth 'd' splits face into (2^d)^2 tiles; we want each tile to have baseResolution / (2^d) vertices along edge.
            int derived = Mathf.Max(2, config.baseResolution / tilesPerEdge);
            // Slight boost for higher detail levels so close-up looks richer.
            if (depth == config.highDepth) derived = Mathf.Max(derived, config.baseResolution / tilesPerEdge + 2);
            if (depth == config.ultraDepth) derived = Mathf.Max(derived, config.baseResolution / tilesPerEdge + 4);
            return derived;
        }

    // (Removed CubeFaceUvToDir; replaced by CubeSphere.FaceLocalToUnit)

        private void ShowDepth(int depth)
        {
            if (!_bakedLevels.TryGetValue(depth, out var level)) return;
            ClearActiveTiles();
            foreach (var kv in level)
            {
                var td = kv.Value;
                if (td.mesh == null) continue;
                SpawnOrUpdateTileGO(td);
            }
            TerrainShaderGlobals.Apply(config, terrainMaterial);
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
            // Auto-bake safety: if user forgot and no tiles baked yet, trigger once (delayed until Start sets targetCamera)
            if (autoBakeOnStart && !bakeInProgress && highestBakedDepth < 0)
            {
                autoBakeOnStart = false; // consume
                if (autoBakeAllDepths) StartCoroutine(BakeAllBakedLevels()); else StartCoroutine(BakePlanetLowOnly());
            }
            else if (highestBakedDepth < 0)
            {
                // Keep trying to bind provider if null before bake is triggered manually
                EnsureHeightProvider(false);
            }
            if (!enableDynamicSelection || bakeInProgress) return;
            if (highestBakedDepth < 0) return; // nothing baked yet
            if (targetCamera == null) targetCamera = Camera.main;
            UpdateSelectionImmediate();
        }

        private bool _warnedMissingProvider;
        private void EnsureHeightProvider(bool logOnBind = true)
        {
            if (heightProvider == null && config != null && config.heightProvider != null)
            {
                heightProvider = config.heightProvider;
                if (logOnBind && debugLogTransitions)
                    Debug.Log("[PlanetLod] Bound heightProvider from TerrainConfig: " + config.heightProvider.GetType().Name);
            }
            if (heightProvider == null && !_warnedMissingProvider)
            {
                _warnedMissingProvider = true;
                Debug.LogWarning("[PlanetLod] No heightProvider assigned (and none in config). Terrain will be flat.");
            }
        }

        private void UpdateSelectionImmediate()
        {
            if (targetCamera == null) return;
            if (singlePatchRefinement)
            {
                DoSinglePatchSelection();
            }
            else
            {
                DoSseSelection();
            }
        }

        private void DoSinglePatchSelection()
        {
            int low = Mathf.Max(0, config.lowDepth);
            if (!_bakedLevels.TryGetValue(low, out var lowLevel)) return;
            _selectionBuffer.Clear();
            foreach (var kv in lowLevel) _selectionBuffer.Add(kv.Value); // baseline
            float camDist = targetCamera.transform.position.magnitude;
            if (highestBakedDepth <= low) { ApplySelection(); return; }
            if (camDist > startRefineDistance) { _currentRefinedDepth = low; ApplySelection(); return; }
            // Desired depth (raw)
            float tRaw = Mathf.InverseLerp(startRefineDistance, deepestDetailDistance, camDist);
            int desiredDepth = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(low, highestBakedDepth, 1f - tRaw)), low, highestBakedDepth);
            if (_currentRefinedDepth < low) _currentRefinedDepth = low;
            float upThresh = deepestDetailDistance * (1f + depthDistanceHysteresis);
            float downThresh = startRefineDistance * (1f - depthDistanceHysteresis);
            if (desiredDepth > _currentRefinedDepth && camDist <= upThresh) _currentRefinedDepth = desiredDepth;
            else if (desiredDepth < _currentRefinedDepth && camDist >= downThresh) _currentRefinedDepth = desiredDepth;
            Vector3 camDir = targetCamera.transform.position.normalized;
            TileData focusLow;
            if (_focusLowTile.HasValue && lowLevel.TryGetValue(_focusLowTile.Value, out var saved))
            {
                focusLow = saved;
                float dot = Vector3.Dot(camDir, focusLow.center.normalized);
                float angleDeg = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
                if (angleDeg > patchRecenteringAngle)
                {
                    focusLow = FindClosestTile(lowLevel, camDir);
                    _focusLowTile = focusLow.id;
                }
            }
            else
            {
                focusLow = FindClosestTile(lowLevel, camDir);
                if (focusLow != null) _focusLowTile = focusLow.id;
            }
            if (focusLow == null || _currentRefinedDepth <= low) { ApplySelection(); return; }
            TileData current = focusLow;
            for (int d = low + 1; d <= _currentRefinedDepth; d++)
            {
                if (!_bakedLevels.TryGetValue(d, out var nextDict)) break;
                var c0 = current.id.Child(0); var c1 = current.id.Child(1); var c2 = current.id.Child(2); var c3 = current.id.Child(3);
                if (!nextDict.TryGetValue(c0, out var td0) || !nextDict.TryGetValue(c1, out var td1) ||
                    !nextDict.TryGetValue(c2, out var td2) || !nextDict.TryGetValue(c3, out var td3)) break;
                current = ClosestOf(camDir, td0, td1, td2, td3);
                if (d == _currentRefinedDepth)
                {
                    _selectionBuffer.RemoveAll(td => td.id.Equals(current.id.Parent()));
                    _selectionBuffer.Add(td0); _selectionBuffer.Add(td1); _selectionBuffer.Add(td2); _selectionBuffer.Add(td3);
                }
            }
            ApplySelection();
        }

        private TileData FindClosestTile(Dictionary<TileId, TileData> tiles, Vector3 dir)
        {
            TileData best = null; float bestDot = -2f;
            foreach (var kv in tiles)
            {
                var td = kv.Value; if (td.center == Vector3.zero) continue;
                float dot = Vector3.Dot(dir, td.center.normalized);
                if (dot > bestDot) { bestDot = dot; best = td; }
            }
            return best;
        }
        private TileData ClosestOf(Vector3 dir, params TileData[] list)
        {
            TileData best = null; float bestDot = -2f;
            foreach (var td in list)
            {
                if (td.center == Vector3.zero) continue;
                float dot = Vector3.Dot(dir, td.center.normalized);
                if (dot > bestDot) { bestDot = dot; best = td; }
            }
            return best;
        }

        private void DoSseSelection()
        {
            _selectionBuffer.Clear();
            int low = Mathf.Max(0, config.lowDepth);
            if (!_bakedLevels.TryGetValue(low, out var roots)) return;
            var frontier = new List<TileData>();
            foreach (var kv in roots) frontier.Add(kv.Value);
            float threshold = config.baseScreenError * Mathf.Max(0.0001f, sseMultiplier);
            var next = new List<TileData>();
            bool refined; int safety = 0;
            do
            {
                refined = false;
                _selectionBuffer.Clear();
                foreach (var tile in frontier)
                {
                    if (tile.id.depth < highestBakedDepth && ShouldRefine(tile, threshold, out var childrenList))
                    {
                        next.AddRange(childrenList);
                        refined = true;
                    }
                    else _selectionBuffer.Add(tile);
                }
                if (refined)
                {
                    frontier.Clear(); frontier.AddRange(next); next.Clear();
                }
            } while (refined && ++safety < 32);
            if (!refined && frontier.Count > 0 && _selectionBuffer.Count == 0) _selectionBuffer.AddRange(frontier);
            ApplySelection();
        }

        private bool ShouldRefine(TileData tile, float threshold, out List<TileData> children)
        {
            children = null;
            int nextDepth = tile.id.depth + 1;
            if (!_bakedLevels.TryGetValue(nextDepth, out var nextLevelDict)) return false;
            // Gather children
            var c0 = tile.id.Child(0); var c1 = tile.id.Child(1); var c2 = tile.id.Child(2); var c3 = tile.id.Child(3);
            if (!nextLevelDict.TryGetValue(c0, out var td0) || !nextLevelDict.TryGetValue(c1, out var td1) ||
                !nextLevelDict.TryGetValue(c2, out var td2) || !nextLevelDict.TryGetValue(c3, out var td3))
                return false; // missing child tiles
            float sse = EstimateScreenSpaceError(tile);
            if (sse > threshold)
            {
                children = _tmpChildren;
                children.Clear();
                children.Add(td0); children.Add(td1); children.Add(td2); children.Add(td3);
                return true;
            }
            return false;
        }

        private readonly List<TileData> _tmpChildren = new();

        private float EstimateScreenSpaceError(TileData tile)
        {
            // Simple heuristic: project tile geometric error (height range) at tile center distance.
            Vector3 worldCenter = tile.center; // planet centered at origin
            float distance = Vector3.Distance(targetCamera.transform.position, worldCenter);
            float errorWorld = Mathf.Max(0.0001f, tile.error);
            // Convert world error to approximate pixels: (error / distance) * focalLengthPixels
            float fovRad = targetCamera.fieldOfView * Mathf.Deg2Rad;
            float screenHeight = Screen.height;
            float focalLength = (0.5f * screenHeight) / Mathf.Tan(0.5f * fovRad);
            float pixelError = (errorWorld / distance) * focalLength;
            return pixelError;
        }

        private void ApplySelection()
        {
            _activeFlags.Clear();
            int maxDepthSeen = -1;
            Vector3 camDir = targetCamera ? targetCamera.transform.position.normalized : Vector3.forward;
            foreach (var td in _selectionBuffer)
            {
                if (cullBackHemisphere)
                {
                    Vector3 tdir = td.center.normalized;
                    float dot = Vector3.Dot(camDir, tdir);
                    if (dot < backCullDotThreshold) continue;
                }
                SpawnOrUpdateTileGO(td);
                _activeFlags.Add(td.id);
                if (td.id.depth > maxDepthSeen) maxDepthSeen = td.id.depth;
            }
            // Despawn unselected
            _toRemove.Clear();
            foreach (var kv in _tileObjects)
                if (!_activeFlags.Contains(kv.Key)) _toRemove.Add(kv.Key);
            if (config.enableCrossFade && Application.isPlaying)
                StartCoroutine(FadeAndDestroy(_toRemove));
            else
            {
                foreach (var id in _toRemove)
                {
                    var go = _tileObjects[id];
                    if (go != null) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
                    _tileObjects.Remove(id);
                }
            }
            TerrainShaderGlobals.Apply(config, terrainMaterial);
            if (debugLogTransitions && Application.isPlaying && maxDepthSeen >= 0)
                Debug.Log($"[PlanetLod] Active tiles: {_selectionBuffer.Count} (max depth {maxDepthSeen})");
        }

        private readonly HashSet<TileId> _activeFlags = new();
        private readonly List<TileId> _toRemove = new();

        private void SpawnOrUpdateTileGO(TileData td)
        {
            if (_tileObjects.TryGetValue(td.id, out var existing) && existing != null)
            {
                var mf = existing.GetComponent<MeshFilter>();
                if (mf.sharedMesh != td.mesh) mf.sharedMesh = td.mesh;
                if (debugTintByDepth)
                {
                    var r = existing.GetComponent<MeshRenderer>();
                    if (r != null)
                    {
                        Color c = Color.Lerp(Color.white, Color.red, td.id.depth / Mathf.Max(1f, highestBakedDepth));
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
            if (debugTintByDepth)
            {
                Color c = Color.Lerp(Color.white, Color.red, td.id.depth / Mathf.Max(1f, highestBakedDepth));
                renderer.sharedMaterial = new Material(terrainMaterial);
                renderer.sharedMaterial.SetColor("_ColorHigh", c);
            }
            if (config.enableCrossFade && Application.isPlaying)
            {
                var fade = go.AddComponent<global::HexGlobeProject.TerrainSystem.LOD.TileFade>();
                fade.Begin(true, config.lodFadeDuration);
            }
            _tileObjects[td.id] = go;
        }

        private IEnumerator FadeAndDestroy(List<TileId> ids)
        {
            float dur = Mathf.Max(0.05f, config.lodFadeDuration);
            var toDestroy = new List<GameObject>();
            foreach (var id in ids)
            {
                if (_tileObjects.TryGetValue(id, out var go) && go != null)
                {
                    var fade = go.GetComponent<global::HexGlobeProject.TerrainSystem.LOD.TileFade>();
                    if (fade == null) fade = go.AddComponent<global::HexGlobeProject.TerrainSystem.LOD.TileFade>();
                    fade.Begin(false, dur);
                    toDestroy.Add(go);
                }
                _tileObjects.Remove(id);
            }
            yield return new WaitForSeconds(dur);
            foreach (var go in toDestroy)
            {
                if (go != null) Destroy(go);
            }
        }

        // Optional gizmos for debugging baked tiles
        private void OnDrawGizmosSelected()
        {
            if (!gizmoWireTiles && !gizmoHeightRanges) return;
            foreach (var kvLevel in _bakedLevels)
            {
                foreach (var kv in kvLevel.Value)
                {
                    var data = kv.Value;
                    if (data.mesh == null) continue;
                    if (gizmoWireTiles)
                    {
                        Gizmos.color = Color.Lerp(Color.green, Color.red, data.id.depth / 8f);
                        Gizmos.DrawWireMesh(data.mesh);
                    }
                    if (gizmoHeightRanges)
                    {
                        float rMin = config.baseRadius + data.minHeight;
                        float rMax = config.baseRadius + data.maxHeight;
                        Vector3 center = Vector3.zero; // planet center
                        Gizmos.color = new Color(1, 1, 0, 0.1f);
                        Gizmos.DrawWireSphere(center, rMin);
                        Gizmos.color = new Color(1, 0, 0, 0.1f);
                        Gizmos.DrawWireSphere(center, rMax);
                    }
                }
            }
        }
    }
}
