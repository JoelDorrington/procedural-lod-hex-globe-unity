using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Icosahedral tile mapping system for uniform sphere tessellation.
    /// Replaces cube-sphere mapping to eliminate pole singularities and provide
    /// more regular tile distribution across the planet surface.
    /// </summary>
    public static class IcosphereMapping
    {
        // Golden ratio constant for icosahedron construction
        private const float PHI = 1.618033988749895f; // (1 + sqrt(5)) / 2
        
        // Standard icosahedron vertices (12 vertices)
        private static readonly Vector3[] IcosahedronVertices = 
        {
            new Vector3(-1,  PHI, 0).normalized,  // 0
            new Vector3( 1,  PHI, 0).normalized,  // 1  
            new Vector3(-1, -PHI, 0).normalized,  // 2
            new Vector3( 1, -PHI, 0).normalized,  // 3
            new Vector3(0, -1,  PHI).normalized,  // 4
            new Vector3(0,  1,  PHI).normalized,  // 5
            new Vector3(0, -1, -PHI).normalized,  // 6
            new Vector3(0,  1, -PHI).normalized,  // 7
            new Vector3( PHI, 0, -1).normalized,  // 8
            new Vector3( PHI, 0,  1).normalized,  // 9
            new Vector3(-PHI, 0, -1).normalized,  // 10
            new Vector3(-PHI, 0,  1).normalized   // 11
        };
        
        // 20 triangular faces of the icosahedron  
        private static readonly int[,] IcosahedronFaces = 
        {
            // 5 faces around point 0
            {0, 11, 5}, {0, 5, 1}, {0, 1, 7}, {0, 7, 10}, {0, 10, 11},
            // 5 adjacent faces
            {1, 5, 9}, {5, 11, 4}, {11, 10, 2}, {10, 7, 6}, {7, 1, 8},
            // 5 faces around point 3
            {3, 9, 4}, {3, 4, 2}, {3, 2, 6}, {3, 6, 8}, {3, 8, 9},
            // 5 adjacent faces  
            {4, 9, 5}, {2, 4, 11}, {6, 2, 10}, {8, 6, 7}, {9, 8, 1}
        };
        
        /// <summary>
        /// Convert a 3D world direction to icosahedral face and barycentric coordinates.
        /// </summary>
        /// <param name="worldDirection">Normalized direction vector from planet center</param>
        /// <param name="faceIndex">Output: icosphere face index</param>
        public static void WorldDirectionToTileFaceIndex(
            Vector3 worldDirection,
            out int faceIndex)
        {
            Vector3 p = worldDirection.normalized;
            
            // Find the closest icosahedral face by checking dot product with face normals
            float maxDot = float.MinValue;
            faceIndex = 0;
            
            for (int face = 0; face < 20; face++)
            {
                // Get the three vertices of this face
                Vector3 v0 = IcosahedronVertices[IcosahedronFaces[face, 0]];
                Vector3 v1 = IcosahedronVertices[IcosahedronFaces[face, 1]];
                Vector3 v2 = IcosahedronVertices[IcosahedronFaces[face, 2]];
                
                // Calculate face normal (centroid direction for regular icosahedron)
                Vector3 faceNormal = (v0 + v1 + v2).normalized;
                
                float dot = Vector3.Dot(p, faceNormal);
                if (dot > maxDot)
                {
                    maxDot = dot;
                    faceIndex = face;
                }
            }
        }

        
        /// <summary>
        /// Convert barycentric coordinates back to world direction.
        /// </summary>
        public static Vector3 BarycentricToWorldDirection(int faceIndex, float u, float v)
        {
            Vector3 v0 = IcosahedronVertices[IcosahedronFaces[faceIndex, 0]];
            Vector3 v1 = IcosahedronVertices[IcosahedronFaces[faceIndex, 1]];
            Vector3 v2 = IcosahedronVertices[IcosahedronFaces[faceIndex, 2]];
            
            // Third barycentric coordinate
            float w = 1f - u - v;
            
            // Interpolate position using barycentric coordinates
            Vector3 position = w * v0 + u * v1 + v * v2;
            
            return position.normalized;
        }
        
        /// <summary>
        /// Convert tile indices back to barycentric coordinates for mesh generation.
        /// </summary>
        /// <param name="faceNormal">Icosahedral face index</param>
        /// <param name="depth">Subdivision depth</param>
        /// <param name="vertexI">Vertex i-index within tile mesh</param>
        /// <param name="vertexJ">Vertex j-index within tile mesh</param>
        /// <param name="resolution">Mesh resolution</param>
        /// <param name="u">Output: barycentric u coordinate</param>
        /// <param name="v">Output: barycentric v coordinate</param>
        public static void TileVertexToBarycentricCoordinates(
            TileId tileId,
            int vertexI,
            int vertexJ,
            int resolution,
            out float u,
            out float v)
        {
            // Try to find a precomputed tile entry for this exact TileId (prefer discrete match)
            PlanetTileVisibilityManager.PrecomputedTileEntry entry;
            if (!PlanetTileVisibilityManager.GetPrecomputedIndex(tileId, out int _idx, out entry))
            {
                // Fallback to face-normal based lookup if discrete index not available
                if (!PlanetTileVisibilityManager.GetPrecomputedEntry(tileId, out entry))
                {
                    // No precomputed entry available - fall through to fallback mapping below
                    entry = default;
                }
            }
            else
            {
                int tilesPerEdge = entry.tilesPerEdge;

                // Compute global integer grid coordinates across the whole face so adjacent tiles align exactly.
                // This uses a (tilesPerEdge * (resolution-1)) grid per edge.
                int resMinusOne = Mathf.Max(1, resolution - 1);
                int globalPerEdge = tilesPerEdge * resMinusOne;

                int globalI = entry.x * resMinusOne + vertexI;
                int globalJ = entry.y * resMinusOne + vertexJ;

                u = globalI / (float)globalPerEdge;
                v = globalJ / (float)globalPerEdge;

                // Clamp to valid triangular region. When the point lies in the mirrored
                // half (u+v>=1) we must reflect the integer grid indices across the
                // diagonal so adjacent tiles compute identical integer coordinates.
                const float edgeEpsilon = 1e-6f;
                if (u + v >= 1f - edgeEpsilon)
                {
                    // Nudge slightly inward for exact boundary cases to avoid numerical flips
                    if (Mathf.Abs(u + v - 1f) < edgeEpsilon)
                    {
                        u -= edgeEpsilon * 0.5f;
                        v -= edgeEpsilon * 0.5f;
                    }
                    else
                    {
                        // Reflect integer coordinates across the diagonal so global grid
                        // alignment is preserved. Recompute u/v from the reflected integers.
                        globalI = globalPerEdge - globalI;
                        globalJ = globalPerEdge - globalJ;
                        u = globalI / (float)globalPerEdge;
                        v = globalJ / (float)globalPerEdge;
                    }
                }

                u = Mathf.Clamp01(u);
                v = Mathf.Clamp01(v);
                return;
            }

            // Fallback: approximate mapping by selecting face index from faceNormal and assuming origin at 0
            WorldDirectionToTileFaceIndex(tileId.faceNormal, out int faceIndex);
            int fallbackTilesPerEdge = 1 << tileId.depth;
            float localUF = vertexI / (float)(resolution - 1);
            float localVF = vertexJ / (float)(resolution - 1);
            u = localUF / fallbackTilesPerEdge;
            v = localVF / fallbackTilesPerEdge;
            if (u + v > 1f)
            {
                float excess = (u + v) - 1f;
                u -= excess * 0.5f;
                v -= excess * 0.5f;
            }
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);
        }
        
        /// <summary>
        /// Get the number of valid tiles for a given icosahedral face at a specific depth.
        /// <summary>
        /// Get the number of valid tiles for a given icosahedral face at a specific depth.
        /// Due to triangular geometry, only tiles whose barycentric centers fall within
        /// the triangle (u + v ≤ 1) are considered valid.
        /// </summary>
        public static int GetValidTileCountForDepth(int depth)
        {
            if (depth == 0) return 1; // One tile per face at depth 0
            
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
        /// For triangular faces, we exclude tiles where the barycentric center
        /// would fall outside the triangle (u + v > 1).
        /// </summary>
        public static bool IsValidTileIndex(int tileX, int tileY, int depth)
        {
            int tilesPerEdge = 1 << depth;
            if (tileX < 0 || tileY < 0 || tileX >= tilesPerEdge || tileY >= tilesPerEdge)
                return false;
                
            // For depth 0, there's only one tile per face
            if (depth == 0) return true;
            
            // Check if the barycentric center would be inside the triangle
            float u = (tileX + 0.5f) / tilesPerEdge;
            float v = (tileY + 0.5f) / tilesPerEdge;
            return (u + v) <= 1.0f;
        }

        /// <summary>
        /// Compute the canonical barycentric center for a tile at (x,y,depth).
        /// This is the single source of truth for tile center computation used by
        /// TileId, precomputation, mesh builder, and tests.
        /// </summary>
        public static void GetTileBarycentricCenter(int x, int y, int depth, out float u, out float v)
        {
            int tilesPerEdge = 1 << depth;
            if (tilesPerEdge == 1)
            {
                // For the whole face (depth 0) use the triangle centroid
                u = 1f / 3f;
                v = 1f / 3f;
            }
            else
            {
                u = (x + 0.5f) / tilesPerEdge;
                v = (y + 0.5f) / tilesPerEdge;
                // Nudge points that lie on or extremely close to the triangle edge
                // inward by a tiny epsilon so the face lookup is unambiguous.
                const float edgeNudge = 1e-4f;
                if (u + v >= 1f - 1e-6f)
                {
                    u -= edgeNudge * 0.5f;
                    v -= edgeNudge * 0.5f;
                }
            }
        }
    }
}
