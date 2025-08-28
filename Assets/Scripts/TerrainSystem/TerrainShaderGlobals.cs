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
            // Shader auto-sync disabled. Material properties now controlled manually in inspector.
        }
    }
}
