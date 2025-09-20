using System;
using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{

    /// <summary>
    /// Immutable value type representing barycentric coordinates (u,v,w) on an
    /// icosphere face. w is implicitly computed as (1 - u - v).
    ///
    /// Use this ADT when passing barycentric coordinates through APIs to make
    /// intent explicit and to avoid confusion between tile-local indices and
    /// normalized bary fractions.
    /// </summary>
    public readonly struct Barycentric : IEquatable<Barycentric>
    {
        /// <summary>Normalized u coordinate in [0,1].</summary>
        public float U { get; }
        /// <summary>Normalized v coordinate in [0,1].</summary>
        public float V { get; }
        /// <summary>Implicit third coordinate w = 1 - u - v.</summary>
        public float W => 1f - U - V;

        public Barycentric(float u, float v)
        {
            U = u;
            V = v;
        }

        /// <summary>
        /// Construct from a tile-local subdivision index pair.
        /// </summary>
        /// <param name="tileId">Tile id containing depth/x/y.</param>
        /// <param name="localX">Tile-local subdivision offset along u axis.</param>
        /// <param name="localY">Tile-local subdivision offset along v axis.</param>
        /// <param name="res">Mesh resolution used to derive subdivisions per tile edge.</param>
        /// <returns>Normalized Barycentric coordinates across the face.</returns>
        public static Barycentric FromTileIndices(TileId tileId, float localX, float localY, int res)
        {
            int tilesPerFaceEdge = 1 << tileId.depth;
            int subdivisionsPerTileEdge = Math.Max(1, res - 1);
            float subdivisionsPerFaceEdge = tilesPerFaceEdge * subdivisionsPerTileEdge;

            float uGlobal = (tileId.x * subdivisionsPerTileEdge + localX) / subdivisionsPerFaceEdge;
            float vGlobal = (tileId.y * subdivisionsPerTileEdge + localY) / subdivisionsPerFaceEdge;

            if (uGlobal + vGlobal > 1f)
            {
                float oldU = uGlobal;
                float oldV = vGlobal;
                uGlobal = 1f - oldU;
                vGlobal = 1f - oldV;
            }
            return new Barycentric(uGlobal, vGlobal);
        }

        /// <summary>
        /// Construct from an array-based tile-local index input (two-element float[]).
        /// </summary>
        public static Barycentric FromTileIndexArray(TileId tileId, float[] localIndices, int res)
        {
            if (localIndices == null || localIndices.Length < 2)
                throw new ArgumentException("localIndices must be a two-element array", nameof(localIndices));
            return FromTileIndices(tileId, localIndices[0], localIndices[1], res);
        }

        public bool Equals(Barycentric other) => Mathf.Approximately(U, other.U) && Mathf.Approximately(V, other.V);

        public override bool Equals(object obj) => obj is Barycentric other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(U, V);

        public override string ToString() => $"Bary(U={U:0.######}, V={V:0.######}, W={W:0.######})";

        /// <summary>
        /// Convert this barycentric to a <see cref="UnityEngine.Vector2"/> with (U,V).
        /// </summary>
        public Vector2 ToVector2() => new Vector2(U, V);

        /// <summary>
        /// Implicit conversion to UnityEngine.Vector2 so callers can use Barycentric
        /// where Vector2 is expected. This is a convenience for incremental refactors.
        /// </summary>
        public static implicit operator Vector2(Barycentric b) => b.ToVector2();
    }
}
