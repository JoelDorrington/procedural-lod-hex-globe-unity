using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using HexGlobeProject.TerrainSystem.Core;
using HexGlobeProject.TerrainSystem.Graphics;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.HexMap
{
    public static class PlanetRenderingUtils
    {
        // Toggle the procedural overlay property on materials assigned to a MeshRenderer.
        public static void SetOverlayOnMaterials(MeshRenderer meshRenderer, Transform planetTransform, float sphereRadius, bool enabled)
        {
            if (meshRenderer == null) return;

            // Gather overlay defaults from any TerrainRoot.config if available
            var terrainRoot = GameObject.FindFirstObjectByType<TerrainRoot>();
            TerrainConfig cfg = null;
            if (terrainRoot != null) cfg = terrainRoot.config;

            var mats = meshRenderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;

                var overlayShader = Shader.Find("HexGlobe/PlanetTerrain");
                if (overlayShader != null && (m.shader == null || m.shader.name != "HexGlobe/PlanetTerrain"))
                {
                    var newMat = new Material(overlayShader);
                    try { if (m.HasProperty("_Color") && newMat.HasProperty("_Color")) newMat.SetColor("_Color", m.GetColor("_Color")); } catch { }
                    mats[i] = newMat;
                    m = newMat;
                    changed = true;
                }

                if (cfg != null)
                {
                    TerrainShaderGlobals.Apply(cfg, m);
                }
                else
                {
                    if (m.HasProperty("_BaseRadius")) m.SetFloat("_BaseRadius", sphereRadius);
                }
                if (m.HasProperty("_OverlayEnabled")) m.SetFloat("_OverlayEnabled", enabled ? 1f : 0f);
            }

            if (changed)
            {
                meshRenderer.sharedMaterials = mats;
            }

            // Hide legacy Wireframe object if present
            GameObject wireframeObj = planetTransform.Find("Wireframe")?.gameObject;
            if (wireframeObj != null)
            {
                wireframeObj.SetActive(!enabled);
            }
        }

        // Build or update the dual wireframe GameObject under the provided parent transform.
        // This is a refactored copy of the original logic from Planet.BuildDualWireframe.
        public static void BuildDualWireframe(Transform parent, Mesh baseMesh, float baseRadius, Color wireframeColor,
            float wireOffsetFraction, bool projectEachSmoothingPass, Planet.DualSmoothingMode dualSmoothingMode,
            int dualSmoothingIterations, float smoothLambda, float smoothMu, float dualProjectionBlend)
        {
            if (parent == null || baseMesh == null) return;

            var tris = baseMesh.triangles;
            var verts = baseMesh.vertices;
            int faceCount = tris.Length / 3;

            var dualVertsRaw = new Vector3[faceCount];
            for (int f = 0; f < faceCount; f++)
            {
                int i0 = tris[3 * f + 0];
                int i1 = tris[3 * f + 1];
                int i2 = tris[3 * f + 2];
                Vector3 a = verts[i0];
                Vector3 b = verts[i1];
                Vector3 c = verts[i2];

                Vector3 p = (a + b + c) / 3f;
                dualVertsRaw[f] = p;
            }

            long EdgeKey(int a, int b)
            {
                int min = a < b ? a : b;
                int max = a ^ b ^ min;
                return ((long)min << 32) | (uint)max;
            }

            var edgeToFace = new Dictionary<long, int>(faceCount * 3);
            var lineIndices = new List<int>(faceCount * 3);
            var neighbors = new List<int>[faceCount];
            for (int i = 0; i < faceCount; i++) neighbors[i] = new List<int>(6);
            for (int f = 0; f < faceCount; f++)
            {
                int a = tris[3 * f + 0];
                int b = tris[3 * f + 1];
                int c = tris[3 * f + 2];

                void AddEdge(int u, int v, int face)
                {
                    long key = EdgeKey(u, v);
                    if (edgeToFace.TryGetValue(key, out int other))
                    {
                        lineIndices.Add(other);
                        lineIndices.Add(face);
                        if (neighbors[other].Count == 0 || neighbors[other][neighbors[other].Count - 1] != face) neighbors[other].Add(face);
                        if (neighbors[face].Count == 0 || neighbors[face][neighbors[face].Count - 1] != other) neighbors[face].Add(other);
                        edgeToFace.Remove(key);
                    }
                    else
                    {
                        edgeToFace[key] = face;
                    }
                }

                AddEdge(a, b, f);
                AddEdge(b, c, f);
                AddEdge(c, a, f);
            }

            GameObject wireframeObj = parent.Find("Wireframe")?.gameObject;
            if (wireframeObj == null)
            {
                wireframeObj = new GameObject("Wireframe");
                wireframeObj.transform.SetParent(parent, false);
                wireframeObj.AddComponent<MeshFilter>();
                var mr = wireframeObj.AddComponent<MeshRenderer>();
                var shader = Shader.Find("Unlit/Color");
                mr.sharedMaterial = new Material(shader) { color = wireframeColor };
            }
            else
            {
                var mr = wireframeObj.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null) mr.sharedMaterial.color = wireframeColor;
            }

            int terrainLayer = LayerMask.NameToLayer("TerrainTiles");
            if (terrainLayer >= 0)
            {
                wireframeObj.layer = terrainLayer;
                foreach (Transform t in wireframeObj.transform)
                {
                    t.gameObject.layer = terrainLayer;
                }
            }
            else
            {
                Debug.LogWarning("Layer 'TerrainTiles' not found. Wireframe GameObject will remain on the default layer.", parent.gameObject);
            }

            if (dualSmoothingIterations > 0 && dualSmoothingMode != Planet.DualSmoothingMode.None)
            {
                var temp = new Vector3[faceCount];

                System.Action<float> laplacianPass = (float weight) =>
                {
                    for (int f = 0; f < faceCount; f++)
                    {
                        var nbs = neighbors[f];
                        if (nbs.Count == 0) { temp[f] = dualVertsRaw[f]; continue; }
                        Vector3 acc = Vector3.zero;
                        for (int i = 0; i < nbs.Count; i++) acc += dualVertsRaw[nbs[i]];
                        Vector3 avg = acc / nbs.Count;
                        Vector3 delta = avg - dualVertsRaw[f];
                        if (dualSmoothingMode == Planet.DualSmoothingMode.LaplacianTangent || dualSmoothingMode == Planet.DualSmoothingMode.TaubinTangent)
                        {
                            Vector3 n = dualVertsRaw[f] != Vector3.zero ? dualVertsRaw[f].normalized : Vector3.up;
                            delta -= Vector3.Dot(delta, n) * n;
                        }
                        temp[f] = dualVertsRaw[f] + weight * delta;
                    }
                    var swap = dualVertsRaw; dualVertsRaw = temp; temp = swap;
                };

                System.Action reprojectToSphere = () =>
                {
                    if (!projectEachSmoothingPass) return;
                    for (int i = 0; i < faceCount; i++)
                    {
                        Vector3 v = dualVertsRaw[i];
                        if (v.sqrMagnitude > 1e-12f)
                            dualVertsRaw[i] = v.normalized * baseRadius;
                        else
                            dualVertsRaw[i] = Vector3.up * baseRadius;
                    }
                };

                for (int iter = 0; iter < dualSmoothingIterations; iter++)
                {
                    if (dualSmoothingMode == Planet.DualSmoothingMode.Laplacian || dualSmoothingMode == Planet.DualSmoothingMode.LaplacianTangent)
                    {
                        laplacianPass(smoothLambda);
                        reprojectToSphere();
                    }
                    else
                    {
                        laplacianPass(smoothLambda);
                        reprojectToSphere();
                        laplacianPass(smoothMu);
                        reprojectToSphere();
                    }
                }
            }

            Vector3[] dualVerts = new Vector3[faceCount];
            float drawOffset = baseRadius * wireOffsetFraction;
            for (int f = 0; f < faceCount; f++)
            {
                Vector3 raw = dualVertsRaw[f];
                if (dualProjectionBlend <= 0f)
                {
                    dualVerts[f] = raw + raw.normalized * drawOffset;
                }
                else
                {
                    Vector3 rawOffset = raw + raw.normalized * drawOffset;
                    Vector3 spherical = raw.normalized * (baseRadius + drawOffset);
                    dualVerts[f] = Vector3.Lerp(rawOffset, spherical, dualProjectionBlend);
                }
            }

            var wireMesh = new Mesh { name = "IcosphereDualWireframe" };
            if (dualVerts.Length > 65535)
                wireMesh.indexFormat = IndexFormat.UInt32;
            wireMesh.vertices = dualVerts;
            wireMesh.SetIndices(lineIndices, MeshTopology.Lines, 0, true);
            wireMesh.RecalculateBounds();

            wireframeObj.GetComponent<MeshFilter>().sharedMesh = wireMesh;
        }
    }
}
