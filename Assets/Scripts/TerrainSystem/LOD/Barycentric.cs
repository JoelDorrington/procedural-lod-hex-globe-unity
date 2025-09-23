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
        public bool IsReflected { get; }

        public Barycentric(float u, float v)
        {
            // Be robust against tiny floating-point errors when u+v is extremely
            // close to 1.0. If W is only slightly negative due to rounding, treat
            // the point as lying on the edge (clamp/renormalize) instead of
            // reflecting it. Only perform a reflection when W is meaningfully
            // negative.
            // Increase epsilon to tolerate small rounding errors from lattice arithmetic
            // (TileVertexBarys computes weights that can slightly overshoot 1.0).
            const float kEpsilon = 1e-4f;
            float w = 1f - u - v;
            if (w < 0f)
            {
                if (w > -kEpsilon)
                {
                    // Clamp to the edge by renormalizing U,V so they sum to 1.
                    float s = u + v;
                    if (s > 0f)
                    {
                        U = u / s;
                        V = v / s;
                    }
                    else
                    {
                        U = u;
                        V = v;
                    }
                    IsReflected = false;
                }
                else
                {
                    // True reflection required
                    U = 1f - u;
                    V = 1f - v;
                    IsReflected = true;
                }
            }
            else
            {
                U = u;
                V = v;
                IsReflected = false;
            }
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

        /// <summary>
        /// Add two barycentric coordinates component-wise.
        /// </summary>
        public static Barycentric operator +(Barycentric a, Barycentric b) => new Barycentric(a.U + b.U, a.V + b.V);

        /// <summary>
        /// Subtract two barycentric coordinates component-wise.
        /// </summary>
        public static Barycentric operator -(Barycentric a, Barycentric b) => new Barycentric(a.U - b.U, a.V - b.V);

        /// <summary>
        /// Divide barycentric components by an integer divisor.
        /// Useful to convert a locally weighted barycentric into a global weighted barycentric
        /// when the weights are given as integer counts.
        /// </summary>
        public static Barycentric operator /(Barycentric a, int d)
        {
            if (d == 0) throw new DivideByZeroException("Cannot divide Barycentric by zero");
            return new Barycentric(a.U / d, a.V / d);
        }

        /// <summary>
        /// Divide barycentric components by a float divisor.
        /// </summary>
        public static Barycentric operator /(Barycentric a, float d)
        {
            if (Mathf.Approximately(d, 0f)) throw new DivideByZeroException("Cannot divide Barycentric by zero");
            return new Barycentric(a.U / d, a.V / d);
        }

        /// <summary>
        /// Multiply barycentric components by a float scalar.
        /// </summary>
        public static Barycentric operator *(Barycentric a, float s) => new Barycentric(a.U * s, a.V * s);
    }
}
