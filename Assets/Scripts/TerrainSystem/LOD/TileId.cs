using System;
using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Identifies a quadtree tile on a cube-sphere face.
    /// face: 0..5, depth: 0=root, x,y in [0, 2^depth).
    /// </summary>
    [Serializable]
    public struct TileId : IEquatable<TileId>
    {
        public int face;
        public int depth;
        public int x;
        public int y;

        public TileId(int face, int depth, int x, int y)
        {
            this.face = face; this.depth = depth; this.x = x; this.y = y;
        }

        public override string ToString() => $"F{face} D{depth} ({x},{y})";
        public bool Equals(TileId other) => face==other.face && depth==other.depth && x==other.x && y==other.y;
        public override bool Equals(object obj) => obj is TileId o && Equals(o);
        public override int GetHashCode() => (face * 73856093) ^ (depth * 19349663) ^ (x * 83492791) ^ (y * 265443576);

        public TileId Parent() => depth==0 ? this : new TileId(face, depth-1, x>>1, y>>1);
        public TileId Child(int index)
        {
            // index: 0..3 (yx ordering). bit0 = x, bit1 = y
            int cx = x*2 + (index & 1);
            int cy = y*2 + ((index>>1)&1);
            return new TileId(face, depth+1, cx, cy);
        }
    }
}
