using UnityEngine;

namespace HexGlobeProject.TerrainSystem
{
    /// <summary>
    /// Shared utility for realistic height remapping so that both static and LOD systems stay consistent.
    /// </summary>
    public static class HeightRemapper
    {
        /// <summary>
        /// Middle-ground remap: simple, fast. Assumes raw in approx -1..1.
        /// Steps: clamp -> scale to max -> gentle compression of upper tail -> sparse peaks.
        /// Uses existing config fields: maxElevationPercent, typicalEnvelopeFill, envelopeCompressionExponent, peakProbability, peakExtraHeightPercent.
        /// </summary>
        public static float MinimalRemap(float rawHeight, TerrainConfig config)
        {
            if (config == null) return rawHeight;
            float maxH = config.baseRadius * config.maxElevationPercent;
            if (maxH <= 0f) return 0f;
            float clamped = Mathf.Clamp(rawHeight, -1f, 1f); // normalize domain
            float h = clamped * maxH;
            float sign = Mathf.Sign(h);
            float mag = Mathf.Abs(h);
            if (mag > 0f)
            {
                float fill = Mathf.Clamp01(config.typicalEnvelopeFill <= 0f ? 0.9f : config.typicalEnvelopeFill);
                if (fill > 0.999f) fill = 0.999f;
                float t = mag / maxH; // 0..1
                if (t > fill && sign > 0f) // only compress positive elevations
                {
                    float tailT = (t - fill) / (1f - fill);
                    tailT = Mathf.Pow(tailT, Mathf.Max(1f, config.envelopeCompressionExponent));
                    t = Mathf.Lerp(fill, 1f, tailT);
                    // Sparse peak chance
                    if (config.peakProbability > 0f && config.peakExtraHeightPercent > 0f)
                    {
                        // Hash based on raw height to remain deterministic across LOD (could add direction later)
                        float hash = Mathf.Abs(Mathf.Sin(rawHeight * 123.456f + maxH * 0.137f));
                        if (hash < config.peakProbability)
                        {
                            float extra = config.baseRadius * config.peakExtraHeightPercent * (0.2f + 0.8f * hash / Mathf.Max(1e-5f, config.peakProbability));
                            return sign * (t * maxH + extra);
                        }
                    }
                }
                mag = t * maxH;
            }
            return sign * mag;
        }
        /// <summary>
        /// Remap a raw sampled height (in provider units scaled externally) to a physically bounded elevation
        /// distribution clustered near a modal elevation with a hard max.
        /// </summary>
        public static float RemapRealistic(float rawHeight, TerrainConfig config)
        {
            if (config == null || !config.realisticHeights) return rawHeight;
            float maxRad = config.baseRadius * config.maxElevationPercent;
            float modalRad = config.baseRadius * config.modalElevationPercent;
            if (maxRad <= 1e-6f) return rawHeight;
            float frac = Mathf.Clamp(rawHeight / maxRad, -1f, 1f); // signed fraction of max
            float absF = Mathf.Abs(frac);
            // Smoothstep for gentle emphasis on lower magnitudes then blend for skew.
            float smooth = absF * absF * (3f - 2f * absF);
            float biased = Mathf.Lerp(absF, smooth, 0.65f);
            float mag = modalRad * biased + (maxRad - modalRad) * (biased * biased);
            float h = mag * Mathf.Sign(frac);

            if (config.useElevationEnvelope && h > 0f)
            {
                // Envelope baseline: sea radius * envelopeScaleOverSea minus baseRadius gives max target height inside envelope.
                float seaRadius = config.baseRadius + config.seaLevel;
                float envelopeRadius = seaRadius * config.envelopeScaleOverSea;
                float envelopeMaxHeight = envelopeRadius - config.baseRadius;
                if (envelopeMaxHeight > 0f)
                {
                    float t = Mathf.Clamp01(h / envelopeMaxHeight);
                    // Compress top portion so most samples stay below typical fill.
                    float fill = config.typicalEnvelopeFill;
                    if (t > fill)
                    {
                        float tailT = (t - fill) / Mathf.Max(1e-5f, 1f - fill); // 0..1 over tail band
                        tailT = Mathf.Pow(tailT, config.envelopeCompressionExponent); // strong compression
                        t = Mathf.Lerp(fill, 1f, tailT);
                        // Sparse peaks chance: random hash from height fraction to keep deterministic-ish
                        if (config.peakProbability > 0f && config.peakExtraHeightPercent > 0f)
                        {
                            // Simple hash via sin
                            float hash = Mathf.Abs(Mathf.Sin(h * 12.9898f + envelopeMaxHeight * 78.233f));
                            if (hash < config.peakProbability)
                            {
                                h = envelopeMaxHeight + config.baseRadius * config.peakExtraHeightPercent * hash; // hash scales peak
                                return h; // allow peak beyond envelope
                            }
                        }
                    }
                    h = t * envelopeMaxHeight; // enforce envelope
                }
            }
            return h;
        }
    }
}
