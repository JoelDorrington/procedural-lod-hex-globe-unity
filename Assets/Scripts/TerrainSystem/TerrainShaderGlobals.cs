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
            if (terrainMaterial.HasProperty("_LineThickness"))
            {
                float thick = Mathf.Clamp(config.overlayLineThickness, 0.0005f, 0.5f);
                terrainMaterial.SetFloat("_LineThickness", thick);
            }
            if (terrainMaterial.HasProperty("_BaseRadius")) terrainMaterial.SetFloat("_BaseRadius", config.baseRadius);
            if (terrainMaterial.HasProperty("_EdgeExtrusion"))
            {
                // Prevent excessively large extrusions that would mask the overlay entirely
                float extr = Mathf.Clamp(config.overlayEdgeExtrusion, 0f, 5f);
                terrainMaterial.SetFloat("_EdgeExtrusion", extr);
            }
        }

        // Apply a high-visibility debug overlay configuration to a material.
        // This forces values intended to make the overlay visible in the scene for debugging.
        public static void ApplyDebugHighVisibility(Material mat)
        {
            if (mat == null) return;
            if (mat.HasProperty("_OverlayEnabled")) mat.SetFloat("_OverlayEnabled", 1f);
            if (mat.HasProperty("_OverlayColor")) mat.SetColor("_OverlayColor", Color.cyan);
            if (mat.HasProperty("_OverlayOpacity")) mat.SetFloat("_OverlayOpacity", 1f);
            if (mat.HasProperty("_CellSize")) mat.SetFloat("_CellSize", 0.5f);
            if (mat.HasProperty("_LineThickness")) mat.SetFloat("_LineThickness", 0.01f);
            if (mat.HasProperty("_BaseRadius")) mat.SetFloat("_BaseRadius", 0.5f);
            if (mat.HasProperty("_EdgeExtrusion")) mat.SetFloat("_EdgeExtrusion", 0f);
        }

        // Log overlay-related properties of a material to the Unity console for inspection.
        public static void LogMaterialOverlayProperties(Material mat)
        {
            if (mat == null) return;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"Material: {mat.name}");
            void AddProp(string name)
            {
                if (!mat.HasProperty(name)) return;
                var prop = mat.GetFloat(name);
                sb.AppendLine($"  {name} = {prop}");
            }
            AddProp("_OverlayEnabled");
            if (mat.HasProperty("_OverlayColor")) sb.AppendLine($"  _OverlayColor = {mat.GetColor("_OverlayColor")}");
            AddProp("_OverlayOpacity");
            AddProp("_CellSize");
            AddProp("_LineThickness");
            AddProp("_BaseRadius");
            AddProp("_EdgeExtrusion");

            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}
