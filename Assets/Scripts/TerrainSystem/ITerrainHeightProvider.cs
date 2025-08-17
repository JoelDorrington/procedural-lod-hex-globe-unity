using UnityEngine;

namespace HexGlobeProject.TerrainSystem
{
    /// <summary>
    /// Pure interface returning a height (displacement) given a unit-length normal direction.
    /// Should be thread-safe and allocation-free.
    /// </summary>
    public interface ITerrainHeightProvider
    {
        float Sample(Vector3 unitDirection);
    }
}
