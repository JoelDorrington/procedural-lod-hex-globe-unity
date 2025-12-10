using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// ScriptableObject storing configuration for procedural terrain generation.
    /// </summary>
    [CreateAssetMenu(menuName = "HexGlobe/Terrain Config", fileName = "TerrainConfig")]
    public class TerrainConfig : ScriptableObject
    {
    [Tooltip("Base planetary radius")] public float baseRadius = 30f;
    [Tooltip("Base face resolution (vertices per edge) used for depth=0; deeper depths derive automatically.")] public int baseResolution = 16;
    [Tooltip("Height provider implementation (serialized polymorphically)")]
    [SerializeReference] public TerrainHeightProviderBase heightProvider; // polymorphic field
    [Tooltip("Global height scale multiplier")] public float heightScale = 2f;

    // Ocean & snow options removed (managed manually or by shader parameters directly).
    [Tooltip("Sea level in provider height units (0 = base radius). Positive raises sea surface, negative lowers it.")] public float seaLevel = 0f; // kept minimal for terrain remap logic
    [Tooltip("Vertical band height above sea for shallow coloration (still used by generation if needed). ")] public float shallowWaterBand = 2f;

    // Underwater culling removed: coastal clamping and triangle removal are deprecated and handled externally if needed.

    // Realistic height scaling removed: raw heightProvider output now used directly (scaled only by heightScale / debugElevationMultiplier).

    // LOD (simplified) removed - controlled elsewhere. Previously: bakeDepth, splitTargetDepth, maxOctaveBake, maxOctaveSplit

    // Elevation envelope & peaks system removed; raw heights now unbounded except by seaLevel and any external shader logic.

    [Header("Debug / Tuning")]
    [Tooltip("Multiplies final sampled elevation (after remap if any) to exaggerate relief for tuning (1 = off)." )]
    public float debugElevationMultiplier = 1f;

    [Tooltip("Icosphere subdivisions for helper/test sphere and dual-mesh generation. Higher value -> smaller, more numerous cells (affects cell count and game model size).")]
    [Range(0, 6)]
    public int icosphereSubdivisions = 4;


    [Tooltip("Recalculate mesh normals from geometry (shows slopes). If off, uses radial normals (fast but hides relief when viewed head-on)." )]
    public bool recalcNormals = true;

    // Removed shoreline detail and seam fix fields to keep configuration minimal and aligned with current runtime usage.

    [Header("Overlay / Debug Mesh")]
    [Tooltip("Enable the procedural dual-mesh overlay (shader-driven).")]
    public bool overlayEnabled = false;
    [Tooltip("Color of the overlay lines")]
    public Color overlayColor = Color.black;
    [Tooltip("Overlay line opacity")]
    [Range(0f,1f)] public float overlayOpacity = 0.9f;
    [Tooltip("Line thickness in lattice units (0..1 approx)")]
    public float overlayLineThickness = 0.05f;
    [Tooltip("Radial extrusion height applied to edges when masking overlay")]
    public float overlayEdgeExtrusion = 0.5f;
    [Header("Shader: Tiered Colors Above Sea Level")]
    [Tooltip("Deep water color (h <= 0)")]
    public Color waterColor = new Color(0.10f, 0.20f, 0.60f, 1f);
    [Space(6)]
    [Tooltip("Coastline max height above sea level (integer stored as float)")]
    public float coastMax = 0.1f;
    [Tooltip("Lowlands max height above sea level (integer stored as float)")]
    public float lowlandsMax = 0.3f;
    [Tooltip("Highlands max height above sea level (integer stored as float)")]
    public float highlandsMax = 0.5f;
    [Tooltip("Mountains max height above sea level (integer stored as float)")]
    public float mountainsMax = 0.8f;
    [Tooltip("Snowcaps max height above sea level (integer stored as float)")]
    public float snowcapsMax = 0.99f;
    [Space(6)]
    [Tooltip("Coastline color")] public Color coastColor = new Color(1.0f, 1.0f, 0.0f, 1f); // yellow
    [Tooltip("Lowlands color")] public Color lowlandsColor = new Color(0.75f, 0.95f, 0.55f, 1f); // light green
    [Tooltip("Highlands color")] public Color highlandsColor = new Color(0.55f, 0.70f, 0.50f, 1f); // greyish green
    [Tooltip("Mountains color")] public Color mountainsColor = new Color(0.30f, 0.30f, 0.30f, 1f); // dark grey
    [Tooltip("Snowcaps color")] public Color snowcapsColor = new Color(1.0f, 1.0f, 1.0f, 1f); // white
    }
}
