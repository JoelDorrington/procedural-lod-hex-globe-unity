using System;
using UnityEngine;

namespace HexGlobeProject.UI
{

    public class PlanetConfig
    {
        public float radius = 30f;
        public float heightExaggeration = 1f;
        public float seaLevel = 30f;
    }

    public class ShaderConfig
    {
        Vector3 PlanetCenter = new Vector3(0, 0, 0);
        float PlanetRadius = 30f;
        float SeaLevel = 30f;
        Color Color = new Color(1, 1, 1, 1); // base color fallback only
        Color ColorLow = new Color(0.1f,0.2f,0.6f,1);
        float ShallowBand = 2f;  // band above sea level to blend ocean color
        Color ShallowColor = new Color(0.12f, 0.25f, 0.55f, 1f); // ?
        Color ColorHigh = new Color(0.15f, 0.35f, 0.15f, 1f); // e.g. grassland
        Color ColorMountain = new Color(0.5f,0.5f,0.5f,1);
        float _MountainStart = 4f;
        float _MountainFull = 10f;
        float _SlopeBoost = 0.4f;
        float _SnowStart = 12f;
        float _SnowFull = 18f;
        float _SnowSlopeBoost = 0.5f;
        Color _SnowColor = new Color(0.9f,0.9f,0.95f,1f);
    }

    // Helper JSON types and factory methods for playtest scene instantiation
    [Serializable]
    public class RootConfig
    {
        public CameraConfig camera;
        public StarFieldConfig universalStarField;
        public StarFieldConfig galacticStarField;
        public DirectionalLightConfig directionalLight;
        public float planetRadius = 30f; // default, may be overridden by JSON
        public bool spawnPlanet = true; // default, may be overridden by JSON
        public float sunIntensity = 1f; // default, may be overridden by JSON
        public bool sunFlareEnabled = true; // default, may be overridden by JSON
        public string sunFlareName = "sunburst"; // default, may be overridden by JSON
        public float planetInitialRotationDegreesPerSecond = 2f; // default, may be overridden by JSON
        public float planetLODUpdateIntervalSeconds = 0.5f; // default, may be overridden by JSON
        public float planetTileLoadRadiusMultiplier = 3f; // default, may be overridden by JSON
        public float planetTileUnloadRadiusMultiplier = 4f; // default, may be overridden by JSON
        public int planetMaxConcurrentTileLoads = 2; // default, may be overridden by JSON
        public int planetMaxTilesPerFrame = 2; // default, may be overridden by JSON
        public int planetMaxActiveTiles = 100; // default, may be overridden by JSON
        public float planetHeightExaggeration = 1f; // default, may be overridden by JSON
        public float planetWaterLevel = 0f; // default, may be overridden by JSON
        public float planetRadiusForWaterLevel = 30f; // default, may be overridden by JSON
        

        public RootConfig(string jsonPath)
        {
            var json = System.IO.File.ReadAllText(jsonPath);
            var flat = JsonUtility.FromJson<PlaytestSceneConfigFlat>(json);

            // config camera
            camera = new CameraConfig();
            camera.backgroundColor = flat.backgroundColor;
            // map clearFlags int to CameraClearFlags string representation
            try
            {
                camera.clearFlags = ((CameraClearFlags)flat.clearFlags).ToString();
            }
            catch
            {
                camera.clearFlags = "SolidColor";
            }

            // config universal starfield
            universalStarField = new StarFieldConfig();
            universalStarField.shape = "Sphere";
            // prefer explicit universalRadius, otherwise fall back to universalVoidRadius if present
            universalStarField.radius = (flat.universalRadius > 0f) ? flat.universalRadius : flat.universalVoidRadius;
            universalStarField.arc = flat.universalArc;
            universalStarField.emissionRateOverTime = flat.universalRateOverTime;
            universalStarField.burstCount = flat.universalBurstCount;

            // config galaxytic starfield
            galacticStarField = new StarFieldConfig();
            galacticStarField.shape = "Donut";
            galacticStarField.donutRadius = flat.galacticDonutRadius;
            galacticStarField.radius = flat.galacticRadius;
            galacticStarField.arc = flat.galacticArc;
            galacticStarField.emissionRateOverTime = flat.galacticRateOverTime;
            galacticStarField.maxParticles = flat.galacticMaxParticles;
            galacticStarField.innerBiasSkew = flat.galacticInnerBiasSkew;
            galacticStarField.torusTiltDegrees = flat.galacticTorusTiltDegrees;

            // config sun
            directionalLight = new DirectionalLightConfig();
            directionalLight.color = flat.sunColor;
            directionalLight.intensity = flat.sunIntensity;
            // prefer sunFlareEnabled if present; fall back to legacy sunDrawHalo if provided in older JSON
            if (flat.sunFlareEnabled)
            {
                directionalLight.sunFlareEnabled = flat.sunFlareEnabled;
            }
            else
            {
                // legacy key may be sunDrawHalo
                directionalLight.sunFlareEnabled = flat.sunDrawHalo;
            }
            if (!string.IsNullOrEmpty(flat.sunFlareName)) directionalLight.flareName = flat.sunFlareName;
            directionalLight.flareBrightness = flat.sunFlareBrightness;
            directionalLight.rotation = flat.sunRotationEuler;
            directionalLight.position = flat.sunPosition;
        }

    }
}