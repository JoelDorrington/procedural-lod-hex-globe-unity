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
        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;
        private float _startTime;
        private float _duration = 0.3f;
        private bool _fadingIn;
        private bool _active;

        public void Begin(bool fadeIn, float duration)
        {
            if (_mr == null) _mr = GetComponent<MeshRenderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _fadingIn = fadeIn;
            _duration = Mathf.Max(0.01f, duration);
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
            float t = (Time.time - _startTime) / _duration;
            if (t >= 1f) { t = 1f; _active = false; }
            float alpha = _fadingIn ? t : (1f - t);
            _mr.GetPropertyBlock(_mpb);
            _mpb.SetFloat("_Morph", alpha); // reuse _Morph as fade weight (1=fully visible)
            _mr.SetPropertyBlock(_mpb);
        }
    }
}
