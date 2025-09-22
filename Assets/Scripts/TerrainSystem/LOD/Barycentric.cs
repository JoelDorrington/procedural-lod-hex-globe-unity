using System;
using System.Net.WebSockets;
using System.Runtime.InteropServices.WindowsRuntime;
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
        public float W => 1f - (U + V);

        public Barycentric(float u, float v)
        {
            U = u;
            V = v;
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
        /// Note: this does NOT renormalize or clamp the result. The caller
        /// is responsible for ensuring the result is a valid barycentric
        /// coordinate (for example by calling <see cref="Normalized"/>).
        /// </summary>
        public static Barycentric operator +(Barycentric a, Barycentric b) => new Barycentric(a.U + b.U, a.V + b.V);

        /// <summary>
        /// Subtract two barycentric coordinates component-wise.
        /// </summary>
        public static Barycentric operator -(Barycentric a, Barycentric b) => new Barycentric(a.U - b.U, a.V - b.V);

        /// <summary>
        /// Return a normalized barycentric where U+V+W == 1. If the input already
        /// defines a valid bary, this returns the same values. If the sum is zero,
        /// returns the zero barycentric (0,0).
        /// </summary>
        public Barycentric Normalized()
        {
            float sum = U + V + W; // which equals 1f, but we compute safely for arbitrary sums
            if (Mathf.Approximately(sum, 0f)) return new Barycentric(0f, 0f);
            return new Barycentric(U / sum, V / sum);
        }

        /// <summary>
        /// Returns the barycentric reflected across the triangle diagonal, i.e.
        /// (U,V) -> (1 - V, 1 - U). This is the canonical mirror used by the
        /// triangular lattice folding logic. Use this instead of inlining the
        /// expression at call sites so behavior remains centralized.
        /// </summary>
        public Barycentric Reflected() {
            // Proper reflection across the triangle diagonal: (U,V) -> (1 - V, 1 - U)
            return new Barycentric(1f - V, 1f - U);
        }

        /// <summary>
        /// Returns true when the barycentric lies in the mirrored half of the
        /// reference triangle (i.e., when U + V > 1).
        /// </summary>
        public bool IsMirrored() => (U + V) > 1f;

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
