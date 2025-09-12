using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.Util;
using System;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class PlanetTileMeshBuilder
    {
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
            bool doCull = config.cullBelowSea && !config.debugDisableUnderwaterCulling;
            bool removeTris = doCull && config.removeFullySubmergedTris;
            float seaR = radius + config.seaLevel;
            float eps = config.seaClampEpsilon;
            var submergedFlags = removeTris ? new List<bool>(res * res) : null;

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
                    // Use icosahedral barycentric coordinate system (canonical global u/v)
                    IcosphereMapping.TileVertexToBarycentricCoordinates(
                        data.id, i, j, res,
                        out float globalU, out float globalV);

                    // Store canonical UV for texturing / consistent coordinates
                    _uvs.Add(new Vector2(globalU, globalV));

                    // Map directly from canonical barycentric coords to world direction to avoid distortion
                    Vector3 dir = IcosphereMapping.BarycentricToWorldDirection(entry.face, globalU, globalV).normalized;

                    // Sample height using the consistent world direction, with resolution passed for detail control
                    if (swSampling != null) swSampling.Start();
                    float raw = provider.Sample(in dir, res);
                    if (swSampling != null) swSampling.Stop();

                    raw *= config.heightScale;
                    if (raw < rawMin) rawMin = raw;
                    if (raw > rawMax) rawMax = raw;

                    float hSample = raw;
                    float finalR = radius + hSample;

                    if (config.shorelineDetail && data.id.depth >= config.shorelineDetailMinDepth)
                    {
                        float seaRLocal = config.baseRadius + config.seaLevel;
                        if (Mathf.Abs(finalR - seaRLocal) <= config.shorelineBand)
                        {
                            Vector3 sp = dir * config.shorelineDetailFrequency + new Vector3(12.345f, 45.67f, 89.01f);
                            float n = Mathf.PerlinNoise(sp.x, sp.y) * 2f - 1f;
                            float bandT = 1f - Mathf.Clamp01(Mathf.Abs(finalR - seaRLocal) / Mathf.Max(0.0001f, config.shorelineBand));
                            float add = n * config.shorelineDetailAmplitude * bandT;
                            if (config.shorelinePreserveSign)
                            {
                                float before = finalR - seaRLocal; float after = before + add;
                                if (Mathf.Sign(before) != 0 && Mathf.Sign(after) != Mathf.Sign(before)) add *= 0.3f;
                            }
                            finalR += add; hSample = finalR - radius;
                        }
                    }

                    bool submerged = doCull && finalR < seaR;
                    if (submerged && !removeTris)
                        finalR = seaR + eps;

                    minH = Mathf.Min(minH, hSample);
                    maxH = Mathf.Max(maxH, hSample);

                    Vector3 worldVertex = dir * finalR + planetCenter;

                    _verts.Add(worldVertex);
                    _normals.Add(dir);
                    centerAccum += worldVertex;
                    vertexIndexMap[i, j] = vertCounter;
                    vertCounter++;
                    if (removeTris) submergedFlags.Add(submerged);
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

                    if (removeTris)
                    {
                        bool sub0 = submergedFlags[i0];
                        bool sub1 = submergedFlags[i1];
                        bool sub2 = submergedFlags[i2];
                        if (sub0 && sub1 && sub2) continue;
                    }

                    if (swTriBuild != null) swTriBuild.Start();
                    Vector3 v0 = _verts[i0]; Vector3 v1 = _verts[i1]; Vector3 v2 = _verts[i2];
                    float area0 = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
                    if (area0 > 1e-7f)
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
                            if (removeTris)
                            {
                                bool sub1 = submergedFlags[i1];
                                bool sub2b = submergedFlags[i2];
                                bool sub3 = submergedFlags[i3];
                                if (sub1 && sub2b && sub3) { if (swTriBuild != null) swTriBuild.Stop(); continue; }
                            }

                            Vector3 u0 = _verts[i1]; Vector3 u1 = _verts[i3]; Vector3 u2 = _verts[i2];
                            float area1 = Vector3.Cross(u1 - u0, u2 - u0).magnitude * 0.5f;
                            if (area1 > 1e-7f)
                            {
                                _tris.Add(i1); _tris.Add(i3); _tris.Add(i2);
                            }
                            else degenerateTriangleCount++;
                        }
                    }

                    if (swTriBuild != null) swTriBuild.Stop();
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

            // Use the sampled world-space centroid as the authoritative tile center when
            // possible. This ensures that when we convert vertices to local-space
            // (subtracting the center) and later place the GameObject at data.center,
            // the mesh's world-space centroid will equal data.center. If sampling
            // failed or produced no vertices, fall back to the precomputed registry center.
            Vector3 sampledCenter = vertCounter > 0 ? (centerAccum / vertCounter) : entry.centerWorld;
            data.center = sampledCenter;

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

            // Recalculate normals from triangle geometry. The previous square-grid
            // neighbor-based computation assumed a full rectangular grid and caused
            // indexing errors after switching to a triangular lattice. Using
            // Unity's RecalculateNormals() on the mesh triangles produces robust
            // per-vertex normals that align with the actual triangle topology.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // Force the mesh bounds center to origin so the mesh's world-centroid
            // equals the GameObject position (data.center) without modifying vertex positions.
            var b = mesh.bounds;
            mesh.bounds = new Bounds(Vector3.zero, b.size);
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
