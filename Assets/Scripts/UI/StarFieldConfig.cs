using System;
using UnityEngine;

namespace HexGlobeProject.UI
{
    [Serializable]
    public class StarFieldConfig
    {
        public string name = "Star Field";
        public float radius = 1200f;
        [Tooltip("Inner void radius for universal field (unused directly by sphere but useful for alignment with galactic void)")]
        public float voidRadius = 1000f;
        public float arc = 360f;
        // sensible defaults so a fresh, empty scene still shows stars
        public int rateOverTime = 10;
        public int burstCount = 1000;
        // default to sphere so the universal starfield is visible when no JSON config is provided
        public string shape = "Sphere"; // "Sphere" or "Donut"
        public float emissionRateOverTime = 10f;
        // donut/torus defaults (used when shape contains "donut")
        public float donutRadius = 200f;
        public int maxParticles = 1000;
        public float innerBiasSkew = 2.2f;
        public float torusTiltDegrees = 12f;
        // particle appearance defaults
        public float startSize = 0.08f;
        public float startSpeed = 0f;
        // long lifetime so emitted particles remain static and visible after emission
        public float startLifetime = 9999f;
        public Vector3 position = Vector3.zero;
        public Vector3 scale = Vector3.one;
    }
}