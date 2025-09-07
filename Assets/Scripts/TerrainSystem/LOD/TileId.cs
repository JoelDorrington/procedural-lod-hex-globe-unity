using System;
using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Identifies a hierarchical tile on a sphere surface.
    /// Originally cube-sphere (face: 0..5), now supports icosphere (face: 0..19).
    /// face: face index, depth: 0=root, x,y in [0, 2^depth) for valid tile region.
    /// </summary>
    [Serializable]
    public struct TileId : IEquatable<TileId>
    {
    public Vector3 faceNormal;
    public int depth;
    // Discrete identifiers when available (preferred for determinism)
    public int face;
    public int x;
    public int y;
    // Optional canonical registry index to avoid float equality issues
    public int canonicalIndex;

        public TileId(Vector3 faceNormal, int depth)
        {
            this.faceNormal = faceNormal; this.depth = depth; this.face = -1; this.x = -1; this.y = -1; this.canonicalIndex = -1;
        }

        /// <summary>
        /// Construct a TileId from discrete icosphere indices (face, x, y, depth).
        /// Stores discrete indices which are the canonical identifiers used for equality
        /// and hashing. The faceNormal is still computed and stored for texture/winding
        /// or geometric uses but is NOT used for equality or hashing.
        /// </summary>
        public TileId(int face, int x, int y, int depth)
        {
            // Use the canonical barycentric center computation
            IcosphereMapping.GetTileBarycentricCenter(x, y, depth, out float u, out float v);
            this.faceNormal = IcosphereMapping.BarycentricToWorldDirection(face, u, v).normalized;
            this.depth = depth;
            this.face = face;
            this.x = x;
            this.y = y;
            this.canonicalIndex = -1;
        }

        public override string ToString()
        {
            if (face >= 0) return $"F{face}({x},{y}) D{depth}";
            return $"F{faceNormal} D{depth}";
        }

        public bool Equals(TileId other)
        {
            // Prefer discrete indices (face,x,y) when both sides have them - deterministic and stable
            if (face >= 0 && other.face >= 0)
            {
                return face == other.face && x == other.x && y == other.y && depth == other.depth;
            }

            // If discrete indices are not available, fall back to canonicalIndex only when both sides provide it
            if (canonicalIndex >= 0 && other.canonicalIndex >= 0)
            {
                return canonicalIndex == other.canonicalIndex && depth == other.depth;
            }

            // No reliable discrete or canonical identifiers -> consider unequal
            return false;
        }

        public override bool Equals(object obj) => obj is TileId o && Equals(o);

        public override int GetHashCode()
        {
            // Prefer discrete indices for hashing when available
            if (face >= 0) return ((face * 73856093) ^ (x * 19349663) ^ (y * 83492791) ^ (depth * 15485863));
            if (canonicalIndex >= 0) return (canonicalIndex * 1000003) ^ depth;
            // No discrete identifiers: fall back to depth-based hash only (faceNormal intentionally excluded)
            return depth.GetHashCode();
        }
    }
}
