using UnityEngine;
using System.Collections.Generic;

namespace HexGlobeProject.TerrainSystem
{
    /// <summary>
    /// Allows composing multiple height providers by summing their contributions.
    /// </summary>
    [System.Serializable]
    public class TerrainHeightStack : TerrainHeightProviderBase
    {
        [SerializeReference] public List<TerrainHeightProviderBase> layers = new();
        public override float Sample(in Vector3 unitDirection)
        {
            float h = 0f;
            for (int i = 0; i < layers.Count; i++)
            {
                h += layers[i].Sample(unitDirection);
            }
            return h;
        }
    }
}
