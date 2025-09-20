using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using HexGlobeProject.Graphics.DataStructures;
using HexGlobeProject.TerrainSystem.Core;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Graphics;

namespace HexGlobeProject.HexMap
{
    /// <summary>
    /// Planet is a container class that holds the CellGraph instance representing the grid of the globe,
    /// and manages rendering components like MeshFilter and MeshRenderer.
    /// This will be the game state controller, coordinating the UI with model changes.
    /// </summary>
    public class Planet : MonoBehaviour
    {
        // The graph representing the cells and their neighbor relationships
        private CellGraph cellGraph;

        // Rendering related components
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        private float sphereRadius = 30f; // Radius used to generate the procedural icosphere
        private Color sphereColor = new Color(0.2f, 0.4f, 1f);

        // Public settings for wireframe
        public Color wireframeColor = Color.black;
        public float lineThickness = 1f; // Note: line thickness might require a custom shader to be visible
        public enum DualSmoothingMode { None, Laplacian, LaplacianTangent, Taubin, TaubinTangent }
        [Tooltip("Smoothing method for dual vertices. Tangent variants preserve radius; Taubin reduces shrink.")]
        [HideInInspector] public DualSmoothingMode dualSmoothingMode = DualSmoothingMode.TaubinTangent;
        [HideInInspector] public int dualSmoothingIterations = 64;
        [HideInInspector] public float smoothLambda = 1f;
        [HideInInspector] public float smoothMu = 0f;
        [HideInInspector] public float dualProjectionBlend = 1f;
        [HideInInspector] public float wireOffsetFraction = 0.01f;
        [HideInInspector] public bool projectEachSmoothingPass = true;

        // Subdivision is now canonicalized to TerrainConfig.icosphereSubdivisions
        [Header("Scene helpers")]
        [SerializeField]
        [Tooltip("Hide the ocean renderer (keeps Ocean GameObject/transform). Mirrors TerrainRoot.hideOceanRenderer when a TerrainRoot exists in the scene.")]
        public bool hideOceanRenderer = false;

        // Initialization
        private void Awake()
        {
            ApplyHardcodedSettings();
            // Initialize the cell graph
            cellGraph = new CellGraph();
            // generation state
            isGenerated = false;
        }

        // Public flag to indicate when GeneratePlanet() has completed
        [HideInInspector]
        public bool isGenerated = false;

        /// <summary>
        /// Generates the planet by creating an icosphere and building its dual-wireframe overlay.
        /// </summary>
        public void GeneratePlanet()
        {
            ApplyHardcodedSettings();
            // Determine the sphere's radius directly from configuration (prefer TerrainRoot.config.baseRadius when available)
            float sphereR = sphereRadius;
            try
            {
                var terrainRoot = UnityEngine.Object.FindAnyObjectByType<TerrainRoot>();
                if (terrainRoot != null && terrainRoot.config != null && terrainRoot.config.baseRadius > 0f)
                {
                    // Prefer the tile-center radius used by the visibility system so the visual sphere aligns with spawned tiles.
                    // PlanetTileVisibilityManager uses a small curved multiplier (~1.01) when computing tile center world positions;
                    // match that here so the helper icosphere and the tile precomputed centers coincide visually.
                    sphereR = terrainRoot.config.baseRadius * 1.01f;
                }
            }
            catch { /* robust fallback: keep designer-set sphereRadius */ }

            // Ensure the GameObject has a MeshFilter
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            // Ensure the GameObject has a MeshRenderer
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            
            if (meshRenderer.sharedMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                meshRenderer.sharedMaterial = new Material(shader) { color = sphereColor };
            }

            // Determine subdivisions: prefer TerrainConfig.icosphereSubdivisions if available
            int extraSubs = 4; // fallback default when no TerrainConfig is present
            try
            {
                var terrainRoot = UnityEngine.Object.FindAnyObjectByType<TerrainRoot>();
                if (terrainRoot != null && terrainRoot.config != null)
                {
                    extraSubs = Mathf.Clamp(terrainRoot.config.icosphereSubdivisions, 0, 6);
                }
            }
            catch { }

            // Build solid icosphere using the provided generator (higher subdivisions -> smaller cells)
            Mesh sphereMesh = IcosphereGenerator.GenerateIcosphere(radius: sphereR, subdivisions: Mathf.Max(0, extraSubs));
            meshFilter.sharedMesh = sphereMesh;
            // Respect the hideOceanRenderer flag: keep the mesh assigned for transform/selection
            // but disable the MeshRenderer so the visual sphere is invisible when requested.
            if (meshRenderer != null)
            {
                meshRenderer.enabled = !hideOceanRenderer;
            }

            // Ensure the procedural shader overlay is enabled by default for playtests so the GPU overlay receives data.
            // Previously this was guarded by `enableWireframe`; force it on here so users see the overlay immediately.
            SetOverlayOnMaterials(true);
            // mark generation complete
            isGenerated = true;

        }

        // Toggle the procedural overlay property on materials assigned to this planet's MeshRenderer.
        // If a material doesn't support the property, it is left unchanged. Also hide any old Wireframe GameObject
        // so legacy geometry won't display while the overlay is active.
        private void SetOverlayOnMaterials(bool enabled)
        {
            if (meshRenderer == null) return;
            // Gather overlay defaults from any TerrainRoot.config if available
            var terrainRoot = FindAnyObjectByType<TerrainRoot>();
            TerrainConfig cfg = null;
            if (terrainRoot != null) cfg = terrainRoot.config;

            var mats = meshRenderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;

                // If the material doesn't use the overlay shader, replace it with one that does
                // This is an intentional, visible-playtest convenience: overlay will appear by default.
                var overlayShader = Shader.Find("HexGlobe/PlanetTerrain");
                if (overlayShader != null && (m.shader == null || m.shader.name != "HexGlobe/PlanetTerrain"))
                {
                    var newMat = new Material(overlayShader);
                    // copy a common color property if present
                    try { if (m.HasProperty("_Color") && newMat.HasProperty("_Color")) newMat.SetColor("_Color", m.GetColor("_Color")); } catch { }
                    mats[i] = newMat;
                    m = newMat;
                    changed = true;
                }

                // Ensure overlay parameters are applied using centralized logic (clamps ranges)
                if (cfg != null)
                {
                    TerrainShaderGlobals.Apply(cfg, m);
                }
                else
                {
                    // Fall back to local defaults where sensible
                    if (m.HasProperty("_BaseRadius")) m.SetFloat("_BaseRadius", sphereRadius);
                }
                if (m.HasProperty("_OverlayEnabled")) m.SetFloat("_OverlayEnabled", enabled ? 1f : 0f);
            }

            if (changed)
            {
                // If we replaced any materials, assign the modified array back to the renderer so the new materials are used.
                meshRenderer.sharedMaterials = mats;
            }

            // Hide legacy Wireframe object if present
            GameObject wireframeObj = transform.Find("Wireframe")?.gameObject;
            if (wireframeObj != null)
            {
                wireframeObj.SetActive(!enabled);
            }
        }

        private void ApplyHardcodedSettings()
        {
            dualSmoothingMode = DualSmoothingMode.TaubinTangent;
            dualSmoothingIterations = 64;
            smoothLambda = 1f;
            smoothMu = 0f;
            dualProjectionBlend = 1f;
            wireOffsetFraction = 0.01f;
            projectEachSmoothingPass = true;
        }

        private void BuildDualWireframe(Mesh baseMesh, float baseRadius)
        {
            var tris = baseMesh.triangles;
            var verts = baseMesh.vertices;
            int faceCount = tris.Length / 3;

            // Compute raw dual vertices (no normalization), then optionally project/blend to sphere for display
            var dualVertsRaw = new Vector3[faceCount];
            for (int f = 0; f < faceCount; f++)
            {
                int i0 = tris[3 * f + 0];
                int i1 = tris[3 * f + 1];
                int i2 = tris[3 * f + 2];
                Vector3 a = verts[i0];
                Vector3 b = verts[i1];
                Vector3 c = verts[i2];

                Vector3 p = (a + b + c) / 3f; // centroid-only for best regularity
                dualVertsRaw[f] = p;
            }

            // Map each undirected edge to the two faces that share it, then connect their centroids
            long EdgeKey(int a, int b)
            {
                int min = a < b ? a : b;
                int max = a ^ b ^ min; // faster: other
                return ((long)min << 32) | (uint)max;
            }

            var edgeToFace = new Dictionary<long, int>(faceCount * 3);
            var lineIndices = new List<int>(faceCount * 3); // ~3F indices (E*2, with E≈3F/2)
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
                        // Found the second face; connect centroids
                        lineIndices.Add(other);
                        lineIndices.Add(face);
                        // Build adjacency
                        if (neighbors[other].Count == 0 || neighbors[other][neighbors[other].Count - 1] != face) neighbors[other].Add(face);
                        if (neighbors[face].Count == 0 || neighbors[face][neighbors[face].Count - 1] != other) neighbors[face].Add(other);
                        // optional: remove to keep dict small
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

            // Create or update the wireframe GameObject
            GameObject wireframeObj = transform.Find("Wireframe")?.gameObject;
            if (wireframeObj == null)
            {
                wireframeObj = new GameObject("Wireframe");
                wireframeObj.transform.SetParent(this.transform, false);
                wireframeObj.AddComponent<MeshFilter>();
                var mr = wireframeObj.AddComponent<MeshRenderer>();
                var shader = Shader.Find("Unlit/Color");
                mr.sharedMaterial = new Material(shader) { color = wireframeColor };
            }
            else
            {
                // ensure material color matches current setting
                var mr = wireframeObj.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null) mr.sharedMaterial.color = wireframeColor;
            }

            // Ensure the wireframe GameObject is on the TerrainTiles layer so it is culled and rendered with tiles
            int terrainLayer = LayerMask.NameToLayer("TerrainTiles");
            if (terrainLayer >= 0)
            {
                // apply recursively in case children exist
                wireframeObj.layer = terrainLayer;
                foreach (Transform t in wireframeObj.transform)
                {
                    t.gameObject.layer = terrainLayer;
                }
            }
            else
            {
                Debug.LogWarning("Layer 'TerrainTiles' not found. Wireframe GameObject will remain on the default layer.", this);
            }

            // Optional smoothing on raw dual vertices
            if (dualSmoothingIterations > 0 && dualSmoothingMode != DualSmoothingMode.None)
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
                        if (dualSmoothingMode == DualSmoothingMode.LaplacianTangent || dualSmoothingMode == DualSmoothingMode.TaubinTangent)
                        {
                            // Project delta to tangent plane of the current position to limit radial shrink
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
                    if (dualSmoothingMode == DualSmoothingMode.Laplacian || dualSmoothingMode == DualSmoothingMode.LaplacianTangent)
                    {
                        laplacianPass(smoothLambda);
                        reprojectToSphere();
                    }
                    else // Taubin variants
                    {
                        laplacianPass(smoothLambda);
                        reprojectToSphere();
                        laplacianPass(smoothMu);
                        reprojectToSphere();
                    }
                }
            }

            // Apply projection blend and outward offset for display
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

            // Build wireframe mesh (straight segments). Simpler and less distortion while blending.
            var wireMesh = new Mesh { name = "IcosphereDualWireframe" };
            if (dualVerts.Length > 65535)
                wireMesh.indexFormat = IndexFormat.UInt32;
            wireMesh.vertices = dualVerts;
            wireMesh.SetIndices(lineIndices, MeshTopology.Lines, 0, true);

            wireMesh.RecalculateBounds();

            wireframeObj.GetComponent<MeshFilter>().sharedMesh = wireMesh;

            // Simple quality metric: edge length mean and coefficient of variation (std/mean)
            double sum = 0, sumSq = 0; int m = lineIndices.Count / 2;
            if (m > 0)
            {
                for (int i = 0; i < lineIndices.Count; i += 2)
                {
                    var d = (dualVerts[lineIndices[i]] - dualVerts[lineIndices[i + 1]]).magnitude;
                    sum += d; sumSq += d * d;
                }
                double mean = sum / m;
                double var = sumSq / m - mean * mean;
                double std = var > 0 ? (double)Mathf.Sqrt((float)var) : 0;
                double cv = mean > 1e-9 ? std / mean : 0;
            }

            // No solid mesh deformation
        }

        private void Start()
        {
            GeneratePlanet();
        }

    }
}
