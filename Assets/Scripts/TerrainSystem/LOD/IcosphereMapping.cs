using System;
using System.Collections.Generic;
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
        private const float ONE_THIRD = 1f / 3f;
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
        /// Convert a 3D world direction to icosahedral face and Bary coordinates.
        /// </summary>
        /// <param name="worldDirection">Normalized direction vector from planet center</param>
        /// <param name="faceIndex">Output: icosphere face index</param>
        public static void WorldDirectionToTileFaceIndex(
            Vector3 p,
            out int faceIndex)
        {

            // Choose the face whose canonical Bary center direction
            // aligns best with the input direction. This uses the same
            // Bary center computation used by the registry and the
            // TileId constructor so face selection is consistent.
            float maxDot = float.MinValue;
            faceIndex = 0;

            for (int face = 0; face < 20; face++)
            {
                // Use barycentric center (u=v=1/3) for canonical face direction
                Vector3 centroidDir = BaryToWorldDirection(face, ONE_THIRD, ONE_THIRD);
                float dot = Vector3.Dot(p, centroidDir);
                if (dot > maxDot)
                {
                    maxDot = dot;
                    faceIndex = face;
                }
            }
        }

        public static Vector2 BaryLocalToGlobal(TileId tileId, float[] localBary, int res)
        {
            if (tileId.face < 0 || tileId.x < 0 || tileId.y < 0)
            {
                throw new Exception("BaryLocalToGlobal requires a TileId with positive integer face/x/y indices.");
            }
            int tilesPerFaceEdge = 1 << tileId.depth;
            // The mesh lattice uses (res - 1) segments per tile edge (TileVertexBarys uses weight = 1/(res-1)).
            // To produce a consistent global lattice we must use the same segment count here.
            int subdivisionsPerTileEdge = Math.Max(1, res - 1);
            float subdivisionsPerFaceEdge = tilesPerFaceEdge * subdivisionsPerTileEdge;
            // tile-local subdivision index -> global bary across the face
            // globalIndex = tileId.x * subdivisionsPerTileEdge + localBary.x
            float uGlobal = (tileId.x * subdivisionsPerTileEdge + localBary[0]) / subdivisionsPerFaceEdge;
            float vGlobal = (tileId.y * subdivisionsPerTileEdge + localBary[1]) / subdivisionsPerFaceEdge;
            // Reflect strictly when the sum exceeds 1.0f. Do not reflect at exactly 1.0f
            // so boundary lattice coordinates remain stable and deterministic.
            if (uGlobal + vGlobal > 1f)
            {
                uGlobal = 1f - vGlobal;
                vGlobal = 1f - uGlobal;
            }
            return new Vector2(uGlobal, vGlobal);
        }

        // Non-allocating overload to avoid per-vertex small array allocations.
        public static Vector2 BaryLocalToGlobal(TileId tileId, float localX, float localY, int res)
        {
            if (tileId.x < 0 || tileId.y < 0)
            {
                throw new Exception("BaryLocalToGlobal requires a TileId with positive integer face/x/y indices.");
            }
            int tilesPerFaceEdge = 1 << tileId.depth;
            int subdivisionsPerTileEdge = Math.Max(1, res - 1);
            float subdivisionsPerFaceEdge = tilesPerFaceEdge * subdivisionsPerTileEdge;
            float uGlobal = (tileId.x * subdivisionsPerTileEdge + localX) / subdivisionsPerFaceEdge;
            float vGlobal = (tileId.y * subdivisionsPerTileEdge + localY) / subdivisionsPerFaceEdge;
            // Reflect strictly when the sum exceeds 1.0f. This matches the test
            // expectation that sums equal to 1 are not reflected.
            if (uGlobal + vGlobal > 1f)
            {
                uGlobal = 1f - vGlobal;
                vGlobal = 1f - uGlobal;
            }
            return new Vector2(uGlobal, vGlobal);
        }


        /// <summary>
        /// Convert Bary coordinates back to world direction.
        /// </summary>
        public static Vector3 BaryToWorldDirection(int faceIndex, float u, float v)
        {  // Depth is irrelevant. Converting straight from uv to world direction. no x, y subdivision indexing involved.
            Vector3 v0 = IcosahedronVertices[IcosahedronFaces[faceIndex, 0]];
            Vector3 v1 = IcosahedronVertices[IcosahedronFaces[faceIndex, 1]];
            Vector3 v2 = IcosahedronVertices[IcosahedronFaces[faceIndex, 2]];

            // Third Bary coordinate
            float w = 1f - u - v;

            // Interpolate position using Bary coordinates
            Vector3 position = w * v0 + u * v1 + v * v2;

            return position.normalized;
        }

        public static Vector3[] GetCorners(TileId id, float planetRadius, Vector3 planetCenter)
        {
            var corners = new Vector3[3];

            float[][] localIndices = new float[3][] {
                new float[2]{0f, 0f},
                new float[2]{1f, 0f},
                new float[2]{0f, 1f}
            };

            for (int k = 0; k < 3; k++)
            {
                Vector2 globalUV = BaryLocalToGlobal(id, localIndices[k], 1);
                Vector3 dir = BaryToWorldDirection(id.face, globalUV.x, globalUV.y).normalized;
                corners[k] = dir * planetRadius + planetCenter;
            }

            return corners;
        }

        /// <summary>
        /// Compute Bary coordinates (u,v) inside the triangle face for a given world direction.
        /// This inverts BaryToWorldDirection by projecting the direction into the triangle's
        /// coordinate frame and solving the Bary linear system.
        /// </summary>
        public static void WorldDirectionToTileIndex(int depth, Vector3 worldDirection, out int faceIndex, out int x, out int y)
        {

            WorldDirectionToTileFaceIndex(worldDirection, out faceIndex);
            Vector3 p = worldDirection.normalized;
            Vector3 v0 = IcosahedronVertices[IcosahedronFaces[faceIndex, 0]];
            Vector3 v1 = IcosahedronVertices[IcosahedronFaces[faceIndex, 1]];
            Vector3 v2 = IcosahedronVertices[IcosahedronFaces[faceIndex, 2]];

            // Solve for Bary coordinates on the plane of the triangle using standard formula
            Vector3 v0v1 = v1 - v0;
            Vector3 v0v2 = v2 - v0;
            Vector3 v0p = p - v0;

            float d00 = Vector3.Dot(v0v1, v0v1);
            float d01 = Vector3.Dot(v0v1, v0v2);
            float d11 = Vector3.Dot(v0v2, v0v2);
            float d20 = Vector3.Dot(v0p, v0v1);
            float d21 = Vector3.Dot(v0p, v0v2);

            float denom = d00 * d11 - d01 * d01;
            float u, v;
            if (Mathf.Abs(denom) < 1e-8f)
            { // center
                u = v = 0.3333f;
            }
            else
            {
                u = (d11 * d20 - d01 * d21) / denom;
                v = (d00 * d21 - d01 * d20) / denom;
            }

            if (u + v >= 1f)
            {
                u = 1f - v;
                v = 1f - u;
            }

            if (depth < 0) depth = 0;
            int tilesPerEdge = 1 << depth;
            int maxSubdivisionIndex = tilesPerEdge - 1;

            x = Mathf.FloorToInt(Mathf.Lerp(0, maxSubdivisionIndex, u * tilesPerEdge));
            y = Mathf.FloorToInt(Mathf.Lerp(0, maxSubdivisionIndex, v * tilesPerEdge));
        }

        /// <summary>
        /// Convert tile indices back to Bary coordinates for mesh generation.
        /// </summary>
        /// <param name="tileId">Canonical face id object</param>
        /// <param name="res">Mesh resolution</param>
        public static IEnumerable<float[]> TileVertexBarys(int res)
        {
            if (res <= 1)
            {
                // Degenerate resolution: single vertex at the triangle center index 0
                yield return new float[2] { 0f, 0f };
                yield break;
            }
            // Use (res - 1) segments per tile edge. The triangular lattice contains
            // exactly res*(res+1)/2 vertices when iterating rows with lengths
            // res, res-1, ..., 1 (j = 0..res-1).
            float weight = 1f / (res - 1);
            for (int j = 0; j < res; j++)
            {
                // maxI such that i + j <= res - 1
                int maxI = res - 1 - j;
                for (int i = 0; i <= maxI; i++)
                {
                    float u = weight * i;
                    float v = weight * j;

                    float w = 1f - u - v;
                    if (w < 0f)
                    {
                        v = 1f - u;
                        u = 1f - v;
                    }
                    yield return new float[2] { u, v };
                }
            }
            yield break;
        }

        /// <summary>
        /// Compute the canonical Bary center for a tile at (depth,x,y).
        /// This is the single source of truth for tile center computation used by
        /// TileId, precomputation, mesh builder, and tests.
        /// </summary>
        public static void GetTileBaryCenter(int depth, int x, int y, out float u, out float v)
        {
            /*
                depth = 0: center weights at thirds
                depth = 1: center weights at 9ths
                depth = 2: center weights at 18ths
                SOLUTION: weight = 1 / 3^(depth+1)
            */
            float weight = 1f / Mathf.Pow(3, depth + 1); // no possibility of divide by zero
            u = (x + 1) * weight;
            v = (y + 1) * weight;
            // Reflect only when the sum strictly exceeds 1 so boundary centers are stable.
            if (u + v > 1f)
            {
                u = 1f - v;
                v = 1f - u;
            }
        }
    }
}
