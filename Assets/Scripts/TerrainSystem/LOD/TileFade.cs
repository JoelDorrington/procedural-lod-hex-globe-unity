using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Simple per-tile fade controller; adjusts material _Morph alpha factor (or color multiplier) over time.
    /// Assumes material uses instance property block.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class TileFade : MonoBehaviour
    {
        public enum FadeCurve { Linear, Smooth, EaseIn, EaseOut }
        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;
        private float _startTime;
        private float _delay;
        private float _duration = 0.3f;
        private bool _fadingIn;
        private bool _active;
        private FadeCurve _curve = FadeCurve.Smooth;

        public void Begin(bool fadeIn, float duration, float delay = 0f, FadeCurve curve = FadeCurve.Smooth)
        {
            if (_mr == null) _mr = GetComponent<MeshRenderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _fadingIn = fadeIn;
            _duration = Mathf.Max(0.01f, duration);
            _delay = Mathf.Max(0f, delay);
            _curve = curve;
            _startTime = Time.time;
            _active = true;
            Update();
        }

        private void LateUpdate()
        {
            if (_active) Update();
        }

        private void Update()
        {
            if (!_active) return;
            float elapsed = Time.time - _startTime;
            if (elapsed < _delay) return; // wait until delay passes
            float t = (elapsed - _delay) / _duration;
            if (t >= 1f) { t = 1f; _active = false; }
            // Curve shaping
            float c;
            switch (_curve)
            {
                case FadeCurve.EaseIn: c = t * t; break;
                case FadeCurve.EaseOut: c = 1f - (1f - t) * (1f - t); break;
                case FadeCurve.Smooth: c = t * t * (3f - 2f * t); break; // smoothstep
                default: c = t; break;
            }
            float alpha = _fadingIn ? c : (1f - c);
            _mr.GetPropertyBlock(_mpb);
            _mpb.SetFloat("_Morph", alpha); // reuse _Morph as fade weight (1=fully visible)
            _mr.SetPropertyBlock(_mpb);
        }
    }
}
