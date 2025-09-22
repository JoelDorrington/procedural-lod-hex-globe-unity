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
            // Set canonical tile center on the TileData so callers (and cache) have
            // the authoritative center available. Mesh vertices will be stored in
            // local space relative to this center.
            data.center = entry.centerWorld;
            Debug.Log($"[DIAGNOSTIC] Tile {data.id}: Set data.center = {data.center} from registry");
            
            // DIAGNOSTIC: Compare registry center with what we'd compute for tile center
            var tileCenterBary = IcosphereMapping.TileIndexToBaryCenter(data.id.depth, data.id.x, data.id.y);
            var tileCenterDir = IcosphereMapping.BaryToWorldDirection(entry.face, tileCenterBary);
            var computedCenter = tileCenterDir * radius + planetCenter;
            Debug.Log($"[DIAGNOSTIC] Tile {data.id}: Registry center={data.center}, computed center={computedCenter}, diff={(data.center - computedCenter).magnitude:F6}");
            Debug.Log($"[DIAGNOSTIC] Tile {data.id}: tileCenterBary={tileCenterBary}, tileCenterDir={tileCenterDir}");

            if (TryGetExistingMesh(entry, res, out var cached))
            {
                data.mesh = cached.mesh;
                data.center = cached.centerUsed;
                Debug.Log($"[DIAGNOSTIC] Tile {data.id}: Using cached mesh, data.center changed to {data.center}");
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
            foreach (var bary in IcosphereMapping.TileVertexBarys(res))
            {
                Barycentric global = IcosphereMapping.BaryLocalToGlobal(data.id, bary, res);
                _uvs.Add(global);

                // Sample height at this vertex
                var dir = IcosphereMapping.BaryToWorldDirection(entry.face, global);
                
                // DIAGNOSTIC: Log first few vertices to see barycentric coords
                if (_verts.Count < 3)
                {
                    Debug.Log($"[BARY] Tile {data.id} vertex {_verts.Count}: local={bary}, global={global}, dir={dir}");
                }

                float rawSample = provider.Sample(in dir, res);
                float rawScaled = rawSample * config.heightScale;
                if (rawScaled < minHeight) minHeight = rawScaled;
                if (rawScaled > maxHeight) maxHeight = rawScaled;

                // Add vert (store as local position relative to tile center)
                Vector3 worldVert = dir * (radius + rawScaled) + planetCenter;
                Vector3 localVert = worldVert - data.center;
                _verts.Add(localVert);
                
                // DIAGNOSTIC: Log first few vertices to verify local vs world computation
                if (_verts.Count <= 3)
                {
                    Debug.Log($"[VERTEX] Tile {data.id} vertex {_verts.Count-1}: worldVert={worldVert}, data.center={data.center}, localVert={localVert}, addedToList={_verts[_verts.Count-1]}");
                }
                // Store an outward-pointing normal (world space relative to planet center).
                // Since the mesh will be placed at `data.center` with identity rotation,
                // this world-space normal is valid as the mesh-local normal. If the
                // GameObject gains rotation/scale, Unity will transform the normal.
                _normals.Add((worldVert - planetCenter).normalized);
                // Use the bary's integer lattice indices (i,j) as returned by TileVertexBarys
                int gi = Mathf.RoundToInt(bary.U);
                int gj = Mathf.RoundToInt(bary.V);
                // stash index in lattice keyed by the local integer coordinates
                if (gi >= 0 && gj >= 0 && gi < _vertsMap.GetLength(0) && gj < _vertsMap.GetLength(1))
                {
                    _vertsMap[gi, gj] = _verts.Count - 1;
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

            // DIAGNOSTIC: Check if vertices are mesh-local (average should be near zero)
            if (_verts.Count > 0)
            {
                Vector3 avgVert = Vector3.zero;
                foreach (var v in _verts) avgVert += v;
                avgVert /= _verts.Count;
                Debug.Log($"[DIAGNOSTIC] Tile {data.id}: Built {_verts.Count} vertices, average position = {avgVert}, magnitude = {avgVert.magnitude:F6}");
                Debug.Log($"[DIAGNOSTIC] Tile {data.id}: data.center = {data.center}, planetCenter = {planetCenter}");
                
                // CRITICAL FIX: The vertices are being mapped to the wrong area of the triangle.
                // Instead of using the registry center (which is based on tile center bary),
                // compute the actual geometric center of the mesh vertices in world space
                // and use that as the tile center. This ensures the mesh is truly local.
                Vector3 meshWorldCenter = Vector3.zero;
                foreach (var localVert in _verts)
                {
                    Vector3 worldVert = localVert + data.center;
                    meshWorldCenter += worldVert;
                }
                meshWorldCenter /= _verts.Count;
                
                Debug.Log($"[FIX] Tile {data.id}: Computed mesh world center = {meshWorldCenter}");
                Debug.Log($"[FIX] Tile {data.id}: Original data.center = {data.center}");
                Debug.Log($"[FIX] Tile {data.id}: Center difference = {(meshWorldCenter - data.center).magnitude:F6}");
                
                // Update data.center to the actual mesh center and recompute all vertices as local
                data.center = meshWorldCenter;
                for (int i = 0; i < _verts.Count; i++)
                {
                    // Convert back to world, then to local relative to new center
                    Vector3 worldVert = _verts[i] + entry.centerWorld; // original world position
                    Vector3 newLocalVert = worldVert - data.center;
                    _verts[i] = newLocalVert;
                }
                
                // Verify the fix
                Vector3 newAvgVert = Vector3.zero;
                foreach (var v in _verts) newAvgVert += v;
                newAvgVert /= _verts.Count;
                Debug.Log($"[FIX] Tile {data.id}: After fix, average vertex = {newAvgVert}, magnitude = {newAvgVert.magnitude:F6}");
                
                if (avgVert.magnitude > 1e-3f)
                {
                    Debug.LogWarning($"[DIAGNOSTIC] Tile {data.id}: Average vertex magnitude {avgVert.magnitude:F6} suggests non-local vertices!");
                }
            }

            // Ensure triangle winding matches vertex normals (so triangles naturally face away from sphere)
            // Check the geometric normal of the first triangle against the averaged vertex normal;
            // flip winding only when necessary to satisfy the requested outwardNormals parameter.
            if (_tris.Count >= 3)
            {
                int aIdx = _tris[0];
                int bIdx = _tris[1];
                int cIdx = _tris[2];
                Vector3 aPos = _verts[aIdx];
                Vector3 bPos = _verts[bIdx];
                Vector3 cPos = _verts[cIdx];
                // Compute triangle geometric normal in mesh-local space (aPos/bPos/cPos are local verts)
                Vector3 triNormal = Vector3.Cross(bPos - aPos, cPos - aPos).normalized;

                // Compute the averaged vertex normal using the per-vertex normals we sampled earlier.
                // Convert the sampled outward normals to mesh-local frame is unnecessary here because
                // we sampled them consistently with mesh-local axes (no rotation assumed). Use them directly.
                Vector3 avgVertNormal = (_normals[aIdx] + _normals[bIdx] + _normals[cIdx]).normalized;

                // If triangle geometric normal points opposite to averaged vertex normal,
                // flip triangle winding and invert all normals so they remain outward-facing.
                float dot = Vector3.Dot(triNormal, avgVertNormal);
                if (dot < 0f)
                {
                    FlipTriangleWinding(_tris);
                    for (int ni = 0; ni < _normals.Count; ni++) _normals[ni] = -_normals[ni];
                }
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
