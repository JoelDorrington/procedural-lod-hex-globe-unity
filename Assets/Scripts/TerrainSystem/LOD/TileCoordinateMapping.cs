using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Shared coordinate mapping utilities to ensure consistency between raycast heuristic and mesh generation.
    /// This eliminates the coordinate system mismatch that was causing north pole tile issues at depth 1+.
    /// </summary>
    public static class TileCoordinateMapping
    {
        /// <summary>
        /// Convert a 3D world direction to cube face coordinates and tile indices.
        /// This is the authoritative method used by both raycast heuristic and mesh validation.
        /// </summary>
        /// <param name="worldDirection">Normalized direction vector from planet center</param>
        /// <param name="depth">Tile depth (0 = 1 tile per face, 1 = 2x2 tiles per face, etc.)</param>
        /// <param name="face">Output: cube face index (0-5)</param>
        /// <param name="tileX">Output: tile X index within face</param>
        /// <param name="tileY">Output: tile Y index within face</param>
        /// <param name="localFX">Output: local face X coordinate [-1, 1]</param>
        /// <param name="localFY">Output: local face Y coordinate [-1, 1]</param>
        public static void WorldDirectionToTileCoordinates(
            Vector3 worldDirection, 
            int depth,
            out int face, 
            out int tileX, 
            out int tileY,
            out float localFX,
            out float localFY)
        {
            Vector3 p = worldDirection.normalized;
            float absX = Mathf.Abs(p.x);
            float absY = Mathf.Abs(p.y);
            float absZ = Mathf.Abs(p.z);

            // Determine dominant axis and map to cube face
            if (absX >= absY && absX >= absZ)
            {
                if (p.x > 0f) { face = 0; localFX = -p.z / absX; localFY = -p.y / absX; }
                else { face = 1; localFX = p.z / absX; localFY = -p.y / absX; }
            }
            else if (absY >= absX && absY >= absZ)
            {
                if (p.y > 0f) { face = 2; localFX = p.x / absY; localFY = -p.z / absY; }
                else { face = 3; localFX = p.x / absY; localFY = -p.z / absY; }
            }
            else
            {
                if (p.z > 0f) { face = 4; localFX = p.x / absZ; localFY = -p.y / absZ; }
                else { face = 5; localFX = -p.x / absZ; localFY = -p.y / absZ; }
            }

            // Invert fy to align hemisphere mapping with camera (consistent with existing system)
            localFY = -localFY;

            // Convert face-local coordinates [-1,1] to tile indices
            int tilesPerEdge = 1 << depth;
            tileX = Mathf.Clamp(Mathf.FloorToInt((localFX + 1f) / 2f * tilesPerEdge), 0, tilesPerEdge - 1);
            tileY = Mathf.Clamp(Mathf.FloorToInt((localFY + 1f) / 2f * tilesPerEdge), 0, tilesPerEdge - 1);
        }
    }
}
