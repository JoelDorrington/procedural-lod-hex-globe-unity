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
            if (terrainMaterial == null) return;

            // helper to clamp colors to 0..1
            System.Func<Color, Color> clampColor = (c) => {
                c.r = Mathf.Clamp01(c.r);
                c.g = Mathf.Clamp01(c.g);
                c.b = Mathf.Clamp01(c.b);
                c.a = Mathf.Clamp01(c.a);
                return c;
            };
            if (config == null) return;
            if (terrainMaterial.HasProperty("_OverlayEnabled")) terrainMaterial.SetFloat("_OverlayEnabled", config.overlayEnabled ? 1f : 0f);
            if (terrainMaterial.HasProperty("_OverlayColor")) terrainMaterial.SetColor("_OverlayColor", clampColor(config.overlayColor));
            if (terrainMaterial.HasProperty("_OverlayOpacity")) terrainMaterial.SetFloat("_OverlayOpacity", Mathf.Clamp01(config.overlayOpacity));
            // Map simplified tiered colors
            if (terrainMaterial.HasProperty("_WaterColor")) terrainMaterial.SetColor("_WaterColor", clampColor(config.waterColor));
            if (terrainMaterial.HasProperty("_CoastColor")) terrainMaterial.SetColor("_CoastColor", clampColor(config.coastColor));
            if (terrainMaterial.HasProperty("_LowlandsColor")) terrainMaterial.SetColor("_LowlandsColor", clampColor(config.lowlandsColor));
            if (terrainMaterial.HasProperty("_HighlandsColor")) terrainMaterial.SetColor("_HighlandsColor", clampColor(config.highlandsColor));
            if (terrainMaterial.HasProperty("_MountainsColor")) terrainMaterial.SetColor("_MountainsColor", clampColor(config.mountainsColor));
            if (terrainMaterial.HasProperty("_SnowcapsColor")) terrainMaterial.SetColor("_SnowcapsColor", clampColor(config.snowcapsColor));

            // Map simplified tier thresholds (absolute heights above sea level)
            // Clamp to ensure monotonic increasing tiers and non-negative values.
            float cMax = Mathf.Max(0f, config.coastMax);
            float lMax = Mathf.Max(cMax, config.lowlandsMax);
            float hMax = Mathf.Max(lMax, config.highlandsMax);
            float mMax = Mathf.Max(hMax, config.mountainsMax);
            float sMax = Mathf.Max(mMax, config.snowcapsMax);
            if (terrainMaterial.HasProperty("_CoastMax")) terrainMaterial.SetFloat("_CoastMax", cMax);
            if (terrainMaterial.HasProperty("_LowlandsMax")) terrainMaterial.SetFloat("_LowlandsMax", lMax);
            if (terrainMaterial.HasProperty("_HighlandsMax")) terrainMaterial.SetFloat("_HighlandsMax", hMax);
            if (terrainMaterial.HasProperty("_MountainsMax")) terrainMaterial.SetFloat("_MountainsMax", mMax);
            if (terrainMaterial.HasProperty("_SnowcapsMax")) terrainMaterial.SetFloat("_SnowcapsMax", sMax);
            // Also set global shader variables so UI/global toggles and runtime code can rely on global state
            try
            {
                Shader.SetGlobalFloat("_OverlayEnabled", config.overlayEnabled ? 1f : 0f);
                Shader.SetGlobalColor("_WaterColor", clampColor(config.waterColor));
                Shader.SetGlobalColor("_CoastColor", clampColor(config.coastColor));
                Shader.SetGlobalColor("_LowlandsColor", clampColor(config.lowlandsColor));
                Shader.SetGlobalColor("_HighlandsColor", clampColor(config.highlandsColor));
                Shader.SetGlobalColor("_MountainsColor", clampColor(config.mountainsColor));
                Shader.SetGlobalColor("_SnowcapsColor", clampColor(config.snowcapsColor));

                Shader.SetGlobalFloat("_CoastMax", cMax);
                Shader.SetGlobalFloat("_LowlandsMax", lMax);
                Shader.SetGlobalFloat("_HighlandsMax", hMax);
                Shader.SetGlobalFloat("_MountainsMax", mMax);
                Shader.SetGlobalFloat("_SnowcapsMax", sMax);
            }
            catch { }
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
