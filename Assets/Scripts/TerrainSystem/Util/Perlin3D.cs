using UnityEngine;
using System;

namespace HexGlobeProject.TerrainSystem.Util
{
    /// <summary>
    /// Small, self-contained 3D Perlin helper.
    /// Provides a deterministic permutation builder and a Noise method that accepts
    /// an explicit permutation table so callers can maintain per-instance seeding.
    /// </summary>
    public static class Perlin3D
    {
        public static int[] BuildPermutation(int seed)
        {
            int[] p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;

            uint s = (uint)seed;
            for (int i = 255; i > 0; i--)
            {
                s = s * 1664525u + 1013904223u; // simple LCG
                int j = (int)(s % (uint)(i + 1));
                int tmp = p[i]; p[i] = p[j]; p[j] = tmp;
            }

            int[] perm = new int[512];
            for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
            return perm;
        }

        public static float Noise(Vector3 v, int[] perm)
        {
            if (perm == null)
            {
                Debug.LogWarning("Perlin3D.Noise called with null permutation; returning 0.");
                return 0f; // degrade gracefully when permutation missing
            }

            if (perm.Length == 0)
            {
                Debug.LogError("Perlin3D.Noise received empty permutation array; aborting noise sample and returning 0.");
                return 0f;
            }

            if (perm.Length < 512)
            {
                Debug.LogWarning($"Perlin3D.Noise received permutation of length {perm.Length}; expected >= 512. Falling back to safe indexing.");
            }

            // helper to safely index into perm (wrap into 0..perm.Length-1)
            System.Func<int,int> PermAt = (idx) =>
            {
                int m = perm.Length;
                int i = idx % m;
                if (i < 0) i += m;
                return perm[i];
            };

            float x = v.x;
            float y = v.y;
            float z = v.z;

            int X = Mathf.FloorToInt(x) & 255;
            int Y = Mathf.FloorToInt(y) & 255;
            int Z = Mathf.FloorToInt(z) & 255;

            x -= Mathf.Floor(x);
            y -= Mathf.Floor(y);
            z -= Mathf.Floor(z);

            float u = Fade(x);
            float v2 = Fade(y);
            float w = Fade(z);

            int A = PermAt(X) + Y;
            int AA = PermAt(A) + Z;
            int AB = PermAt(A + 1) + Z;
            int B = PermAt(X + 1) + Y;
            int BA = PermAt(B) + Z;
            int BB = PermAt(B + 1) + Z;

            float res = Lerp(
                Lerp(
                    Lerp(Grad(PermAt(AA), x, y, z), Grad(PermAt(BA), x - 1, y, z), u),
                    Lerp(Grad(PermAt(AB), x, y - 1, z), Grad(PermAt(BB), x - 1, y - 1, z), u), v2),
                Lerp(
                    Lerp(Grad(PermAt(AA + 1), x, y, z - 1), Grad(PermAt(BA + 1), x - 1, y, z - 1), u),
                    Lerp(Grad(PermAt(AB + 1), x, y - 1, z - 1), Grad(PermAt(BB + 1), x - 1, y - 1, z - 1), u), v2),
                w);

            return res;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Lerp(float a, float b, float t) => a + t * (b - a);

        private static float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15; // 16 gradients
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}
