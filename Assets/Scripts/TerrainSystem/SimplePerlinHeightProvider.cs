using UnityEngine;

namespace HexGlobeProject.TerrainSystem
{
    /// <summary>
    /// Basic layered Perlin noise height provider.
    /// </summary>
    [System.Serializable]
    public class SimplePerlinHeightProvider : TerrainHeightProviderBase, IOctaveSampler
    {
        public float baseFrequency = 1f;
        public int octaves = 4;
        public float lacunarity = 2f;
        public float gain = 0.5f;
        public float amplitude = 1f;
        public int seed = 12345;

        public override float Sample(in Vector3 unitDirection, int resolution)
        {
            // Use resolution to scale baseFrequency for more detail at higher resolutions
            float freqScale = Mathf.Max(1f, resolution / 16f);
            Vector3 p = unitDirection * baseFrequency * freqScale;
            float sum = 0f;
            float amp = 1f;
            float freq = 1f;
            Vector3 seedOffset = new Vector3(
                (seed * 0.0137f) % 10f,
                (seed * 0.0173f) % 10f,
                (seed * 0.0199f) % 10f);
            p += seedOffset;
            for (int i = 0; i < octaves; i++)
            {
                sum += (Mathf.PerlinNoise(p.x * freq, p.y * freq) * 2f - 1f) * amp;
                p = new Vector3(p.y, p.z, p.x);
                freq *= lacunarity;
                amp *= gain;
            }
            return sum * amplitude;
        }

        public float SampleOctaveMasked(in Vector3 unitDirection, int maxInclusive)
        {
            Vector3 p = unitDirection * baseFrequency;
            float sum = 0f;
            float amp = 1f;
            float freq = 1f;
            Vector3 seedOffset = new Vector3(
                (seed * 0.0137f) % 10f,
                (seed * 0.0173f) % 10f,
                (seed * 0.0199f) % 10f);
            p += seedOffset;
            int limit = Mathf.Min(octaves - 1, Mathf.Max(-1, maxInclusive));
            if (limit < 0) limit = octaves - 1; // treat -1 as full
            for (int i = 0; i <= limit; i++)
            {
                sum += (Mathf.PerlinNoise(p.x * freq, p.y * freq) * 2f - 1f) * amp;
                p = new Vector3(p.y, p.z, p.x);
                freq *= lacunarity;
                amp *= gain;
            }
            return sum * amplitude;
        }
    }
}
