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

    [Header("Shoreline Detail")] 
    [Tooltip("Enable added high-frequency detail near coastlines at higher LODs.")] public bool shorelineDetail = true;
    [Tooltip("Minimum depth at which shoreline detail activates.")] public int shorelineDetailMinDepth = 4;
    [Tooltip("Vertical band (world units of height) around sea level where extra detail noise is applied.")] public float shorelineBand = 3f;
    [Tooltip("Maximum vertical displacement added by shoreline detail at the sea level (fades outward within band).")] public float shorelineDetailAmplitude = 0.6f;
    [Tooltip("Frequency multiplier for shoreline detail noise (higher = finer jaggedness).")] public float shorelineDetailFrequency = 6f;
    [Tooltip("Preserve original above/below sea classification to keep large-scale silhouette stable.")] public bool shorelinePreserveSign = true;
    // Removed seam fix fields (constrainChildEdgeHeights, childEdgeBlendRings, promoteEdgeAfterFade) to revert to original simpler configuration.

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
    }
}
