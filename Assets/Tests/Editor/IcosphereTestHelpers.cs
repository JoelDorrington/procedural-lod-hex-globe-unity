using UnityEngine;

namespace HexGlobeProject.Tests.Editor
{
    public static class IcosphereTestHelpers
    {
        public static int GetValidTileCountForDepth(int depth)
        {
            if (depth == 0) return 1;
            int tilesPerEdge = 1 << depth;
            int validCount = 0;
            for (int x = 0; x < tilesPerEdge; x++)
            {
                for (int y = 0; y < tilesPerEdge; y++)
                {
                    if (IsValidTileIndex(x, y, depth))
                        validCount++;
                }
            }
            return validCount;
        }

        /// <summary>
        /// Validate that tile indices are within the canonical subdivision grid.
        /// For triangular faces, we exclude tiles where the Bary center
        /// would fall outside the triangle (u + v > 1).
        /// </summary>
        public static bool IsValidTileIndex(int tileX, int tileY, int depth)
        {
            int tilesPerEdge = 1 << depth;
            if (tileX < 0 || tileY < 0 || tileX >= tilesPerEdge || tileY >= tilesPerEdge)
                return false;
            // For depth 0, there's only one tile per face
            if (depth == 0) return true;

            // Check if the Bary center would be inside the triangle
            float u = (tileX + 0.5f) / tilesPerEdge;
            float v = (tileY + 0.5f) / tilesPerEdge;
            return (u + v) <= 1.0f;
        }
    }
}