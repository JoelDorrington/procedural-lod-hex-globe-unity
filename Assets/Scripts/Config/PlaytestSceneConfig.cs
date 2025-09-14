using UnityEngine;

namespace HexGlobeProject.Config
{
    [CreateAssetMenu(menuName = "HexGlobe/PlaytestSceneConfig", fileName = "PlaytestSceneConfig")]
    public class PlaytestSceneConfig : ScriptableObject
    {
        [Header("Camera")]
        public Color backgroundColor = new Color(0.066037714f, 0.066037714f, 0.066037714f, 0f);
        public CameraClearFlags clearFlags = CameraClearFlags.SolidColor;

        [Header("Universal StarField (spherical)")]
        public float universalRadius = 1200f;
        [Tooltip("Inner void radius for universal field (unused directly by sphere but useful for alignment with galactic void)")]
        public float universalVoidRadius = 1000f;
        public float universalArc = 360f;
        public int universalRateOverTime = 10;
        public int universalBurstCount = 1000;

        [Header("Galactic StarField (donut)")]
        public float galacticDonutRadius = 200f;
        // galacticRadius - galacticDonutRadius = inner void. Set galacticRadius so inner void = 800 -> 800 + 200 = 1000
        public float galacticRadius = 1000f;
        public float galacticArc = 360f;
        public int galacticRateOverTime = 10;
        public int galacticMaxParticles = 1000;
        [Tooltip("Skews torus particle sampling toward the inner ring. 1 = uniform sampling; >1 biases inward.")]
        public float galacticInnerBiasSkew = 2.2f;

        [Tooltip("Tilt (degrees) to apply to the galactic torus around the X axis.")]
        public float galacticTorusTiltDegrees = 12f;

        [Header("Directional Light (Sun)")]
        public Color sunColor = new Color(1f, 0.95686275f, 0.8392157f, 1f);
        public float sunIntensity = 1f;
        public bool sunDrawHalo = true;
        // Rotation as Euler angles
        public Vector3 sunRotationEuler = new Vector3(51.459f, -30f, 0f);
        // Position (optional, directional lights don't use position for lighting, but useful for editor placement)
        public Vector3 sunPosition = new Vector3(200f, 100f, 0f);
    }

}
