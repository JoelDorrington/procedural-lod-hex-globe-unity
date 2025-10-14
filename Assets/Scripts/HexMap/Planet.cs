using UnityEngine;
using HexGlobeProject.Graphics.DataStructures;
using HexGlobeProject.TerrainSystem.Core;

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

            // mark generation complete
            isGenerated = true;

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

        private void Start()
        {
            GeneratePlanet();
        }

    }
}
