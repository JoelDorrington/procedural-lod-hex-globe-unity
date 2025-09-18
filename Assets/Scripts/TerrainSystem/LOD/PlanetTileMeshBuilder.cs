using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem.Core;
using System;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class PlanetTileMeshBuilder
    {
        // Diagnostic helper: when true, log a small number of seam-key events to help
        // debug adjacent-tile continuity issues in editor tests. Keep logging tightly
        // bounded to avoid spamming the test output.
        private const bool s_enableSeamDiagnostics = true;
        private int _diagnosticSeamLogs = 0;
        private const int s_maxSeamLogs = 64;

        // Temporary per-builder shared-vertex map used to guarantee exact
        // identical world-space vertex positions for canonical global grid
        // indices when multiple tiles are built with the same builder instance.
        // This is intentionally not a long-lived static cache — callers may
        // call ClearSharedVertexMap() to reset between unrelated build passes.
        private readonly Dictionary<string, Vector3> _passSharedVertexMap = new();

        /// <summary>
        /// Clear the temporary shared-vertex map. Tests and callers that perform
        /// isolated builds may call this to avoid cross-build pollution.
        /// </summary>
        public void ClearSharedVertexMap()
        {
            _passSharedVertexMap.Clear();
        }
        // Simple cache so repeated builds for the same TileId return the same Mesh instance.
        // Cache both the Mesh and the sampled raw height range so callers that pass
        // ref rawMin/rawMax receive meaningful values even when the mesh is reused.
        private struct CachedMeshEntry { public Mesh mesh; public float minH; public float maxH; public Vector3 centerUsed; public int resolutionUsed; }
        private static readonly Dictionary<TileId, CachedMeshEntry> s_meshCache = new();
        private readonly TerrainConfig config;
        private readonly TerrainHeightProviderBase heightProvider;
        private readonly Vector3 planetCenter = default;
        public TerrainConfig Config => config;

        // Temporary lists for mesh generation
        private readonly List<Vector3> _verts = new();
        private readonly List<int> _tris = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _uvs = new();

        public PlanetTileMeshBuilder(
            TerrainConfig config,
            TerrainHeightProviderBase heightProvider = null,
            Vector3 planetCenter = default)
        {
            this.config = config;
            this.heightProvider = heightProvider;
            this.planetCenter = planetCenter;
        }

        /// <summary>
        /// Builds the tile mesh and allows caller to specify triangle winding direction.
        /// </summary>
        /// <param name="data">Tile data to populate</param>
        /// <param name="rawMin">Minimum sampled height</param>
        /// <param name="rawMax">Maximum sampled height</param>
        public void BuildTileMesh(TileData data, ref float rawMin, ref float rawMax)
        {
            // Note: cache lookup moved later after we obtain the precomputed 'entry'
            // so we can verify the cached mesh was built with the same center.

            _verts.Clear();
            _tris.Clear();
            _normals.Clear();
            _uvs.Clear();

            // Reserve capacity to avoid repeated reallocations for large resolutions
            // Use triangular lattice vertex count: res * (res + 1) / 2
            int expectedVertsCap = data.resolution * (data.resolution + 1) / 2;
            if (_verts.Capacity < expectedVertsCap) _verts.Capacity = expectedVertsCap;
            if (_normals.Capacity < expectedVertsCap) _normals.Capacity = expectedVertsCap;
            if (_uvs.Capacity < expectedVertsCap) _uvs.Capacity = expectedVertsCap;

            int res = data.resolution;
            float radius = config.baseRadius;
            float minH = float.MaxValue;
            float maxH = float.MinValue;

            // Resolve a height provider to use for sampling. If the injected provider is null
            // (for example a serialized reference couldn't be deserialized), fall back to
            // the config's provider or a new default SimplePerlinHeightProvider so terrain
            // is still generated instead of flat tiles.
            var provider = heightProvider ?? config.heightProvider ?? new SimplePerlinHeightProvider();

            Vector3 centerAccum = Vector3.zero; // Still accumulate for fallback/validation
            int vertCounter = 0;

            // Debug observability: measure time spent sampling vs building triangles and detect degenerate tris
            System.Diagnostics.Stopwatch swTotal = null;
            System.Diagnostics.Stopwatch swSampling = null;
            System.Diagnostics.Stopwatch swTriBuild = null;
            int degenerateTriangleCount = 0;
            if (Debug.isDebugBuild)
            {
                swTotal = System.Diagnostics.Stopwatch.StartNew();
                swSampling = new System.Diagnostics.Stopwatch();
                swTriBuild = new System.Diagnostics.Stopwatch();
            }

            bool measureSampling = swSampling != null;
            bool measureTriBuild = swTriBuild != null;
            const float minTriangleArea = 1e-7f;

            var registry = new TerrainTileRegistry(data.id.depth, radius, planetCenter);
            var key = new TileId(data.id.face, data.id.x, data.id.y, data.id.depth);
            if (!registry.tiles.TryGetValue(key, out var entry))
                throw new Exception("Invalid TileId: " + data.id);

            // Return cached mesh instance when available to keep reference equality stable.
            if (data != null && data.id.face >= 0)
            {
                if (s_meshCache.TryGetValue(data.id, out var existing))
                {
                    // Guard against Unity 'destroyed' objects being present in the static cache
                    // (happens in test runs where teardown can destroy created Meshes). If the
                    // cached Mesh has been destroyed, drop the cache entry and fall through to
                    // rebuilding a fresh mesh.
                    if (existing.mesh == null)
                    {
                        try { s_meshCache.Remove(data.id); } catch { }
                    }
                    else if (existing.mesh.name != null && existing.mesh.name.EndsWith("_local"))
                    {
                        // Some older code paths created a "_local" clone and it may have been
                        // cached accidentally; treat such cache entries as stale and rebuild.
                        try { s_meshCache.Remove(data.id); } catch { }
                    }
                    else if ((existing.centerUsed - entry.centerWorld).sqrMagnitude > 1e-6f)
                    {
                        // Cached mesh was built with a different center than the current
                        // precomputed registry entry. Discard stale cache so we rebuild
                        // with the authoritative center and avoid doubled offsets.
                        try { s_meshCache.Remove(data.id); } catch { }
                    }
                    else if (existing.resolutionUsed != data.resolution)
                    {
                        // Cached mesh was built with a different sampling resolution. Invalidate cache
                        // so callers requesting a different density get a correctly sized mesh.
                        try { s_meshCache.Remove(data.id); } catch { }
                    }
                    else
                    {
                        // Populate data and the ref outputs so callers relying on sampled
                        // ranges (rawMin/rawMax) get correct values even when the mesh
                        // was built earlier.
                        data.mesh = existing.mesh;
                        data.minHeight = existing.minH;
                        data.maxHeight = existing.maxH;
                        // Propagate into the caller's refs if they provided sentinel values
                        // (keeps behavior stable for tests that expect variation).
                        rawMin = existing.minH;
                        rawMax = existing.maxH;
                        return;
                    }
                }
            }

            int depthTiles = entry.tilesPerEdge;
            float tileSize = 1f / depthTiles;

            // Use direct barycentric->world mapping per-vertex to ensure consistent sampling
            // (tangent-plane projection caused area distortion at high resolutions)

            // Generate vertices only inside the canonical triangle (u+v <= 1) by iterating
            // a triangular lattice. This prevents mirrored/duplicated vertices and keeps
            // vertex counts minimal: res*(res+1)/2.
            // We'll keep a mapping from (i,j) -> vertex index so triangle indices can be
            // constructed easily.
            int[,] vertexIndexMap = new int[res, res];
            for (int jj = 0; jj < res; jj++) for (int ii = 0; ii < res; ii++) vertexIndexMap[ii, jj] = -1;

            for (int j = 0; j < res; j++)
            {
                int maxI = res - 1 - j; // ensure i+j <= res-1 (inside triangle)
                for (int i = 0; i <= maxI; i++)
                {
                    // Compute canonical global integer indices for this lattice point so we can
                    // derive a single authoritative barycentric (u,v) for seam vertices.
                    int resMinusOneLocal = Mathf.Max(1, res - 1);
                    int localI = Mathf.Clamp(i, 0, resMinusOneLocal);
                    int localJ = Mathf.Clamp(j, 0, resMinusOneLocal);
                    int globalPerEdgeLocal = entry.tilesPerEdge * resMinusOneLocal;
                    int globalI = data.id.x * resMinusOneLocal + localI;
                    int globalJ = data.id.y * resMinusOneLocal + localJ;

                    // Determine if this vertex lies on a seam (face/edge of the global grid
                    // or on a local tile boundary). We must treat local tile boundaries as
                    // seam vertices so adjacent tiles built with the same builder instance
                    // will consult the same per-pass shared-vertex map and produce
                    // identical world positions for shared vertices.
                    bool isSeamVertex = (
                        // local tile boundary within this tile
                        localI == 0 || localJ == 0 || localI == resMinusOneLocal || localJ == resMinusOneLocal
                        // global face/grid boundaries or diagonal seam
                        || globalI == 0 || globalJ == 0 || globalI == globalPerEdgeLocal || globalJ == globalPerEdgeLocal || (globalI + globalJ) == globalPerEdgeLocal
                    );

                    float globalU, globalV;
                    if (isSeamVertex)
                    {
                        // For seam vertices, compute barycentric coords from the global integer grid
                        // using double precision division to avoid tiny float rounding differences
                        // across different tile builds. Apply the same diagonal reflection and
                        // tiny-nudge logic used in TileVertexToBarycentricCoordinates so the
                        // canonical (u,v) and integer indices match across code paths.
                        double dGlobalI = (double)globalI;
                        double dGlobalJ = (double)globalJ;
                        double dGlobalPerEdge = (double)globalPerEdgeLocal;
                        globalU = (float)(dGlobalI / dGlobalPerEdge);
                        globalV = (float)(dGlobalJ / dGlobalPerEdge);

                        const float edgeEpsilon = 1e-6f;
                        if (globalU + globalV >= 1f - edgeEpsilon)
                        {
                            if (Mathf.Abs(globalU + globalV - 1f) < edgeEpsilon)
                            {
                                // tiny inward nudge to keep face selection stable
                                globalU -= edgeEpsilon * 0.5f;
                                globalV -= edgeEpsilon * 0.5f;
                            }
                            else
                            {
                                // reflect indices to canonical triangle
                                globalI = globalPerEdgeLocal - globalI;
                                globalJ = globalPerEdgeLocal - globalJ;
                                dGlobalI = (double)globalI;
                                dGlobalJ = (double)globalJ;
                                globalU = (float)(dGlobalI / dGlobalPerEdge);
                                globalV = (float)(dGlobalJ / dGlobalPerEdge);
                            }
                        }
                    }
                    else
                    {
                        // Non-seam vertices may use the per-tile helper which follows the
                        // canonical mapping for interior lattice points.
                        IcosphereMapping.TileVertexToBarycentricCoordinates(
                            data.id, i, j, res,
                            out globalU, out globalV);
                    }

                    // Store canonical UV for texturing / consistent coordinates
                    _uvs.Add(new Vector2(globalU, globalV));

                    // Map barycentric coords to world direction (IcosphereMapping returns a normalized direction)
                    Vector3 dir = IcosphereMapping.BarycentricToWorldDirection(entry.face, globalU, globalV);

                    // Sample height using the consistent world direction, with resolution passed for detail control
                    if (measureSampling) swSampling.Start();
                    float raw = provider.Sample(in dir, res);
                    if (measureSampling) swSampling.Stop();

                    raw *= config.heightScale;
                    if (raw < rawMin) rawMin = raw;
                    if (raw > rawMax) rawMax = raw;

                    float hSample = raw;
                    float finalR = radius + hSample;

                    minH = Mathf.Min(minH, hSample);
                    maxH = Mathf.Max(maxH, hSample);

                    Vector3 worldVertex = dir * finalR + planetCenter;

                    // For seam vertices, consult the per-builder pass-local shared-vertex map
                    // so adjacent tiles built with the same builder instance get identical
                    // world vertex positions. The key is constructed from face/globalI/globalJ
                    // which is canonical across tiles.
                    if (isSeamVertex)
                    {
                        // Include depth and mesh resolution in the key so vertices sampled
                        // at different LODs or depths don't collide in the per-pass map.
                        // This prevents reuse of a canonical vertex from a previous build
                        // with a different sampling density which would otherwise corrupt
                        // geometry when the builder is reused across unrelated passes.
                        string sharedKey = $"D{data.id.depth}:R{res}:F{entry.face}:I{globalI}:J{globalJ}";
                        if (_passSharedVertexMap.TryGetValue(sharedKey, out var canonical))
                        {
                            // replace with canonical world vertex previously stored
                            if (s_enableSeamDiagnostics && _diagnosticSeamLogs < s_maxSeamLogs)
                            {
                                UnityEngine.Debug.Log($"[SeamDiag] Reusing key={sharedKey} existing={canonical} newCandidate={worldVertex}");
                                _diagnosticSeamLogs++;
                            }
                            worldVertex = canonical;
                        }
                        else
                        {
                            if (s_enableSeamDiagnostics && _diagnosticSeamLogs < s_maxSeamLogs)
                            {
                                UnityEngine.Debug.Log($"[SeamDiag] Registering key={sharedKey} world={worldVertex}");
                                _diagnosticSeamLogs++;
                            }
                            _passSharedVertexMap[sharedKey] = worldVertex;
                        }
                    }

                    _verts.Add(worldVertex);
                    _normals.Add(dir);
                    centerAccum += worldVertex;
                    vertexIndexMap[i, j] = vertCounter;
                    vertCounter++;
                }
            }

            // Build triangles from the triangular lattice using the vertexIndexMap.
            for (int j = 0; j < res - 1; j++)
            {
                int maxI = res - 2 - j; // i such that we have (i+1,j) and (i,j+1)
                for (int i = 0; i <= maxI; i++)
                {
                    int i0 = vertexIndexMap[i, j];
                    int i1 = vertexIndexMap[i + 1, j];
                    int i2 = vertexIndexMap[i, j + 1];
                    if (i0 < 0 || i1 < 0 || i2 < 0) continue;

                    if (measureTriBuild) swTriBuild.Start();
                    Vector3 v0 = _verts[i0]; Vector3 v1 = _verts[i1]; Vector3 v2 = _verts[i2];
                    float area0 = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
                    if (area0 > minTriangleArea)
                    {
                        _tris.Add(i0); _tris.Add(i1); _tris.Add(i2);
                    }
                    else degenerateTriangleCount++;

                    // Add second triangle in the cell when the upper-right vertex exists
                    // which is at (i+1, j+1) for interior cells.
                    if (i + j < res - 2)
                    {
                        int i3 = vertexIndexMap[i + 1, j + 1];
                        if (i3 >= 0)
                        {
                            Vector3 u0 = _verts[i1]; Vector3 u1 = _verts[i3]; Vector3 u2 = _verts[i2];
                            float area1 = Vector3.Cross(u1 - u0, u2 - u0).magnitude * 0.5f;
                            if (area1 > minTriangleArea)
                            {
                                _tris.Add(i1); _tris.Add(i3); _tris.Add(i2);
                            }
                            else degenerateTriangleCount++;
                        }
                    }

                    if (measureTriBuild) swTriBuild.Stop();
                }
            }

            // Ensure triangle winding matches vertex normals (so triangles naturally face away from sphere)
            // Check the geometric normal of the first triangle against the averaged vertex normal;
            // flip winding only when necessary to satisfy the requested outwardNormals parameter.
            if (_tris.Count >= 3 && _normals.Count > 0)
            {
                int aIdx = _tris[0];
                int bIdx = _tris[1];
                int cIdx = _tris[2];
                Vector3 aPos = _verts[aIdx];
                Vector3 bPos = _verts[bIdx];
                Vector3 cPos = _verts[cIdx];
                Vector3 triNormal = Vector3.Cross(bPos - aPos, cPos - aPos).normalized;
                Vector3 avgVertNormal = (_normals[aIdx] + _normals[bIdx] + _normals[cIdx]).normalized;

                // dot > 0 => triangle geometric normal points roughly same direction as vertex normals
                float dot = Vector3.Dot(triNormal, avgVertNormal);

                bool triFacesSameAsVertexNormals = dot >= 0f;

                if (!triFacesSameAsVertexNormals)
                { // just in case
                    FlipTriangleWinding(_tris);
                }
            }

            if (swTotal != null)
            {
                swTotal.Stop();
                // Timing logs removed in production; keep stopwatch usage for local profiling if needed.
            }

            // Use the precomputed registry center as the authoritative tile center.
            // Using the registry's `entry.centerWorld` avoids small per-tile centroid
            // differences introduced by sampling (which grow with resolution) and
            // are the likely cause of tiny seams at higher depths. Keeping the
            // GameObject transform at the registry center ensures adjacent tiles
            // use the same reference when converting world vertices to local-space.
            Vector3 sampledCenter = vertCounter > 0 ? (centerAccum / vertCounter) : entry.centerWorld;
            data.center = entry.centerWorld;

            if (vertCounter > 0)
            {
                data.boundsRadius = 0.5f * ((radius + maxH) - (radius + minH)) + (radius + (minH + maxH) * 0.5f);
            }

            // Convert mesh vertices from world-space to local-space relative to the tile center.
            // Using data.center (the precomputed registry center) as the authoritative
            // GameObject position ensures that TransformPoint(localVerts) == original world verts
            // and adjacent tiles will share exact world-space edge vertices.
            for (int i = 0; i < _verts.Count; i++)
            {
                _verts[i] = _verts[i] - data.center;
            }

            // Note: Do NOT perform an additional centroid recentering here.
            // We already converted vertices to local-space by subtracting data.center above.
            // Any further global shift would change the final world-space vertex positions
            // after the spawned GameObject is placed at data.center and thus break edge continuity
            // between adjacent tiles. Keeping vertices = worldVertex - data.center preserves
            // the exact sampled world positions when the GameObject transform.position == data.center.

            // Create a fresh mesh with corrected vertices
            var mesh = new Mesh();
            mesh.name = $"Tile_{data.id.faceNormal}_d{data.id.depth}";
            mesh.indexFormat = (_verts.Count > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(_verts);
            mesh.SetTriangles(_tris, 0);
            mesh.SetUVs(0, _uvs);

            // Use the precomputed vertex normals collected during sampling instead of
            // calling RecalculateNormals() which is expensive in native code.
            // This keeps normals coherent with the sampling direction (dir) and
            // avoids a costly recompute for each mesh build.
            try
            {
                if (_normals.Count == _verts.Count)
                {
                    mesh.SetNormals(_normals);
                }
                else
                {
                    // Fallback to Unity's recalculation if counts don't match
                    mesh.RecalculateNormals();
                }
            }
            catch
            {
                // If SetNormals throws (unexpected), fallback to safe path
                try { mesh.RecalculateNormals(); } catch { }
            }

            // Avoid the heavy RecalculateBounds call; compute an approximate bounds
            // from the sampled min/max heights which we already track. This is
            // sufficient to keep the mesh renderable and avoids another native pass.
            float maxRad = radius + (maxH == float.MinValue ? 0f : maxH);
            float minRad = radius + (minH == float.MaxValue ? 0f : minH);
            float maxExtent = Mathf.Max(Mathf.Abs(maxRad), Mathf.Abs(minRad));
            var approxSize = Vector3.one * (maxExtent * 2f + 1f);
            mesh.bounds = new Bounds(Vector3.zero, approxSize);

            // Hint that this mesh may be updated frequently to allow the engine to
            // optimize memory and upload paths.
            try { mesh.MarkDynamic(); } catch { }
            // Debug: count normals deviating from radial
            data.mesh = mesh;

            // Populate sampled height ranges so callers and cache entries are correct
            data.minHeight = minH == float.MaxValue ? 0f : minH;
            data.maxHeight = maxH == float.MinValue ? 0f : maxH;
            rawMin = data.minHeight;
            rawMax = data.maxHeight;

            // Cache the produced mesh and sampled range so subsequent builder invocations
            // for the same TileId return the same instance and meaningful rawMin/rawMax.
            try
            {
                if (data != null && data.id.face >= 0)
                {
                    s_meshCache[data.id] = new CachedMeshEntry { mesh = mesh, minH = data.minHeight, maxH = data.maxHeight, centerUsed = data.center, resolutionUsed = data.resolution };
                }
            }
            catch { }
        }
        /// <summary>
        /// Flips the winding order of triangles in the index list (in-place).
        /// </summary>
        private static void FlipTriangleWinding(List<int> tris)
        {
            for (int t = 0; t < tris.Count; t += 3)
            {
                int tmp = tris[t + 1];
                tris[t + 1] = tris[t + 2];
                tris[t + 2] = tmp;
            }
        }
    }
}
