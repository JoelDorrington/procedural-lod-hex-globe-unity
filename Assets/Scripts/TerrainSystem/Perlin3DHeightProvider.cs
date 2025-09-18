using UnityEngine;
using HexGlobeProject.TerrainSystem.Util;
using HexGlobeProject.TerrainSystem.Core;

namespace HexGlobeProject.TerrainSystem
{
    [System.Serializable]
    public class Perlin3DHeightProvider : TerrainHeightProviderBase, IOctaveSampler
    {
        public float baseFrequency = 1f;
        public int octaves = 4;
        public float lacunarity = 2f;
        public float gain = 0.5f;
        public float amplitude = 1f;
        public int seed = 12345;

        private int[] _permCache = null;
        public override float Sample(in Vector3 unitDirection, int resolution)
        {
            try
            {
                return SampleInternal(unitDirection, resolution);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Perlin3DHeightProvider.Sample exception: {ex}");
                return 0f;
            }
        }

        private float SampleInternal(in Vector3 unitDirection, int resolution)
        {
            Vector3 p = unitDirection.normalized * baseFrequency;
            float sum = 0f;
            float amp = 1f;
            float freq = 1f;

            Vector3 seedOffset = new Vector3(
                (seed * 0.0137f) % 10f,
                (seed * 0.0173f) % 10f,
                (seed * 0.0199f) % 10f);

            if (_permCache == null)
            {
                _permCache = Perlin3D.BuildPermutation(seed);
                Debug.Log($"Perlin3DHeightProvider: built permutation for seed {seed}, length {_permCache?.Length}");
            }

            if (_permCache != null && _permCache.Length == 0)
            {
                Debug.LogWarning($"Perlin3DHeightProvider: permutation for seed {seed} is empty; rebuilding.");
                _permCache = Perlin3D.BuildPermutation(seed);
                Debug.Log($"Perlin3DHeightProvider: rebuilt permutation for seed {seed}, length {_permCache?.Length}");
            }

            for (int i = 0; i < octaves; i++)
            {
                Vector3 pp = p * freq + seedOffset * freq;
                // Axis permutations help remove any residual directional bias
                float s1 = Perlin3D.Noise(pp, _permCache);
                float s2 = Perlin3D.Noise(new Vector3(pp.y, pp.z, pp.x), _permCache);
                float s3 = Perlin3D.Noise(new Vector3(pp.z, pp.x, pp.y), _permCache);
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

            if (_permCache == null) _permCache = Perlin3D.BuildPermutation(seed);

            for (int i = 0; i <= limit; i++)
            {
                Vector3 pp = p * freq + seedOffset * freq;
                float s1 = Perlin3D.Noise(pp, _permCache);
                float s2 = Perlin3D.Noise(new Vector3(pp.y, pp.z, pp.x), _permCache);
                float s3 = Perlin3D.Noise(new Vector3(pp.z, pp.x, pp.y), _permCache);
                sum += (s1 + s2 + s3) / 3f * amp;
                freq *= lacunarity;
                amp *= gain;
            }

            return Mathf.Clamp(sum * amplitude, -amplitude, amplitude);
        }
    }
}
