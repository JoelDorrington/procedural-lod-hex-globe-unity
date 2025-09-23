using System;
using UnityEngine;

namespace HexGlobeProject.UI
{

    // Helper JSON types and factory methods for playtest scene instantiation
    [Serializable]
    public class RootConfig
    {
        CameraConfig camera;
        StarFieldConfig universalStarField;
        StarFieldConfig galacticStarField;
        DirectionalLightConfig directionalLight;
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