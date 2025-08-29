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

        public void BuildTileMesh(TileData data, ref float rawMin, ref float rawMax)
        {
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

            bool isChildTile = bakedDepth >= 0 && data.id.depth > bakedDepth;
            bool doCull = config.cullBelowSea && !config.debugDisableUnderwaterCulling;
            bool removeTris = doCull && config.removeFullySubmergedTris;
            float seaR = config.baseRadius + config.seaLevel;
            float eps = config.seaClampEpsilon;
            var submergedFlags = removeTris ? new List<bool>(res * res) : null;

            bool isFirstSplitDepth = isChildTile && data.id.depth == bakedDepth + 1;
            int childLocalXMask = data.id.x & 1;
            int childLocalYMask = data.id.y & 1;

            for (int j = 0; j < res; j++)
            {
                for (int i = 0; i < res; i++)
                {
                    float u = (i * inv + data.id.x) / (1 << data.id.depth);
                    float v = (j * inv + data.id.y) / (1 << data.id.depth);
                    Vector3 dir = CubeSphere.FaceLocalToUnit(data.id.face, u * 2f - 1f, v * 2f - 1f);
                    _uvs.Add(new Vector2(u, v));

                    int ring = Mathf.Min(Mathf.Min(i, j), Mathf.Min(res - 1 - i, res - 1 - j));
                    bool treatAsParent = false;
                    if (isFirstSplitDepth && ring == 0)
                    {
                        if (!_edgePromotionRebuild)
                        {
                            bool onLeft = (i == 0) && childLocalXMask == 0;
                            bool onRight = (i == res - 1) && childLocalXMask == 1;
                            bool onBottom = (j == 0) && childLocalYMask == 0;
                            bool onTop = (j == res - 1) && childLocalYMask == 1;
                            treatAsParent = onLeft || onRight || onBottom || onTop;
                        }
                    }

                    float raw;
                    if (treatAsParent)
                        raw = SampleRawWithOctaveCap(dir, config.maxOctaveBake);
                    else if (isChildTile && config.maxOctaveSplit >= 0)
                        raw = SampleRawWithOctaveCap(dir, config.maxOctaveSplit);
                    else if (octaveWrapper != null && octaveWrapper.inner != null)
                        raw = octaveWrapper.inner.Sample(dir);
                    else
                        raw = heightProvider != null ? heightProvider.Sample(dir) : 0f;

                    raw *= config.heightScale;
                    if (raw < rawMin) rawMin = raw;
                    if (raw > rawMax) rawMax = raw;

                    float enhancement = (isChildTile && childHeightEnhancement > 1.01f) ? childHeightEnhancement : 1.0f;
                    float hSample = raw * enhancement;
                    float finalR = radius + hSample;

                    if (!treatAsParent && config.shorelineDetail && data.id.depth >= config.shorelineDetailMinDepth)
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

            if (_tris.Count >= 3)
            {
                Vector3 va = _verts[_tris[0]];
                Vector3 vb = _verts[_tris[1]];
                Vector3 vc = _verts[_tris[2]];
                Vector3 triN = Vector3.Cross(vb - va, vc - va);
                if (Vector3.Dot(triN, va) < 0f)
                {
                    for (int t = 0; t < _tris.Count; t += 3)
                    {
                        int tmp = _tris[t + 1];
                        _tris[t + 1] = _tris[t + 2];
                        _tris[t + 2] = tmp;
                    }
                }
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

        private float SampleRawWithOctaveCap(Vector3 dir, int maxOctave)
        {
            if (heightProvider is IOctaveSampler sampler && maxOctave >= 0)
                return sampler.SampleOctaveMasked(dir, maxOctave);
            return heightProvider != null ? heightProvider.Sample(dir) : 0f;
        }
    }
}
