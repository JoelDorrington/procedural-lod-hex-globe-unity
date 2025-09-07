using UnityEngine;

namespace HexGlobeProject.TerrainSystem
{
    /// <summary>
    /// Lightweight 3D Perlin-like noise provider for isotropic spherical sampling.
    /// This implementation uses a compact value-noise style gradient hash to produce
    /// reasonably fast 3D noise without external dependencies. It's suitable for tests
    /// and runtime sampling; parameters mirror SimplePerlinHeightProvider.
    /// </summary>
    [System.Serializable]
    public class Perlin3DHeightProvider : TerrainHeightProviderBase, IOctaveSampler
    {
        public float baseFrequency = 1f;
        public int octaves = 4;
        public float lacunarity = 2f;
        public float gain = 0.5f;
        public float amplitude = 1f;
        public int seed = 12345;

        // Cached permutation table for this instance (built lazily)
        private int[] _permCache = null;

        public override float Sample(in Vector3 unitDirection, int resolution)
        {
            Vector3 p = unitDirection.normalized * baseFrequency;
            float sum = 0f;
            float amp = 1f;
            float freq = 1f;

            // Small seeded offset to decorrelate different seeds
            Vector3 seedOffset = new Vector3(
                (seed * 0.0137f) % 10f,
                (seed * 0.0173f) % 10f,
                (seed * 0.0199f) % 10f);

            if (_permCache == null) _permCache = BuildPermutation(seed);
            for (int i = 0; i < octaves; i++)
            {
                Vector3 pp = p * freq + seedOffset * freq;
                // Average three axis-permuted samples to reduce axis bias
                float s1 = Perlin3D(pp, _permCache);
                float s2 = Perlin3D(new Vector3(pp.y, pp.z, pp.x), _permCache);
                float s3 = Perlin3D(new Vector3(pp.z, pp.x, pp.y), _permCache);
                sum += (s1 + s2 + s3) / 3f * amp;
                freq *= lacunarity;
                amp *= gain;
            }

            return Mathf.Clamp(sum * amplitude, -amplitude, amplitude);
        }

        public float SampleOctaveMasked(in Vector3 unitDirection, int maxInclusive)
        {
            Vector3 p = unitDirection.normalized * baseFrequency;
            float sum = 0f;
            float amp = 1f;
            float freq = 1f;

            Vector3 seedOffset = new Vector3(
                (seed * 0.0137f) % 10f,
                (seed * 0.0173f) % 10f,
                (seed * 0.0199f) % 10f);

            int limit = Mathf.Min(octaves - 1, Mathf.Max(-1, maxInclusive));
            if (limit < 0) limit = octaves - 1;

            if (_permCache == null) _permCache = BuildPermutation(seed);
            for (int i = 0; i <= limit; i++)
            {
                Vector3 pp = p * freq + seedOffset * freq;
                float s1 = Perlin3D(pp, _permCache);
                float s2 = Perlin3D(new Vector3(pp.y, pp.z, pp.x), _permCache);
                float s3 = Perlin3D(new Vector3(pp.z, pp.x, pp.y), _permCache);
                sum += (s1 + s2 + s3) / 3f * amp;
                freq *= lacunarity;
                amp *= gain;
            }

            return Mathf.Clamp(sum * amplitude, -amplitude, amplitude);
        }

        // Standard 3D Perlin implementation using permutation table and grad3.
        // We create a deterministic permutation table derived from the provider's seed.
        private float Perlin3D(Vector3 p, int[] permToUse=null)
        {
            // Permutation table (initialized per-instance for deterministic seed)
            var perm = permToUse ?? BuildPermutation(seed);

            float x = p.x;
            float y = p.y;
            float z = p.z;

            int X = Mathf.FloorToInt(x) & 255;
            int Y = Mathf.FloorToInt(y) & 255;
            int Z = Mathf.FloorToInt(z) & 255;

            x -= Mathf.Floor(x);
            y -= Mathf.Floor(y);
            z -= Mathf.Floor(z);

            float u = Fade(x);
            float v = Fade(y);
            float w = Fade(z);

            int A = perm[X] + Y;
            int AA = perm[A] + Z;
            int AB = perm[A + 1] + Z;
            int B = perm[X + 1] + Y;
            int BA = perm[B] + Z;
            int BB = perm[B + 1] + Z;

            float res = Lerp(w,
                        Lerp(v,
                            Lerp(u, Grad(perm[AA], x, y, z), Grad(perm[BA], x - 1, y, z)),
                            Lerp(u, Grad(perm[AB], x, y - 1, z), Grad(perm[BB], x - 1, y - 1, z))),
                        Lerp(v,
                            Lerp(u, Grad(perm[AA + 1], x, y, z - 1), Grad(perm[BA + 1], x - 1, y, z - 1)),
                            Lerp(u, Grad(perm[AB + 1], x, y - 1, z - 1), Grad(perm[BB + 1], x - 1, y - 1, z - 1))));

            return res;
        }

        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static float Lerp(float t, float a, float b)
        {
            return a + t * (b - a);
        }

        // Grad converts hash code to gradient and computes dot product with (x,y,z)
        private static float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        // Build a permutation table from a seed. Return array length 512 with duplicate.
        private static int[] BuildPermutation(int seed)
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
    }
}
