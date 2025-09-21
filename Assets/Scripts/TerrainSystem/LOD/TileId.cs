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
        public int depth;
        public int face;
        public int x;
        public int y;
        public Vector3 faceNormal;

        public TileId(Vector3 faceNormal, int depth)
        {
            this.faceNormal = faceNormal;
            this.depth = depth;
            IcosphereMapping.WorldDirectionToTileIndex(depth, faceNormal, out this.face, out this.x, out this.y);
        }

        /// <summary>
        /// Construct a TileId from discrete icosphere indices (face, x, y, depth).
        /// Stores discrete indices which are the canonical identifiers used for equality
        /// and hashing. The faceNormal is still computed and stored for texture/winding
        /// or geometric uses but is NOT used for equality or hashing.
        /// </summary>
        public TileId(int face, int x, int y, int depth)
        {
            this.depth = depth;
            this.face = face;
            this.x = x;
            this.y = y;
            faceNormal = IcosphereMapping.BaryToWorldDirection(face, IcosphereMapping.GetTileBaryCenter(depth, x, y)).normalized;
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

            // No reliable discrete or canonical identifiers -> consider unequal
            return false;
        }

        public override bool Equals(object obj) => obj is TileId o && Equals(o);

        public override int GetHashCode()
        { // all indices may be zero or negative, so offset by +1 to avoid zero multipliers
           return ((face+1) * 73856093) ^ ((x+1) * 19349663) ^ ((y+1) * 83492791) ^ ((depth+1) * 15485863);
        }
    }
}
