using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class PlanetTileMeshBuilder
    {
        private readonly TerrainConfig config;
        private readonly TerrainHeightProviderBase heightProvider;
        private readonly OctaveMaskHeightProvider octaveWrapper;
        private readonly int bakedDepth;
        private readonly float splitChildResolutionMultiplier;
        private readonly float childHeightEnhancement;
        private readonly bool _edgePromotionRebuild;

        // Temporary lists for mesh generation
        private readonly List<Vector3> _verts = new();
        private readonly List<int> _tris = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _uvs = new();

        public PlanetTileMeshBuilder(
            TerrainConfig config,
            TerrainHeightProviderBase heightProvider,
            OctaveMaskHeightProvider octaveWrapper,
            int bakedDepth,
            float splitChildResolutionMultiplier,
            float childHeightEnhancement,
            bool edgePromotionRebuild)
        {
            this.config = config;
            this.heightProvider = heightProvider;
            this.octaveWrapper = octaveWrapper;
            this.bakedDepth = bakedDepth;
            this.splitChildResolutionMultiplier = splitChildResolutionMultiplier;
            this.childHeightEnhancement = childHeightEnhancement;
            this._edgePromotionRebuild = edgePromotionRebuild;
        }

        /// <summary>
        /// Builds the tile mesh and allows caller to specify triangle winding direction.
        /// </summary>
        /// <param name="data">Tile data to populate</param>
        /// <param name="rawMin">Minimum sampled height</param>
        /// <param name="rawMax">Maximum sampled height</param>
        /// <param name="outwardNormals">If true, flip triangles for outward-facing normals; if false, leave as-is for inward-facing normals.</param>
        public void BuildTileMesh(TileData data, ref float rawMin, ref float rawMax, bool outwardNormals = true)
        {
            Debug.Log($"[TileMeshBuilder] Building mesh for tile: {data.id} with resolution {data.resolution}");

            _verts.Clear();
            _tris.Clear();
            _normals.Clear();
            _uvs.Clear();

            int res = data.resolution;
            float inv = 1f / (res - 1);
            float radius = config.baseRadius;
            float minH = float.MaxValue;
            float maxH = float.MinValue;
            Vector3 centerAccum = Vector3.zero;
            int vertCounter = 0;
            bool doCull = config.cullBelowSea && !config.debugDisableUnderwaterCulling;
            bool removeTris = doCull && config.removeFullySubmergedTris;
            float seaR = config.baseRadius + config.seaLevel;
            float eps = config.seaClampEpsilon;
            var submergedFlags = removeTris ? new List<bool>(res * res) : null;

            for (int j = 0; j < res; j++)
            {
                for (int i = 0; i < res; i++)
                {
                    float u = (i * inv + data.id.x) / (1 << data.id.depth);
                    float v = (j * inv + data.id.y) / (1 << data.id.depth);
                    Vector3 dir = CubeSphere.FaceLocalToUnit(data.id.face, u * 2f - 1f, v * 2f - 1f);
                    _uvs.Add(new Vector2(u, v));

                    float raw = heightProvider != null ? heightProvider.Sample(in dir, res) : 0f;
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
                    _verts.Add(dir * finalR);
                    _normals.Add(dir);
                    centerAccum += dir * finalR;
                    vertCounter++;
                    if (removeTris) submergedFlags.Add(submerged);
                }
            }

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
                        if (sub0 && sub1 && sub2 && sub3)
                            continue;
                    }
                    _tris.Add(i0); _tris.Add(i2); _tris.Add(i1);
                    _tris.Add(i1); _tris.Add(i2); _tris.Add(i3);
                }
            }

            // Fast triangle winding correction: flip if outwardNormals is true
            if (outwardNormals)
            {
                FlipTriangleWinding(_tris);
            }
            var mesh = new Mesh();
            mesh.indexFormat = (res * res > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(_verts);
            mesh.SetTriangles(_tris, 0, true);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            if (config.recalcNormals)
            {
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();

            data.mesh = mesh;
            data.minHeight = minH;
            data.maxHeight = maxH;
            data.error = maxH - minH;
            if (vertCounter > 0)
            {
                data.center = centerAccum / vertCounter;
                data.boundsRadius = 0.5f * ((radius + maxH) - (radius + minH)) + (radius + (minH + maxH) * 0.5f);
            }
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

        private float SampleRawWithOctaveCap(Vector3 dir, int maxOctave)
        {
            if (heightProvider is IOctaveSampler sampler && maxOctave >= 0)
                return sampler.SampleOctaveMasked(dir, maxOctave);
            // Use bakedDepth as a fallback for resolution, or pass a suitable value
            int resolution = bakedDepth > 0 ? bakedDepth : 1;
            return heightProvider != null ? heightProvider.Sample(in dir, resolution) : 0f;
        }
    }
}
