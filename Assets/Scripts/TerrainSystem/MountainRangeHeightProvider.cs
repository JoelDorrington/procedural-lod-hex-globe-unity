using UnityEngine;
using System;

namespace HexGlobeProject.TerrainSystem
{
    /// <summary>
    /// Procedural mountain range generator combining low-frequency continental noise with procedural great-circle ridges.
    /// Avoids custom list editing by generating N range bands from a seed.
    /// </summary>
    [System.Serializable]
    public class MountainRangeHeightProvider : TerrainHeightProviderBase
    {
        [Header("Continents")] public float continentFrequency = 0.25f; // very low frequency
        public int continentOctaves = 3;
        public float continentLacunarity = 2f;
        public float continentGain = 0.45f;
        public float continentAmplitude = 1.0f;

        [Header("Ridges (Great-Circle Based)")] public int numRanges = 8;
        [Range(0.5f, 20f)] public float rangeWidthDegrees = 6f; // angular half-width controlling falloff
        public float rangeAmplitude = 2.5f;
        [Range(0.1f, 10f)] public float rangeSharpness = 3.0f; // higher -> narrower Gaussian
        public int rangeSeed = 12345;

        [Header("Ridged Fractal Details")] public float ridgeBaseFrequency = 1.2f;
        public int ridgeOctaves = 5;
        public float ridgeLacunarity = 1.9f;
        public float ridgeGain = 0.55f;
        [Range(0.5f, 8f)] public float ridgeSharpness = 2.5f; // exponent on ridged value
        public float ridgeAmplitude = 1.0f;

        [Header("Global")] public float finalAmplitude = 1.0f;

        // Cached generated range normals & per-range amplitude jitter
        private Vector3[] _rangeNormals;
        private float[] _rangeAmps;
        private int _cachedSeed;
        private int _cachedCount;
        private float _cachedWidthDeg;

    public override float Sample(in Vector3 unitDirection, int resolution)
        {
            EnsureRanges();
            
            // IMPORTANT: Resolution should NOT affect the height values themselves!
            // Higher resolution tiles must match the topology of lower resolution tiles.
            // Resolution is only used for mesh density, not terrain generation.
            
            // Base continents (soft) - always at consistent frequency
            float continents = FractalPerlin(unitDirection, continentFrequency, continentOctaves, continentLacunarity, continentGain);
            // Shift to -1..1 then scale
            continents = continents * 2f - 1f;
            float continentHeight = continents * continentAmplitude;

            // Range mask sum of Gaussians around great circles
            float widthRad = Mathf.Deg2Rad * Mathf.Max(0.001f, rangeWidthDegrees);
            float invWidth2 = 1.0f / (widthRad * widthRad);
            float mask = 0f;
            for (int i = 0; i < _rangeNormals.Length; i++)
            {
                float d = Mathf.Abs(Vector3.Dot(unitDirection, _rangeNormals[i])); // sin(angle) for great circle normal
                // Angular distance from great circle is asin(d). Approx small-angle: asin(d) ~ d for small d
                float ang = Mathf.Asin(Mathf.Clamp01(d));
                float w = Mathf.Exp(-Mathf.Pow(ang, 2f) * invWidth2 * rangeSharpness);
                mask += w * _rangeAmps[i];
            }
            mask = Mathf.Clamp01(mask); // accumulate then clamp

            // Ridged fractal for mountains - consistent parameters regardless of resolution
            float ridged = RidgedFractal(unitDirection, ridgeBaseFrequency, ridgeOctaves, ridgeLacunarity, ridgeGain, ridgeSharpness);
            float mountain = ridged * ridgeAmplitude * rangeAmplitude * mask;

            return (continentHeight + mountain) * finalAmplitude;
        }

        private void EnsureRanges()
        {
            if (_rangeNormals != null && _cachedSeed == rangeSeed && _cachedCount == numRanges && Mathf.Approximately(_cachedWidthDeg, rangeWidthDegrees)) return;
            _cachedSeed = rangeSeed;
            _cachedCount = numRanges;
            _cachedWidthDeg = rangeWidthDegrees;
            numRanges = Mathf.Max(1, numRanges);
            var rand = new System.Random(rangeSeed);
            _rangeNormals = new Vector3[numRanges];
            _rangeAmps = new float[numRanges];
            for (int i = 0; i < numRanges; i++)
            {
                // Pick two random points on sphere to define a great circle normal n = a x b
                Vector3 a = RandomOnSphere(rand);
                Vector3 b = RandomOnSphere(rand);
                Vector3 n = Vector3.Cross(a, b);
                if (n.sqrMagnitude < 1e-6f)
                {
                    n = Vector3.Cross(a, Vector3.up);
                    if (n.sqrMagnitude < 1e-6f) n = Vector3.Cross(a, Vector3.right);
                }
                n.Normalize();
                _rangeNormals[i] = n;
                _rangeAmps[i] = 0.6f + (float)rand.NextDouble() * 0.8f; // jitter 0.6..1.4
            }
        }

        private static Vector3 RandomOnSphere(System.Random r)
        {
            // Marsaglia method
            while (true)
            {
                double x1 = r.NextDouble() * 2.0 - 1.0;
                double x2 = r.NextDouble() * 2.0 - 1.0;
                double s = x1 * x1 + x2 * x2;
                if (s >= 1.0) continue;
                double z = 1 - 2 * s;
                double t = 2 * Math.Sqrt(1 - s);
                double x = x1 * t;
                double y = x2 * t;
                return new Vector3((float)x, (float)y, (float)z).normalized;
            }
        }

        private float FractalPerlin(Vector3 dir, float baseFreq, int octaves, float lac, float gain)
        {
            float sum = 0f;
            float amp = 1f;
            float freq = baseFreq;
            for (int i = 0; i < octaves; i++)
            {
                // project direction onto a rotated 2D slice each octave for variety
                Vector3 p = dir * freq + new Vector3(i * 13.37f, i * 7.11f, i * 3.17f);
                float n = Mathf.PerlinNoise(p.x + 100f, p.y + 200f);
                sum += n * amp;
                amp *= gain;
                freq *= lac;
            }
            return sum;
        }

        private float RidgedFractal(Vector3 dir, float baseFreq, int octaves, float lac, float gain, float sharpness)
        {
            float sum = 0f;
            float amp = 0.5f;
            float freq = baseFreq;
            for (int i = 0; i < octaves; i++)
            {
                Vector3 p = dir * freq + new Vector3(i * 31.1f, i * 17.7f, i * 9.3f);
                float n = Mathf.PerlinNoise(p.x + 10f, p.y + 40f) * 2f - 1f; // -1..1
                float r = 1f - Mathf.Abs(n);        // ridged shape
                r = Mathf.Pow(Mathf.Max(0f, r), sharpness); // sharpen
                sum += r * amp;
                freq *= lac;
                amp *= gain;
            }
            return sum;
        }
    }
}
