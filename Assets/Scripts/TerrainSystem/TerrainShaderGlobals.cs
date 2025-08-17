using UnityEngine;

namespace HexGlobeProject.TerrainSystem
{
    /// <summary>
    /// Centralized terrain shader global synchronization to avoid duplicated logic.
    /// </summary>
    public static class TerrainShaderGlobals
    {
        public static void Apply(TerrainConfig config, Material terrainMaterial)
        {
            if (config == null || terrainMaterial == null) return;
            float seaWorld = config.baseRadius + config.seaLevel;
            if (terrainMaterial.HasProperty("_SeaLevel")) terrainMaterial.SetFloat("_SeaLevel", seaWorld);
            if (terrainMaterial.HasProperty("_ShallowBand")) terrainMaterial.SetFloat("_ShallowBand", Mathf.Max(0.0001f, config.shallowWaterBand));
            if (terrainMaterial.HasProperty("_SnowStart"))
            {
                terrainMaterial.SetFloat("_SnowStart", seaWorld + config.snowStartOffset);
                terrainMaterial.SetFloat("_SnowFull", seaWorld + config.snowFullOffset);
                terrainMaterial.SetFloat("_SnowSlopeBoost", config.snowSlopeBoost);
                terrainMaterial.SetColor("_SnowColor", config.snowColor);
            }
        }
    }
}
