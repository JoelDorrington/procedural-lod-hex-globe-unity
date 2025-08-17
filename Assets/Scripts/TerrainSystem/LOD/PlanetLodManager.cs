using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HexGlobeProject.TerrainSystem; // shared utilities
using HexGlobeProject.TerrainSystem.LOD; // TileFade

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Simplified single-depth planet terrain manager.
    /// This refactored version ONLY bakes and displays the medium depth (config.mediumDepth) tiles.
    /// All dynamic multi-LOD / selection / SSE logic has been removed for clarity and stability.
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

    [Header("Proximity Split LOD")]
    [Tooltip("Enable smooth splitting of medium tiles into higher-depth tiles near camera.")] [SerializeField] private bool enableProximitySplit = true;
    [Tooltip("Target higher depth for split (defaults to config.highDepth if <0)."), SerializeField] private int splitTargetDepthOverride = -1;
    [Tooltip("Enter split when angle to tile center < tileAngularSpan * this factor.")] [Range(0.1f,1.5f)] [SerializeField] private float splitEnterFactor = 0.6f;
    [Tooltip("Exit split when angle exceeds tileAngularSpan * this factor (should be > enter factor)."), Range(0.1f,2f)] [SerializeField] private float splitExitFactor = 1.1f;
    [Tooltip("Seconds for cross-fade between parent and children.")] [SerializeField] private float splitFadeDuration = 0.35f;
    [Tooltip("Max parent splits (new child sets) started per frame to cap cost.")] [SerializeField] private int splitMaxPerFrame = 2;
    [Tooltip("Destroy child tiles when far to reclaim memory.")] [SerializeField] private bool destroyChildrenOnMerge = true;
    [Tooltip("Debug: draw active split child tile wireframes.")] [SerializeField] private bool debugSplitChildrenGizmo = false;
    [Tooltip("Disable splitting when camera distance (zoom) exceeds this. -1 = never disable by distance.")] [SerializeField] private float splitDisableBeyondDistance = 40f;
    [Tooltip("Multiplier applied to the resolved resolution for split child tiles (>=1 for higher detail)."), SerializeField] private float splitChildResolutionMultiplier = 1f;

    // Split LOD state
    private readonly Dictionary<TileId, List<TileId>> _parentToChildren = new();
    private readonly HashSet<TileId> _activeSplitParents = new();
    private readonly Dictionary<TileId, Coroutine> _parentSplitCoroutines = new();
    private readonly Dictionary<TileId, TileData> _childTiles = new(); // keyed by child id
    private readonly Dictionary<TileId, GameObject> _childTileObjects = new();

        [Header("Runtime State (read-only)")] public bool bakeInProgress;        
        public float bakeProgress; // 0..1 across current phase
        public int bakedTileCount;
    public int bakedDepth = -1; // single depth baked
    private readonly Dictionary<TileId, TileData> _tiles = new();
    private readonly Dictionary<TileId, GameObject> _tileObjects = new();
    // (Removed dynamic patch state)

        // Temporary list to avoid GC during generation
        private readonly List<Vector3> _verts = new();
        private readonly List<int> _tris = new();
        private readonly List<Vector3> _normals = new();
    // (Removed refinement state)

        // Public entry point
        [ContextMenu("Bake Medium Depth")] public void BakeMediumDepthContextMenu()
        {
            if (bakeInProgress) return;
            StartCoroutine(BakeMediumDepth());
        }

        public IEnumerator BakeMediumDepth()
        {
            EnsureHeightProvider();
            bakeInProgress = true;
            bakeProgress = 0f;
            bakedTileCount = 0;
            _tiles.Clear();
            ClearActiveTiles();
            int depth = Mathf.Max(0, config.mediumDepth);
            PrepareOctaveMaskForDepth(depth);
            yield return StartCoroutine(BakeDepthSingle(depth));
            bakedDepth = depth;
            SpawnAllTiles();
            bakeInProgress = false;
            bakeProgress = 1f;
            if (debugLogBake)
                Debug.Log($"[PlanetLod] Baked medium depth {depth} tiles={_tiles.Count}");
        }

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
                    if (debugLogBake)
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
                StartCoroutine(BakeMediumDepth());
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
            int targetDepth = splitTargetDepthOverride >= 0 ? splitTargetDepthOverride : config.highDepth;
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
                        StartParentSplit(parent.id, targetDepth);
                        splitsStarted++;
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
            // Build children (parent depth +1 each level until targetDepth)
            int currentDepth = parentId.depth;
            var frontier = new List<TileId> { parentId };
            while (currentDepth < targetDepth)
            {
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
                    fade.Begin(true, splitFadeDuration);
                }
            }
            if (_tileObjects.TryGetValue(parentId, out var parentGO) && parentGO != null)
            {
                var fade = parentGO.GetComponent<TileFade>(); if (fade == null) fade = parentGO.AddComponent<TileFade>();
                fade.Begin(false, splitFadeDuration);
            }
            _activeSplitParents.Add(parentId);
            yield return new WaitForSeconds(splitFadeDuration);
            // Disable parent renderer once children visible
            if (_tileObjects.TryGetValue(parentId, out var parentGO2) && parentGO2 != null)
            {
                var mr = parentGO2.GetComponent<MeshRenderer>(); if (mr != null) mr.enabled = false;
            }
            _parentSplitCoroutines.Remove(parentId);
        }

        private IEnumerator CoMergeParent(TileId parentId)
        {
            if (!_parentToChildren.TryGetValue(parentId, out var children)) { _parentSplitCoroutines.Remove(parentId); yield break; }
            // Enable parent renderer and fade in
            if (_tileObjects.TryGetValue(parentId, out var parentGO) && parentGO != null)
            {
                var mr = parentGO.GetComponent<MeshRenderer>(); if (mr != null) mr.enabled = true;
                var fade = parentGO.GetComponent<TileFade>(); if (fade == null) fade = parentGO.AddComponent<TileFade>();
                fade.Begin(true, splitFadeDuration);
            }
            // Fade out children
            foreach (var cid in children)
            {
                if (_childTileObjects.TryGetValue(cid, out var go) && go != null)
                {
                    var fade = go.GetComponent<TileFade>(); if (fade == null) fade = go.AddComponent<TileFade>();
                    fade.Begin(false, splitFadeDuration);
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
            int tilesPerEdge = 1 << depth;
            int baseRes = ResolveResolutionForDepth(depth, tilesPerEdge);
            if (splitChildResolutionMultiplier <= 0.01f) return baseRes;
            int scaled = Mathf.Max(2, Mathf.RoundToInt(baseRes * splitChildResolutionMultiplier));
            return scaled;
        }

        private float SampleHeightPipeline(Vector3 dir)
        {
            float raw = heightProvider != null ? heightProvider.Sample(dir) : 0f;
            raw *= config.heightScale;
            float hSample = config.realisticHeights ? HeightRemapper.MinimalRemap(raw, config) : raw;
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
    }
}
