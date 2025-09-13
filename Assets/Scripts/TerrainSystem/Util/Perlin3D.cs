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
            if (perm == null) throw new ArgumentNullException(nameof(perm));

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

            int A = perm[X] + Y;
            int AA = perm[A] + Z;
            int AB = perm[A + 1] + Z;
            int B = perm[X + 1] + Y;
            int BA = perm[B] + Z;
            int BB = perm[B + 1] + Z;

            float res = Lerp(
                Lerp(
                    Lerp(Grad(perm[AA], x, y, z), Grad(perm[BA], x - 1, y, z), u),
                    Lerp(Grad(perm[AB], x, y - 1, z), Grad(perm[BB], x - 1, y - 1, z), u), v2),
                Lerp(
                    Lerp(Grad(perm[AA + 1], x, y, z - 1), Grad(perm[BA + 1], x - 1, y, z - 1), u),
                    Lerp(Grad(perm[AB + 1], x, y - 1, z - 1), Grad(perm[BB + 1], x - 1, y - 1, z - 1), u), v2),
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
