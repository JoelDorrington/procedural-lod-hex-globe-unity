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
            IcosphereMapping.WorldDirectionToTileFaceIndex(direction, out int face);
            IcosphereMapping.BarycentricFromWorldDirection(face, direction, out float u, out float v);

            int tilesPerEdge = 1 << depth;
            int x = Mathf.Clamp(Mathf.FloorToInt(u * tilesPerEdge), 0, tilesPerEdge - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(v * tilesPerEdge), 0, tilesPerEdge - 1);

            // Correct for triangle layout where u+v>1 mapping might fold
            if (!IcosphereMapping.IsValidTileIndex(x, y, depth))
            {
                // Mirror index across the face diagonal (canonical folding)
                x = Mathf.Clamp(tilesPerEdge - 1 - x, 0, tilesPerEdge - 1);
                y = Mathf.Clamp(tilesPerEdge - 1 - y, 0, tilesPerEdge - 1);
            }

            return new TileId(face, x, y, depth);
        }
    }
}
