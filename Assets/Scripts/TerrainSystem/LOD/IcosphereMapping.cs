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
        public const float ONE_THIRD = 1f / 3f;
        // Golden ratio constant for icosahedron construction
        public const float PHI = 1.618033988749895f; // (1 + sqrt(5)) / 2

        // Standard icosahedron vertices (12 vertices)
        public static readonly Vector3[] IcosahedronVertices =
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
        public static readonly int[,] IcosahedronFaces =
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
        public static void WorldDirectionToFaceIndex(
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
                Vector3 centroidDir = BaryToWorldDirection(face, new(ONE_THIRD, ONE_THIRD));
                float dot = Vector3.Dot(p, centroidDir);
                if (dot > maxDot)
                {
                    maxDot = dot;
                    faceIndex = face;
                }
            }
        }

        /// <summary>
        /// Map a tile-local index array into the global barycentric coordinates on
        /// the icosphere face.
        /// </summary>
        /// <param name="tileId">Canonical tile identifier (face, x, y, depth).</param>
        /// <param name="localBary">Two-element array of tile-local subdivision indices (float values allowed).</param>
        /// <param name="res">Mesh resolution; used to derive subdivisions per tile edge (res - 1).</param>
        /// <returns>Global barycentric (u,v) coordinates across the icosphere face.</returns>
        public static Barycentric BaryLocalToGlobal(TileId tileId, Barycentric localBary, int res)
        {
            if (tileId.x < 0 || tileId.y < 0)
            {
                throw new Exception("BaryLocalToGlobal requires a TileId with positive integer face/x/y indices.");
            }
            var origin = TileIndexToBaryOrigin(tileId.depth, tileId.x, tileId.y);
            // Tile span (how much barycentric space this tile occupies across the face)
            float tileSpan = 1f / Mathf.Pow(2, tileId.depth);
            return origin + (localBary * tileSpan);
        }


        /// <summary>
        /// Convert Bary coordinates back to world direction.
        /// </summary>
        public static Vector3 BaryToWorldDirection(int face, Barycentric bary)
        {  // Depth is irrelevant. Converting straight from uv to world direction. no x, y subdivision indexing involved.
            Vector3 v0 = IcosahedronVertices[IcosahedronFaces[face, 0]];
            Vector3 v1 = IcosahedronVertices[IcosahedronFaces[face, 1]];
            Vector3 v2 = IcosahedronVertices[IcosahedronFaces[face, 2]];

            // Interpolate dir using Bary coordinates
            Vector3 dir = bary.W * v0 + bary.U * v1 + bary.V * v2;
            return dir.normalized;
        }

        public static Vector3[] GetCorners(TileId id, float planetRadius, Vector3 planetCenter = default)
        {
            var corners = new Vector3[3];

            Barycentric[] localIndices = new Barycentric[3] {
                new (0f, 0f),
                new (1f, 0f),
                new (0f, 1f)
            };

            for (int k = 0; k < 3; k++)
            {
                Barycentric global = BaryLocalToGlobal(id, localIndices[k], 1);
                Vector3 dir = BaryToWorldDirection(id.face, global).normalized;
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

            WorldDirectionToFaceIndex(worldDirection, out faceIndex);
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

            var bary = new Barycentric(u, v);
            if (depth < 0) depth = 0;
            int tilesPerEdge = 1 << depth;
            int maxTileIndex = tilesPerEdge - 1;

            x = Mathf.FloorToInt(Mathf.Lerp(0, maxTileIndex, bary.U * tilesPerEdge));
            y = Mathf.FloorToInt(Mathf.Lerp(0, maxTileIndex, bary.V * tilesPerEdge));
        }

        /// <summary>
        /// Enumerate all tile-local vertex barycentric coordinates for a given mesh resolution.
        /// These are integer lattice coordinates in the range [0,res-1] that must be   
        /// converted to normalized coordinates by the caller. 
        /// Callers must use BaryLocalToGlobal to convert to normalized coordinates.
        /// </summary>
        public static IEnumerable<Barycentric> TileVertexBarys(int res, int depth, int x, int y)
        {
            if (res <= 1)
            {
                // Degenerate resolution: single vertex at the triangle center index 0
                yield return new(0, 0);
                yield break;
            }
            var tilesPerEdge = 1 << depth;
            var tileWidthWeight = 1f / tilesPerEdge;
            var weight = tileWidthWeight / (res - 1);
            for (int j = 0; j < res; j++)
            {
                int maxI = res - 1 - j;
                for (int i = 0; i <= maxI; i++)
                {
                    float u = tileWidthWeight * x + (float)i * weight;
                    float v = tileWidthWeight * y + (float)j * weight;
                    // convert ints to barycentric
                    yield return new Barycentric(u, v);
                }
            }
            yield break;
        }

        /// <summary>
        /// Compute the canonical global bary zero point for a tile at (depth,x,y).
        /// This is the single source of truth for tile center computation used by
        /// TileId, precomputation, mesh builder, and tests.
        /// </summary>
        public static Barycentric TileIndexToBaryOrigin(int depth, int x, int y)
        {
            int tilesPerEdge = 1 << depth;
            float weight = 1f / tilesPerEdge; // no possibility of divide by zero
            float u = x * weight;
            float v = y * weight;
            // Reflect only when the sum strictly exceeds 1 so boundary centers are stable.
            return new Barycentric(u, v);
        }

        /// <summary>
        /// Compute the canonical global Bary center for a tile at (depth,x,y).
        /// This is the single source of truth for tile center computation used by
        /// TileId, precomputation, mesh builder, and tests.
        /// </summary>
        public static Barycentric TileIndexToBaryCenter(int depth, int x, int y)
        {
            int tilesPerEdge = 1 << depth;
            float fullTileWeight = 1f / tilesPerEdge;
            float tileCenterWeight = fullTileWeight / 3; // center is always at 1/3 of tile span
            float u = x * fullTileWeight + tileCenterWeight;
            float v = y * fullTileWeight + tileCenterWeight;
            // Reflect only when the sum strictly exceeds 1 so boundary centers are stable.
            return new Barycentric(u, v);
        }
    }
}
