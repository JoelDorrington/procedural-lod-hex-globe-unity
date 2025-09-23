using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem.Core;
using System;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class PlanetTileMeshBuilder
    {
        public static T[,] EmptyVertexLattice<T>(int res, T defaultValue = default) {
            // init lattice for triangle construction
            T[,] lattice = new T[res + 1, res + 1];
            // Initialize to -1 so unused slots are distinguishable when building triangles.
            for (int yy = 0; yy < res + 1; yy++)
            {
                for (int xx = 0; xx < res + 1; xx++)
                {
                    lattice[xx, yy] = defaultValue;
                }
            }
            return lattice;
        }

        // Cache sampled edge vertices so adjacent tiles reuse the exact same
        // world-space vertex and normal. Keyed by a compact struct to avoid
        // per-vertex string allocations which were hurting performance.
        private struct EdgeSampleKey : IEquatable<EdgeSampleKey>
        {
            public int face;
            public int gi;
            public int gj;
            public int depth;
            public int res;

            public EdgeSampleKey(int face, int gi, int gj, int depth, int res)
            {
                this.face = face; this.gi = gi; this.gj = gj; this.depth = depth; this.res = res;
            }

            public bool Equals(EdgeSampleKey other)
            {
                return face == other.face && gi == other.gi && gj == other.gj && depth == other.depth && res == other.res;
            }

            public override bool Equals(object obj) => obj is EdgeSampleKey k && Equals(k);

            public override int GetHashCode()
            {
                // Pack fields into a single hash. Use primes to spread bits.
                unchecked
                {
                    int h = 17;
                    h = h * 31 + face;
                    h = h * 31 + gi;
                    h = h * 31 + gj;
                    h = h * 31 + depth;
                    h = h * 31 + res;
                    return h;
                }
            }
        }

    // NOTE: edge sample cache removed to avoid timing/order-dependent seams.
        // Toggle verbose per-vertex logging. Keep false by default to avoid perf hit; enable manually for debugging.
        private static bool s_verboseEdgeSampleLogging = true;

        /// <summary>
        /// Public accessor so editor scripts can toggle verbose edge-sample logging
        /// without editing library code directly.
        /// </summary>
        public static bool VerboseEdgeSampleLogging
        {
            get => s_verboseEdgeSampleLogging;
            set => s_verboseEdgeSampleLogging = value;
        }
        // Simple cache so repeated builds for the same TileId return the same Mesh instance.
        // Cache both the Mesh and the sampled raw height range so callers that pass
        // ref rawMin/rawMax receive meaningful values even when the mesh is reused.
        private struct CachedMeshEntry
        {
            public Mesh mesh;
            public Vector3 centerUsed;
            public int resolutionUsed;
        }
        private static readonly Dictionary<TileId, CachedMeshEntry> s_meshCache = new();

        /// <summary>
        /// Clears the static mesh cache. Public so editor code or tests may call it.
        /// The cache is also cleared automatically on game start by the
        /// <see cref="ClearCacheOnGameStart"/> method which is decorated with
        /// RuntimeInitializeOnLoadMethod.
        /// </summary>
        public static void ClearCache()
        {
            try { s_meshCache.Clear(); } catch { }
        }

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

        private void Init(int resolution)
        {
            _verts.Clear();
            _tris.Clear();
            _normals.Clear();
            _uvs.Clear();

            // Reserve capacity to avoid repeated reallocations for large resolutions
            // Use triangular lattice vertex count: res * (res + 1) / 2
            int expectedVertsCap = resolution * (resolution + 1) / 2;
            if (_verts.Capacity < expectedVertsCap) _verts.Capacity = expectedVertsCap;
            if (_normals.Capacity < expectedVertsCap) _normals.Capacity = expectedVertsCap;
            if (_uvs.Capacity < expectedVertsCap) _uvs.Capacity = expectedVertsCap;
        }

        private bool TryGetExistingMesh(PrecomputedTileEntry entry, int resolution, out CachedMeshEntry foundEntry)
        {
            // Return cached mesh instance when available to keep reference equality stable.
            var id = new TileId(entry.face, entry.x, entry.y, entry.depth);
            if (s_meshCache.TryGetValue(id, out var existing))
            {
                // Guard against Unity 'destroyed' objects being present in the static cache
                // (happens in test runs where teardown can destroy created Meshes). If the
                // cached Mesh has been destroyed, drop the cache entry and fall through to
                // rebuilding a fresh mesh.
                if (existing.mesh == null)
                {
                    try { s_meshCache.Remove(id); } catch { }
                }
                else if (existing.mesh.name != null && existing.mesh.name.EndsWith("_local"))
                {
                    // Some older code paths created a "_local" clone and it may have been
                    // cached accidentally; treat such cache entries as stale and rebuild.
                    try { s_meshCache.Remove(id); } catch { }
                }
                else if ((existing.centerUsed - entry.centerWorld).sqrMagnitude > 1e-6f)
                {
                    // Cached mesh was built with a different center than the current
                    // precomputed registry entry. Discard stale cache so we rebuild
                    // with the authoritative center and avoid doubled offsets.
                    try { s_meshCache.Remove(id); } catch { }
                }
                else if (existing.resolutionUsed != resolution)
                {
                    // Cached mesh was built with a different sampling resolution. Invalidate cache
                    // so callers requesting a different density get a correctly sized mesh.
                    try { s_meshCache.Remove(id); } catch { }
                }
                else
                {
                    // Populate data and the ref outputs so callers relying on sampled
                    // ranges (rawMin/rawMax) get correct values even when the mesh
                    // was built earlier.
                    foundEntry = existing;
                    return true;
                }
            }
            foundEntry = default;
            return false;
        }

        /// <summary>
        /// Builds the tile mesh and allows caller to specify triangle winding direction.
        /// </summary>
        /// <param name="data">Tile data to populate</param>
        public void BuildTileMesh(TileData data, TerrainTileRegistry registry = null)
        {
            if (data == null) Debug.LogError("wtf bro");
            if (data == null) throw new ArgumentNullException("data");
            // Note: cache lookup moved later after we obtain the precomputed 'entry'
            // so we can verify the cached mesh was built with the same center.
            var res = data.resolution;
            var radius = config.baseRadius;
            var planetCenter = this.planetCenter;
            var depth = data.id.depth;

            if(registry == null) registry = new TerrainTileRegistry(depth, radius, planetCenter);
            if (!registry.tiles.ContainsKey(data.id))
            {
                throw new ArgumentException($"TileId {data.id} not found in precomputed registry at depth {depth}");
            }
            var entry = registry.tiles[data.id];
            data.center = entry.centerWorld;
            if (TryGetExistingMesh(entry, res, out var cached))
            {
                data.mesh = cached.mesh;
                data.center = cached.centerUsed;
                return;
            }

            Init(res);

            int degenerateTriangleCount = 0;
            const float minTriangleArea = 1e-8f;
            var provider = heightProvider ?? config.heightProvider ?? new SimplePerlinHeightProvider();

            // init lattice for triangle construction
            int[,] _vertsMap = EmptyVertexLattice(res, -1);

            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            var i = 0; var j = 0;
            foreach (var bary in IcosphereMapping.TileVertexBarys(res, depth, entry.x, entry.y))
            {
                // Sample height at this vertex
                var dir = IcosphereMapping.BaryToWorldDirection(entry.face, bary);
                _uvs.Add(bary);

                float rawSample = provider.Sample(in dir, res);
                float rawScaled = rawSample * config.heightScale;
                if (rawScaled < minHeight) minHeight = rawScaled;
                if (rawScaled > maxHeight) maxHeight = rawScaled;

                // Generate vertex position
                var worldVert = (dir * radius) + (dir * rawScaled) + planetCenter;
                var localVert = worldVert - data.center;

                // Add vertex then record its index in the lattice map so the index
                // points to the newly appended vertex.
                _verts.Add(localVert); // localize to tile center
                _normals.Add((worldVert - planetCenter).normalized);
                _vertsMap[i, j] = _verts.Count - 1;
                i++;
                if (i >= res - j)
                {
                    i = 0;
                    j++;
                }
            }

            // Build triangles from the lattice using the authoritative _vertsMap
            // that was filled while constructing vertices (dual-counter ordering).
            for (int jj = 0; jj < res - 1; jj++)
            {
                int maxI = res - 1 - jj; // ii such that we have (ii+1,jj) and (ii,jj+1)
                for (int ii = 0; ii <= maxI; ii++)
                {
                    int i0 = _vertsMap[ii, jj];
                    int i1 = _vertsMap[ii + 1, jj];
                    int i2 = _vertsMap[ii, jj + 1];
                    if (i0 < 0 || i1 < 0 || i2 < 0) continue;
                    if (i0 >= _verts.Count || i1 >= _verts.Count || i2 >= _verts.Count) continue;

                    Vector3 v0 = _verts[i0]; Vector3 v1 = _verts[i1]; Vector3 v2 = _verts[i2];
                    float area0 = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
                    if (area0 > minTriangleArea)
                    {
                        _tris.Add(i0); _tris.Add(i1); _tris.Add(i2);
                    }
                    else degenerateTriangleCount++;

                    // Add second triangle in the cell when the upper-right vertex exists
                    // which is at (ii+1, jj+1) for interior cells.
                    if (ii + jj < res - 2)
                    {
                        int i3 = _vertsMap[ii + 1, jj + 1];
                        if (i3 >= 0 && i3 < _verts.Count)
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
                }
            }

            if(degenerateTriangleCount > 0) Debug.Log($"Degenerate triangle count: {degenerateTriangleCount} (out of {_tris.Count / 3} total)");

            // Ensure triangle winding matches sampled vertex normals (so triangles face outward).
            // Instead of flipping all triangles based on the first triangle, check each
            // triangle individually and swap its winding when it is inverted. Keep the
            // per-vertex sampled normals unchanged.
            for (int t = 0; t < _tris.Count; t += 3)
            {
                int ai = _tris[t];
                int bi = _tris[t + 1];
                int ci = _tris[t + 2];
                Vector3 aPos = _verts[ai];
                Vector3 bPos = _verts[bi];
                Vector3 cPos = _verts[ci];
                Vector3 worldA = aPos + data.center;
                Vector3 worldB = bPos + data.center;
                Vector3 worldC = cPos + data.center;
                Vector3 triNormalWorld = Vector3.Cross(worldB - worldA, worldC - worldA).normalized;
                Vector3 avgVertNormal = (_normals[ai] + _normals[bi] + _normals[ci]).normalized;
                if (Vector3.Dot(triNormalWorld, avgVertNormal) < 0f)
                {
                    // Swap winding for this triangle (bi <-> ci)
                    _tris[t + 1] = ci;
                    _tris[t + 2] = bi;
                }
            }

            // Optional diagnostic: compute triangle area distribution and log details
            // when there are gross outliers. This runs only when verbose logging
            // is enabled to avoid spamming normal runs.
            if (s_verboseEdgeSampleLogging && _tris.Count >= 3)
            {
                var areas = new List<float>(_tris.Count / 3);
                for (int t = 0; t < _tris.Count; t += 3)
                {
                    Vector3 a = _verts[_tris[t]];
                    Vector3 b = _verts[_tris[t + 1]];
                    Vector3 c = _verts[_tris[t + 2]];
                    float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                    areas.Add(area);
                }
                areas.Sort();
                float median = areas[areas.Count / 2];
                float max = areas[areas.Count - 1];
                if (median > 0f && max / median > 8.0f)
                {
                    // Find the offending triangle index
                    int offendingTri = -1;
                    for (int t = 0; t < _tris.Count; t += 3)
                    {
                        Vector3 a = _verts[_tris[t]];
                        Vector3 b = _verts[_tris[t + 1]];
                        Vector3 c = _verts[_tris[t + 2]];
                        float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                        if (Mathf.Approximately(area, max)) { offendingTri = t; break; }
                    }
                    if (offendingTri >= 0)
                    {
                        int ai = _tris[offendingTri];
                        int bi = _tris[offendingTri + 1];
                        int ci = _tris[offendingTri + 2];
                        var baryA = _uvs[ai]; var baryB = _uvs[bi]; var baryC = _uvs[ci];
                        Vector3 worldA = _verts[ai] + data.center;
                        Vector3 worldB = _verts[bi] + data.center;
                        Vector3 worldC = _verts[ci] + data.center;
                        // Additionally locate lattice coordinates for these vertex indices by
                        // scanning the authoritative _vertsMap. This helps detect whether
                        // map entries pointed to wrong indices or were uninitialized.
                        (int li, int lj) aL = (-1, -1);
                        (int li, int lj) bL = (-1, -1);
                        (int li, int lj) cL = (-1, -1);
                        for (int y = 0; y <= res; y++)
                        {
                            for (int x = 0; x <= res; x++)
                            {
                                if (x < _vertsMap.GetLength(0) && y < _vertsMap.GetLength(1))
                                {
                                    int idx = _vertsMap[x, y];
                                    if (idx == ai) aL = (x, y);
                                    if (idx == bi) bL = (x, y);
                                    if (idx == ci) cL = (x, y);
                                }
                            }
                        }

                        // Collect neighboring map entries around each found lattice coord
                        string NeighborDump((int x, int y) p)
                        {
                            if (p.x < 0 || p.y < 0) return "(not found)";
                            var sb = new System.Text.StringBuilder();
                            for (int oy = -1; oy <= 1; oy++)
                            {
                                for (int ox = -1; ox <= 1; ox++)
                                {
                                    int nx = p.x + ox;
                                    int ny = p.y + oy;
                                    if (nx >= 0 && nx < _vertsMap.GetLength(0) && ny >= 0 && ny < _vertsMap.GetLength(1))
                                        sb.AppendFormat("{0},", _vertsMap[nx, ny]);
                                    else sb.Append("_,");
                                }
                                sb.Append(" | ");
                            }
                            return sb.ToString();
                        }

                        // Recompute directions from stored bary values and log them to
                        // detect whether a stored bary or face selection produced the
                        // unexpected world position.
                        var storedBaryA = _uvs[ai];
                        var storedBaryB = _uvs[bi];
                        var storedBaryC = _uvs[ci];
                        Vector3 dirA = IcosphereMapping.BaryToWorldDirection(entry.face, new Barycentric(storedBaryA.x, storedBaryA.y));
                        Vector3 dirB = IcosphereMapping.BaryToWorldDirection(entry.face, new Barycentric(storedBaryB.x, storedBaryB.y));
                        Vector3 dirC = IcosphereMapping.BaryToWorldDirection(entry.face, new Barycentric(storedBaryC.x, storedBaryC.y));
                        Vector3 recomWorldA = dirA * radius + dirA * provider.Sample(in dirA, res) + planetCenter;
                        Vector3 recomWorldB = dirB * radius + dirB * provider.Sample(in dirB, res) + planetCenter;
                        Vector3 recomWorldC = dirC * radius + dirC * provider.Sample(in dirC, res) + planetCenter;

                        Debug.LogWarning($"Large-triangle outlier on Tile {data.id.faceNormal} d{data.id.depth} res={res}: max/median={max/median:0.###} \n" +
                            $"TriIndices=({ai},{bi},{ci}) bary=({baryA.x:0.###},{baryA.y:0.###})/({baryB.x:0.###},{baryB.y:0.###})/({baryC.x:0.###},{baryC.y:0.###}) \n" +
                            $"WorldA={worldA} WorldB={worldB} WorldC={worldC} \n" +
                            $"RecomputedWorldA={recomWorldA} RecomputedWorldB={recomWorldB} RecomputedWorldC={recomWorldC} \n" +
                            $"StoredBaryA=({storedBaryA.x:0.###},{storedBaryA.y:0.###}) StoredBaryB=({storedBaryB.x:0.###},{storedBaryB.y:0.###}) StoredBaryC=({storedBaryC.x:0.###},{storedBaryC.y:0.###}) \n" +
                            $"LatticeA=({aL.Item1},{aL.Item2}) neighbors={NeighborDump(aL)} \n" +
                            $"LatticeB=({bL.Item1},{bL.Item2}) neighbors={NeighborDump(bL)} \n" +
                            $"LatticeC=({cL.Item1},{cL.Item2}) neighbors={NeighborDump(cL)}");

                        // Dump nearby vertex entries around the offending indices to
                        // detect whether the _verts list contains misordered or corrupt entries.
                        int minIdx = Mathf.Max(0, Math.Min(Math.Min(ai, bi), ci) - 8);
                        int maxIdx = Mathf.Min(_verts.Count - 1, Math.Max(Math.Max(ai, bi), ci) + 8);
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine($"Vertex dump [{minIdx}..{maxIdx}] (_verts.Count={_verts.Count}):");
                        for (int k = minIdx; k <= maxIdx; k++)
                        {
                            var uv = (k < _uvs.Count) ? _uvs[k] : new Vector2(float.NaN, float.NaN);
                            var local = (k < _verts.Count) ? _verts[k] : Vector3.zero;
                            var wpos = local + data.center;
                            sb.AppendFormat("idx={0} uv=({1:0.###},{2:0.###}) world=({3:0.##},{4:0.##},{5:0.##})\n", k, uv.x, uv.y, wpos.x, wpos.y, wpos.z);
                        }
                        Debug.LogWarning(sb.ToString());
                    }
                    else
                    {
                        Debug.LogWarning($"Large-triangle outlier detected on Tile {data.id.faceNormal} d{data.id.depth} res={res}: max/median={max/median:0.###} but offending triangle not found by exact match.");
                    }
                }
            }

            // Create mesh
            var mesh = new Mesh();
            mesh.name = $"Tile_{data.id.faceNormal}_d{data.id.depth}";
            mesh.indexFormat = (_verts.Count > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetTriangles(_tris, 0);
            mesh.SetUVs(0, _uvs);
            
            float maxRad = radius + (maxHeight == float.MinValue ? 0f : maxHeight);
            float minRad = radius + (minHeight == float.MaxValue ? 0f : minHeight);
            float maxExtent = Mathf.Max(Mathf.Abs(maxRad), Mathf.Abs(minRad));
            var approxSize = Vector3.one * (maxExtent * 2f + 1f);
            mesh.bounds = new Bounds(Vector3.zero, approxSize);

            data.mesh = mesh;
            s_meshCache[data.id] = new CachedMeshEntry { mesh = mesh, centerUsed = data.center, resolutionUsed = data.resolution };
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
