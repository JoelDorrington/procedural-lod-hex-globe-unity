using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.TerrainSystem.Core
{
    /// <summary>
    /// Base container for terrain config.
    /// </summary>
    [ExecuteAlways]
    public class TerrainRoot : MonoBehaviour
    {
        public TerrainConfig config;
        public Material terrainMaterial;
        [SerializeField]
        [Tooltip("Hide the ocean's MeshRenderer in the scene but keep the Ocean GameObject and its transform present for positioning.")]
        public bool hideOceanRenderer = false;

    }
}
