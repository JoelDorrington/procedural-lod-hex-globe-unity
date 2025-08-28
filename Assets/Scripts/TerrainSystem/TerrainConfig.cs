using UnityEngine;

namespace HexGlobeProject.TerrainSystem
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

    [Header("Underwater Culling")]
    [Tooltip("If enabled, any terrain below sea surface can be suppressed (clamped or removed) for performance/visual clarity.")]
    public bool cullBelowSea = false;
    [Tooltip("Remove triangles fully under the sea (creates coastline holes letting ocean show). If off, vertices are just clamped up.")]
    public bool removeFullySubmergedTris = true;
    [Tooltip("Small vertical offset added to clamped coastal vertices to avoid z-fighting with ocean surface.")]
    [Range(0f,0.1f)] public float seaClampEpsilon = 0.01f;

    // Realistic height scaling removed: raw heightProvider output now used directly (scaled only by heightScale / debugElevationMultiplier).

    [Header("LOD (Simplified)")]
    [Tooltip("Depth baked at startup (0 = one tile per face). Higher = more base tiles.")] public int bakeDepth = 2;
    [Tooltip("Optional deeper target depth for proximity split. Set <= bakeDepth to disable splitting.")] public int splitTargetDepth = 4;
    [Tooltip("Max octave index for baked tiles (-1 = all). Lower limits distant high-frequency noise.")] public int maxOctaveBake = -1;
    [Tooltip("Max octave index for split child tiles (-1 = all). Usually higher or -1 for full detail.")] public int maxOctaveSplit = -1;

    // Elevation envelope & peaks system removed; raw heights now unbounded except by seaLevel and any external shader logic.

    [Header("Debug / Tuning")]
    [Tooltip("Multiplies final sampled elevation (after remap if any) to exaggerate relief for tuning (1 = off)." )]
    public float debugElevationMultiplier = 1f;
    [Tooltip("If true, ignore underwater culling flags so you can verify full elevation distribution.")]
    public bool debugDisableUnderwaterCulling = false;

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
    }
}
