using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Small math-only selector to map camera direction to a canonical TileId and k-ring neighbors.
    /// This is a deterministic, fast selector used to replace the old raycast heuristic.
    /// </summary>
    public static class MathVisibilitySelector
    {
        // Return the canonical tile (face,x,y) for a world direction at the given depth
        public static TileId TileFromDirection(Vector3 direction, int depth)
        {
            IcosphereMapping.WorldDirectionToTileIndex(depth, direction, out int face, out int x, out int y);
            return new TileId(face, x, y, depth);
        }
    }
}
