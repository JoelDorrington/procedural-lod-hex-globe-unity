using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;    

namespace HexGlobeProject.TerrainSystem.Graphics
{
    /// <summary>
    /// Centralized terrain shader global synchronization to avoid duplicated logic.
    /// </summary>
    public static class TerrainShaderGlobals
    {
        public static void Apply(TerrainConfig config, Material terrainMaterial)
        {
            if (config == null || terrainMaterial == null) return;
            // Apply overlay/debug mesh parameters
            if (terrainMaterial.HasProperty("_OverlayEnabled")) terrainMaterial.SetFloat("_OverlayEnabled", config.overlayEnabled ? 1f : 0f);
            if (terrainMaterial.HasProperty("_OverlayColor")) terrainMaterial.SetColor("_OverlayColor", config.overlayColor);
            if (terrainMaterial.HasProperty("_OverlayOpacity")) terrainMaterial.SetFloat("_OverlayOpacity", config.overlayOpacity);
        }

        // Apply a high-visibility debug overlay configuration to a material.
        // This forces values intended to make the overlay visible in the scene for debugging.
        public static void ApplyDebugHighVisibility(Material mat)
        {
            if (mat == null) return;
            if (mat.HasProperty("_OverlayEnabled")) mat.SetFloat("_OverlayEnabled", 1f);
            if (mat.HasProperty("_OverlayColor")) mat.SetColor("_OverlayColor", Color.cyan);
            if (mat.HasProperty("_OverlayOpacity")) mat.SetFloat("_OverlayOpacity", 1f);
        }
    }
}
