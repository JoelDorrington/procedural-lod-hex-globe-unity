using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem.Core;
using System;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class PlanetTileMeshBuilder
    {
        private static int[,] EmptyVertexLattice(int res) {
            // init lattice for triangle construction
            int[,] lattice = new int[res + 1, res + 1];
            // Initialize to -1 so unused slots are distinguishable when building triangles.
            for (int yy = 0; yy < res + 1; yy++)
            {
                for (int xx = 0; xx < res + 1; xx++)
                {
                    lattice[xx, yy] = -1;
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
        private static bool s_verboseEdgeSampleLogging = false;

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
        public void BuildTileMesh(TileData data)
        {
            if (data == null) Debug.LogError("wtf bro");
            if (data == null) throw new ArgumentNullException("data");
            // Note: cache lookup moved later after we obtain the precomputed 'entry'
            // so we can verify the cached mesh was built with the same center.
            var res = data.resolution;
            var radius = config.baseRadius;
            var planetCenter = this.planetCenter;
            var depth = data.id.depth;

            var registry = new TerrainTileRegistry(depth, radius, planetCenter);
            if (!registry.tiles.ContainsKey(data.id))
            {
                throw new ArgumentException($"TileId {data.id} not found in precomputed registry at depth {depth}");
            }
            var entry = registry.tiles[data.id];

            if (TryGetExistingMesh(entry, res, out var cached))
            {
                data.mesh = cached.mesh;
                data.center = cached.centerUsed;
                return;
            }

            Init(res);

            int degenerateTriangleCount = 0;
            const float minTriangleArea = 0.00000001f;
            var provider = heightProvider ?? config.heightProvider ?? new SimplePerlinHeightProvider();

            // init lattice for triangle construction
            int[,] _vertsMap = EmptyVertexLattice(res);

            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            int j = 0; int i = 0;
            foreach (var bary in IcosphereMapping.TileVertexBarys(res))
            {
                Barycentric global = IcosphereMapping.BaryLocalToGlobal(data.id, bary, res);
                _uvs.Add(global);

                // Sample height at this vertex
                var dir = IcosphereMapping.BaryToWorldDirection(entry.face, global);
                float rawSample = provider.Sample(in dir, res);
                float rawScaled = rawSample * config.heightScale;
                if (rawScaled < minHeight) minHeight = rawScaled;
                if (rawScaled > maxHeight) maxHeight = rawScaled;

                // Add vert
                Vector3 worldVert = dir * (radius + rawScaled) + planetCenter;
                _verts.Add(worldVert);
                _normals.Add(dir);
                _vertsMap[i, j] = _verts.Count - 1; // stash index in lattice
                int maxI = res - 1 - j; // ensure i+j <= res-1 (inside triangle)
                i += 1;
                if (i > maxI)
                {
                    i = 0;
                    j += 1;
                    if (j >= res) break;
                }
            }

            // Build triangles from the lattice using the vertexIndexMap.
            for (int jj = 0; jj < res - 1; jj++)
            {
                int maxI = res - 1 - jj; // ii such that we have (ii+1,jj) and (ii,jj+1)
                for (int ii = 0; ii <= maxI; ii++)
                {
                    int i0 = _vertsMap[ii, jj];
                    int i1 = _vertsMap[ii + 1, jj];
                    int i2 = _vertsMap[ii, jj + 1];
                    if (i0 < 0 || i1 < 0 || i2 < 0) continue;

                    Vector3 v0 = _verts[i0]; Vector3 v1 = _verts[i1]; Vector3 v2 = _verts[i2];
                    float area0 = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
                    if (area0 > minTriangleArea)
                    {
                        _tris.Add(i0); _tris.Add(i1); _tris.Add(i2);
                    }
                    else degenerateTriangleCount++;

                    // Add second triangle in the cell when the upper-right vertex exists
                    // which is at (i+1, j+1) for interior cells.
                    if (ii + jj < res - 2)
                    {
                        int i3 = _vertsMap[ii + 1, jj + 1];
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
                }
            }

            if(degenerateTriangleCount > 0) Debug.Log($"Degenerate triangle count: {degenerateTriangleCount} (out of {_tris.Count / 3} total)");

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

            data.center = entry.centerWorld;

            // Convert mesh vertices from world-space to local-space relative to the tile center.
            // Using data.center (the precomputed registry center) as the authoritative
            // GameObject position ensures that TransformPoint(localVerts) == original world verts
            // and adjacent tiles will share exact world-space edge vertices.
            for (int vrtIdx = 0; vrtIdx < _verts.Count; vrtIdx++)
            {
                _verts[vrtIdx] = _verts[vrtIdx] - data.center;
            }

            // Create mesh
            var mesh = new Mesh();
            mesh.name = $"Tile_{data.id.faceNormal}_d{data.id.depth}";
            mesh.indexFormat = (_verts.Count > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(_verts);
            mesh.SetTriangles(_tris, 0);
            mesh.SetUVs(0, _uvs);

            // Avoid the heavy RecalculateBounds call; compute an approximate bounds
            // from the sampled min/max heights which we already track. This is
            // sufficient to keep the mesh renderable and avoids another native pass.
            float maxRad = radius + (maxHeight == float.MinValue ? 0f : maxHeight);
            float minRad = radius + (minHeight == float.MaxValue ? 0f : minHeight);
            float maxExtent = Mathf.Max(Mathf.Abs(maxRad), Mathf.Abs(minRad));
            var approxSize = Vector3.one * (maxExtent * 2f + 1f);
            mesh.bounds = new Bounds(Vector3.zero, approxSize);

            // Use the normals collected during sampling instead of calling RecalculateNormals() 
            // which is expensive in native code. This keeps normals coherent with the sampling
            //  direction (dir) and avoids a costly recompute for each mesh build.
            try
            {
                if (_normals.Count != _verts.Count)
                {
                    throw new Exception($"Normal count mismatch: {_normals.Count} != {_verts.Count}");
                }
                mesh.SetNormals(_normals);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
                try { mesh.RecalculateNormals(); } catch { }
            }

            data.mesh = mesh;

            // Cache the produced mesh
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
