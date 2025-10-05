using UnityEngine;

namespace HexGlobeProject.TerrainSystem.Graphics
{
    /// <summary>
    /// ScriptableObject that contains tunable shader parameters for the planet terrain shader.
    /// These values are clamped by TerrainShaderGlobals before being sent to the GPU to avoid
    /// accidental breakage from extreme values.
    /// </summary>
    [CreateAssetMenu(fileName = "TerrainShaderConfig", menuName = "HexGlobe/Terrain/ShaderConfig")]
    public class ShaderConfig : ScriptableObject
    {
        [Header("Overlay")]
        public bool overlayEnabled = false;
        public Color overlayColor = Color.black;
        [Range(0f, 1f)] public float overlayOpacity = 0.9f;
        [Range(0f, 1f)] public float overlayEdgeThreshold = 0.15f;
        [Min(0.0001f)] public float overlayAAScale = 100f;
        public bool useDualOverlay = true;
        public Cubemap dualOverlayCube = null;

        [Header("Planet / Glow")]
        [Min(0.0001f)] public float planetRadius = 30f;
        [Min(0f)] public float overlayGlowSpread = 0.5f;
        public Color overlayGlowColor = Color.cyan;
        [Min(0f)] public float overlayGlowIntensity = 1f;
        [Range(0f, 16f)] public float overlayGlowLOD = 2f;
    }
}
