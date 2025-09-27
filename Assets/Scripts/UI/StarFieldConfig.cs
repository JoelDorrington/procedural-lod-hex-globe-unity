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
        public int rateOverTime = 10;
        public int burstCount = 1000;
        public string shape = "donut"; // "sphere" or "donut"
        public float emissionRateOverTime;
        public float donutRadius;
        public int maxParticles;
        public float innerBiasSkew;
        public float torusTiltDegrees;
        public float startSize;
        public float startSpeed;
        public float startLifetime;
        public Vector3 position;
        public Vector3 scale = Vector3.one;
    }
}