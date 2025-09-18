using System;
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

            // Choose the face whose canonical barycentric center direction
            // aligns best with the input direction. This uses the same
            // barycentric center computation used by the registry and the
            // TileId constructor so face selection is consistent.
            float maxDot = float.MinValue;
            faceIndex = 0;

            // Use the canonical depth-0 tile barycentric center so this routine
            // matches the registry and TileId constructor which both use
            // GetTileBarycentricCenter for the authoritative center point.
            GetTileBarycentricCenter(0, 0, 0, out float centerU, out float centerV);

            for (int face = 0; face < IcosahedronFaces.GetLength(0); face++)
            {
                Vector3 centroidDir = BarycentricToWorldDirection(face, centerU, centerV);
                float dot = Vector3.Dot(p, centroidDir);
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
        /// Compute barycentric coordinates (u,v) inside the triangle face for a given world direction.
        /// This inverts BarycentricToWorldDirection by projecting the direction into the triangle's
        /// coordinate frame and solving the barycentric linear system.
        /// </summary>
        public static void BarycentricFromWorldDirection(int faceIndex, Vector3 worldDirection, out float u, out float v)
        {
            Vector3 p = worldDirection.normalized;
            Vector3 v0 = IcosahedronVertices[IcosahedronFaces[faceIndex, 0]];
            Vector3 v1 = IcosahedronVertices[IcosahedronFaces[faceIndex, 1]];
            Vector3 v2 = IcosahedronVertices[IcosahedronFaces[faceIndex, 2]];

            // Solve for barycentric coordinates on the plane of the triangle using standard formula
            Vector3 v0v1 = v1 - v0;
            Vector3 v0v2 = v2 - v0;
            Vector3 v0p = p - v0;

            float d00 = Vector3.Dot(v0v1, v0v1);
            float d01 = Vector3.Dot(v0v1, v0v2);
            float d11 = Vector3.Dot(v0v2, v0v2);
            float d20 = Vector3.Dot(v0p, v0v1);
            float d21 = Vector3.Dot(v0p, v0v2);

            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < 1e-8f)
            {
                u = v = 0.3333f;
                return;
            }

            float a = (d11 * d20 - d01 * d21) / denom;
            float b = (d00 * d21 - d01 * d20) / denom;

            // a corresponds to weight for v1, b for v2 in our BarycentricToWorldDirection convention
            u = Mathf.Clamp01(a);
            v = Mathf.Clamp01(b);

            // If numerically u+v > 1, push slightly inward to avoid edge ambiguity
            if (u + v > 1f)
            {
                float scale = 1f / (u + v + 1e-6f);
                u *= scale;
                v *= scale;
            }
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
            // Use a unified canonical global-integer grid mapping so adjacent tiles
            // always compute identical barycentric coordinates for shared vertices.
            // This avoids asymmetry between precomputed registry lookups and the
            // fallback path which previously produced inconsistent results.

            // Do not construct a registry here; rely on the discrete TileId indices
            // supplied by callers. TileId should contain canonical (face,x,y) values
            // for deterministic mapping. If discrete indices are not present, the
            // caller is using a non-canonical TileId which this routine does not
            // support.
            if (tileId.face < 0 || tileId.x < 0 || tileId.y < 0)
            {
                throw new Exception("TileVertexToBarycentricCoordinates requires a TileId with discrete face/x/y indices.");
            }

            int tilesPerEdge = 1 << tileId.depth;

            int resMinusOne = Mathf.Max(1, resolution - 1);
            int globalPerEdge = tilesPerEdge * resMinusOne; // number of segments across the face

            // Prefer discrete tile indices from the TileId when available. This
            // avoids any floating-point lookup fragility and keeps the canonical
            // integer grid arithmetic purely discrete and deterministic.
            int tileX = tileId.x;
            int tileY = tileId.y;

            // Clamp vertex indices to valid range [0, resMinusOne]
            int localI = Mathf.Clamp(vertexI, 0, resMinusOne);
            int localJ = Mathf.Clamp(vertexJ, 0, resMinusOne);

            int globalI = tileX * resMinusOne + localI;
            int globalJ = tileY * resMinusOne + localJ;

            u = globalI / (float)globalPerEdge;
            v = globalJ / (float)globalPerEdge;

            // Reflect across diagonal for points that lie in the mirrored half so
            // that the integer grid remains consistent across the triangle seam.
            const float edgeEpsilon = 1e-6f;
            if (u + v >= 1f - edgeEpsilon)
            {
                if (Mathf.Abs(u + v - 1f) < edgeEpsilon)
                {
                    // tiny inward nudge to keep face selection stable
                    u -= edgeEpsilon * 0.5f;
                    v -= edgeEpsilon * 0.5f;
                }
                else
                {
                    globalI = globalPerEdge - globalI;
                    globalJ = globalPerEdge - globalJ;
                    u = globalI / (float)globalPerEdge;
                    v = globalJ / (float)globalPerEdge;
                }
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
            // Use the canonical triangular centroid offset (1/3) which lies
            // strictly inside the triangle. Using 0.5 placed the center on the
            // diagonal (u+v == 1) and produced ambiguous points shared by
            // multiple faces (causing face-index collisions). 1/3 is the
            // true barycentric centroid and provides a unique mapping.
            const float centerOffset = 1f / 3f;
            u = (x + centerOffset) / tilesPerEdge;
            v = (y + centerOffset) / tilesPerEdge;

            // If the barycentric center falls in the mirrored half (u+v > 1),
            // reflect into the canonical triangle so the center is consistent
            // with TileVertexToBarycentricCoordinates which mirrors grid vertices.
            const float edgeEpsilon = 1e-6f;
            if (u + v > 1f - edgeEpsilon)
            {
                u = 1f - u;
                v = 1f - v;
            }
        }
    }
}
