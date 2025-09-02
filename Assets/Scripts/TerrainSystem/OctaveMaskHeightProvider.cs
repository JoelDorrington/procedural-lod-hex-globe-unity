using UnityEngine;

namespace HexGlobeProject.TerrainSystem
{
    /// <summary>
    /// Wrapper that masks (excludes) higher octave contributions from an underlying provider.
    /// Assumes the wrapped provider is octave-based (Perlin/fractal style) and exposes a deterministic multi-octave API via IOctaveSampler.
    /// If the underlying provider does not implement IOctaveSampler it falls back to full sampling.
    /// </summary>
    [System.Serializable]
    public class OctaveMaskHeightProvider : TerrainHeightProviderBase, IOctaveSampler
    {
        [SerializeReference] public TerrainHeightProviderBase inner;
        [Tooltip("Inclusive maximum octave to keep (-1 = all; 0 = first octave only)")] public int maxOctave = -1;

        private IOctaveSampler _octaveSampler;

        public void SetMaxOctave(int value) => maxOctave = value;

        public override float Sample(in Vector3 unitDirection, int resolution)
        {
            if (inner == null) return 0f;
            _octaveSampler ??= inner as IOctaveSampler;
            if (_octaveSampler == null || maxOctave < 0 && maxOctave != -1) return inner.Sample(unitDirection, resolution);
            if (maxOctave < 0) return inner.Sample(unitDirection, resolution); // -1 means full
            return _octaveSampler.SampleOctaveMasked(unitDirection, maxOctave);
        }

        /// <summary>
        /// Expose masked sampling for nested wrappers.
        /// </summary>
        public float SampleOctaveMasked(in Vector3 dir, int maxInclusive)
        {
            // Temporarily override maxOctave and call Sample with appropriate resolution
            int prev = this.maxOctave;
            this.maxOctave = maxInclusive;
            // Use a reasonable resolution for octave sampling - this maintains consistency
            int samplingResolution = 32; // Base resolution for octave sampling
            float val = Sample(dir, samplingResolution);
            this.maxOctave = prev;
            return val;
        }
    }

    /// <summary>
    /// Interface for octave-addressable sampling so we can mask high frequencies without modifying base providers heavily.
    /// </summary>
    public interface IOctaveSampler
    {
        /// <summary>
        /// Return height using only octaves 0..maxInclusive (clamped to implementation's octave count).
        /// Implementations should cache where practical.
        /// </summary>
        float SampleOctaveMasked(in Vector3 dir, int maxInclusive);
    }
}
