using System;
using UnityEngine;

namespace HexGlobeProject.UI
{
    // Helper mapping for older flat PlaytestSceneConfig JSON files
    [Serializable]
    public class PlaytestSceneConfigFlat
    {
        // Camera
        public Color backgroundColor;
        public int clearFlags;

        // Universal starfield
        public float universalRadius;
        public float universalVoidRadius;
        public float universalArc;
        public int universalRateOverTime;
        public int universalBurstCount;

        // Galactic starfield
        public float galacticDonutRadius;
        public float galacticRadius;
        public float galacticArc;
        public int galacticRateOverTime;
        public int galacticMaxParticles;
        public float galacticInnerBiasSkew;
        public float galacticTorusTiltDegrees;

        // Directional light (sun)
        public Color sunColor;
        public float sunIntensity;
        // legacy/alternate keys
        public bool sunDrawHalo;
        public bool sunFlareEnabled;
        public string sunFlareName;
        public float sunFlareBrightness;
        public Vector3 sunRotationEuler;
        public Vector3 sunPosition;
    }
}
