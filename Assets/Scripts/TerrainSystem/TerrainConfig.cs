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
        [Tooltip("Resolution (vertices per edge) at LOD0 per cube face")] public int baseResolution = 16;
        [Tooltip("(Deprecated) Maximum LOD depth - static terrain build now")] public int maxLod = 0;
        [Tooltip("Height provider implementation (serialized polymorphically)")]
        [SerializeReference] public TerrainHeightProviderBase heightProvider; // polymorphic field
        [Tooltip("Global height scale multiplier")] public float heightScale = 2f;

        [Header("Oceans")] 
        [Tooltip("Generate a water sphere at the configured sea level")] public bool generateOcean = true;
        [Tooltip("Sea level in provider height units (0 = base radius). Positive raises sea surface, negative lowers it.")] public float seaLevel = 0f;
        [Tooltip("If true, TerrainRoot will overwrite the material _SeaLevel each rebuild. Default off so material value stays manual.")] public bool autoSyncSeaLevel = false;
        [Tooltip("Vertical band height above sea level to blend shallow water color onto terrain (visual shoreline). Set small (e.g. 1-3).")] public float shallowWaterBand = 2f;
        [Tooltip("Latitude segment count for ocean sphere (longitude uses 2x). Higher = smoother water.")] [Range(4,256)] public int oceanResolution = 64;
        [Tooltip("Material used for ocean sphere")] public Material oceanMaterial;

        [Header("Snow Caps")] 
        [Tooltip("Height offset (above sea) where snow blending begins")] public float snowStartOffset = 12f;
        [Tooltip("Height offset (above sea) where snow is fully white")] public float snowFullOffset = 18f;
        [Tooltip("Extra snow accumulation on flatter surfaces (0=none,1=strong)")] [Range(0,1)] public float snowSlopeBoost = 0.5f;
        [Tooltip("Snow color")] public Color snowColor = new Color(0.9f,0.9f,0.95f,1f);

    [Header("Underwater Culling")]
    [Tooltip("If enabled, any terrain below sea surface can be suppressed (clamped or removed) for performance/visual clarity.")]
    public bool cullBelowSea = false;
    [Tooltip("Remove triangles fully under the sea (creates coastline holes letting ocean show). If off, vertices are just clamped up.")]
    public bool removeFullySubmergedTris = true;
    [Tooltip("Small vertical offset added to clamped coastal vertices to avoid z-fighting with ocean surface.")]
    [Range(0f,0.1f)] public float seaClampEpsilon = 0.01f;

    [Header("Realistic Height Scaling")]
    [Tooltip("If enabled, raw height provider output is remapped so terrain heights are within a physical percentage of planet radius.")]
    public bool realisticHeights = true;
    [Tooltip("Maximum elevation as a fraction of planet radius (e.g. 0.02 = 2%).")]
    [Range(0.0001f, 0.1f)] public float maxElevationPercent = 0.02f;
    [Tooltip("Target modal (common) elevation as a fraction of planet radius (skews distribution toward this value).")]
    [Range(0.00005f, 0.05f)] public float modalElevationPercent = 0.005f;

    [Header("LOD Depths (Cube Face Quadtree)")]
    [Tooltip("Depth for Low baked level (0 = whole face).")] public int lowDepth = 0;
    [Tooltip("Depth for Medium baked level.")] public int mediumDepth = 2;
    [Tooltip("Depth for High baked level.")] public int highDepth = 4;
    [Tooltip("Depth for Ultra baked level (optional extra close-up detail). Set <= highDepth to disable.")] public int ultraDepth = 5;
    [Tooltip("Minimum quadtree depth considered for Extreme streaming (not baked fully).")] public int extremeMinDepth = 6;
    [Tooltip("Base screen-space error threshold for selecting tiles (future use)." )] public float baseScreenError = 5f;

    [Header("Per-Level Resolutions (Verts per Edge)")]
    [Tooltip("Resolution for Low depth tiles (verts per edge). If 0 uses baseResolution fallback.")] public int lowResolution = 0;
    [Tooltip("Resolution for Medium depth tiles (verts per edge). If 0 uses baseResolution.")] public int mediumResolution = 0;
    [Tooltip("Resolution for High depth tiles (verts per edge). If 0 uses baseResolution.")] public int highResolution = 0;
    [Tooltip("Resolution for Ultra depth tiles (verts per edge). If 0 uses baseResolution.")] public int ultraResolution = 0;

    [Header("Octave Masks (High-Frequency Isolation)")]
    [Tooltip("Max octave index included for Low depth tiles (-1 = all). 0=lowest freq only.")] public int lowMaxOctave = -1;
    [Tooltip("Max octave index included for Medium depth tiles (-1 = all)." )] public int mediumMaxOctave = -1;
    [Tooltip("Max octave index included for High depth tiles (-1 = all)." )] public int highMaxOctave = -1;
    [Tooltip("Max octave index included for Ultra depth tiles (-1 = all)." )] public int ultraMaxOctave = -1;

    [Header("Elevation Envelope & Peaks")]
    [Tooltip("Constrain most land below a spherical envelope slightly above sea; allow sparse peaks to exceed.")] public bool useElevationEnvelope = true;
    [Tooltip("Envelope scale relative to sea radius (1.01 = 1% above sea)."), Range(1.0f,1.1f)] public float envelopeScaleOverSea = 1.01f;
    [Tooltip("Fraction of envelope height typically used before strong compression (0.9 = top 10% reserved for rare peaks)."), Range(0.5f,0.99f)] public float typicalEnvelopeFill = 0.9f;
    [Tooltip("Probability that a vertex in the upper compressed band becomes a peak (sparse)."), Range(0f,0.05f)] public float peakProbability = 0.002f;
    [Tooltip("Additional peak height above envelope as fraction of baseRadius (e.g. 0.002 = 0.2%)."), Range(0f,0.02f)] public float peakExtraHeightPercent = 0.002f;
    [Tooltip("Compression exponent for scaling heights within the envelope tail (higher = flatter top before peaks)."), Range(1f,8f)] public float envelopeCompressionExponent = 4f;

        [Header("Distance Detail Boost")] 
        [Tooltip("Increase mesh face resolution when camera is within activate distance.")] public bool enableDistanceDetail = true;
        [Tooltip("Camera distance (world units) from planet center to switch to higher resolution.")] public float detailActivateDistance = 55f;
        [Tooltip("Distance to fall back to base resolution (should be > activate for hysteresis). ")] public float detailDeactivateDistance = 65f;
        [Tooltip("Multiplier applied to baseResolution when in detail mode.")] public int detailResolutionMultiplier = 2;
        [Tooltip("Minimum seconds between automatic rebuilds to avoid thrash.")] public float detailRebuildCooldown = 1.0f;

    [Header("LOD Cross-Fade")]
    [Tooltip("Smoothly cross-fade between LOD levels instead of popping.")] public bool enableCrossFade = true;
    [Tooltip("Duration of LOD cross-fade in seconds.")] [Range(0.05f,2f)] public float lodFadeDuration = 0.4f;

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

    [Header("Detail Scaling")] 
    [Tooltip("Scale refinement target depth by camera FOV (zoom). Narrow FOV increases perceived detail distance.")] public bool useFovScaling = true;
    [Tooltip("Reference FOV (degrees) considered 'neutral' for refinement scaling.")] public float referenceFov = 60f;
    }
}
