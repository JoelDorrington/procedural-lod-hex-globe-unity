using UnityEngine;

namespace HexGlobeProject.TerrainSystem
{
    /// <summary>
    /// Abstract base for all procedural height providers. Unity can serialize subclasses via SerializeReference.
    /// </summary>
    [System.Serializable]
    public abstract class TerrainHeightProviderBase
    {
    /// <param name="unitDirection">Normalized direction from planet center.</param>
    /// <param name="resolution">Tile resolution for noise detail.</param>
    public abstract float Sample(in Vector3 unitDirection, int resolution);
    }
}
