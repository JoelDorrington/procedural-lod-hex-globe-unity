using System;
using System.Collections;
using UnityEngine;
using HexGlobeProject.HexMap.Model;
using HexGlobeProject.HexMap.Runtime;

namespace HexGlobeProject.UI
{
    /// <summary>
    /// Simple non-invasive scene bootstrapper for quick playtests.
    /// It can either build a tiny deterministic topology and spawn a unit, or enter a "space-only" mode
    /// where it creates camera, starfields and a directional light from a JSON config and places a camera target at origin.
    /// Reports coarse progress to the MainMenuController.
    /// </summary>
    public class SceneBootstrapper : MonoBehaviour, IBootstrapper
    {
        public UnitManager unitManagerPrefab; // optional prefab to instantiate
        [Tooltip("If true, skip planet generation and start in an empty space scene using the playtest config JSON.")]
        public bool spaceOnly = true;
        [Tooltip("Relative path under Assets to a playtest config JSON. Resolved via Application.dataPath.")]
        public string playtestConfigRelativePath = "Configs/playtest_scene_config.json";

        public IEnumerator RunBootstrapper(Action<float> onProgress, Action<string> onError, Action onComplete)
        {
            try
            {
                if (spaceOnly)
                {
                    // Space-only bootstrap: create an empty camera target at origin, create camera, starfields and directional light.
                    onProgress?.Invoke(0.05f);

                    // create camera target at origin
                    var cameraTarget = new GameObject("CameraTarget");
                    cameraTarget.transform.position = Vector3.zero;

                    onProgress?.Invoke(0.15f);

                    // attempt to load JSON config
                    var jsonPath = System.IO.Path.Combine(Application.dataPath, playtestConfigRelativePath.Replace("Assets/", ""));
                    RootConfig cfg = null;
                    if (System.IO.File.Exists(jsonPath))
                    {
                        try
                        {
                            cfg = new RootConfig(jsonPath);
                            if (cfg != null)
                            {
                                try
                                {
                                    // Parse into a quick helper object matching the flat PlaytestSceneConfig layout
                                    var flat = JsonUtility.FromJson<PlaytestSceneConfigFlat>("");
                                    if (flat != null)
                                    {

                                    }
                                }
                                catch { /* ignore fallback mapping failures */ }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("SceneBootstrapper: failed to parse playtest config JSON: " + ex.Message);
                            cfg = null;
                        }
                    }
                    else
                    {
                        Debug.LogWarning("SceneBootstrapper: playtest config JSON not found at " + jsonPath + ". Falling back to defaults.");
                    }

                    // create camera
                    CreateCameraFromConfig(cfg);
                    onProgress?.Invoke(0.45f);

                    // create starfields
                    CreateStarFieldFromConfig(cfg?.universalStarField, "UniversalStarField");
                    onProgress?.Invoke(0.7f);
                    CreateStarFieldFromConfig(cfg?.galacticStarField, "GalacticStarField");
                    onProgress?.Invoke(0.85f);

                    // create directional light
                    CreateDirectionalLightFromConfig(cfg?.directionalLight);
                    onProgress?.Invoke(1f);
                    onComplete?.Invoke();
                }
                else
                {
                    // original behaviour: build a tiny topology (0..0.4)
                    onProgress?.Invoke(0.05f);
                    var cfg = new TopologyConfig();
                    cfg.entries = new System.Collections.Generic.List<TopologyConfig.TileEntry>();

                    // Build a tiny 4-node test (a cross)
                    cfg.entries.Add(new TopologyConfig.TileEntry { tileId = 100, neighbors = new[] { 101, 102 }, center = Vector3.right });
                    cfg.entries.Add(new TopologyConfig.TileEntry { tileId = 101, neighbors = new[] { 100, 103 }, center = Vector3.up });
                    cfg.entries.Add(new TopologyConfig.TileEntry { tileId = 102, neighbors = new[] { 100, 103 }, center = Vector3.left });
                    cfg.entries.Add(new TopologyConfig.TileEntry { tileId = 103, neighbors = new[] { 101, 102 }, center = Vector3.down });

                    var topology = TopologyBuilder.Build(cfg, new SparseMapIndex());
                    Debug.Log("SceneBootstrapper: topology built");
                    onProgress?.Invoke(0.35f);

                    // Stage 2: create a managed GameModel (0.35..0.7)
                    Debug.Log("SceneBootstrapper: initializing GameModel");
                    var model = new GameModel();
                    model.Initialize(topology);
                    onProgress?.Invoke(0.7f);

                    // Stage 3: spawn UnitManager and one unit (0.7..1.0)
                    UnitManager um = null;
                    if (unitManagerPrefab != null)
                    {
                        var go = Instantiate(unitManagerPrefab.gameObject);
                        um = go.GetComponent<UnitManager>();
                    }
                    else
                    {
                        var go = new GameObject("UnitManager");
                        um = go.AddComponent<UnitManager>();
                    }

                    Debug.Log("SceneBootstrapper: creating UnitManager and wiring model/topology");
                    // wire topology and model for playtest
                    um.topology = topology;
                    um.modelManaged = model;
                    um.planetRadius = 10f;
                    um.planetTransform = null;

                    onProgress?.Invoke(0.85f);

                    // create a simple visible primitive to use as the unit prefab if none provided
                    if (um.unitPrefab == null)
                    {
                        Debug.Log("SceneBootstrapper: creating temporary unit prefab (sphere)");
                        var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        temp.name = "TestUnitPrefab";
                        temp.transform.localScale = Vector3.one * 0.6f;
                        // give it a simple color
                        var rend = temp.GetComponent<Renderer>();
                        if (rend != null)
                        {
                            var mat = new Material(Shader.Find("Standard"));
                            mat.color = Color.green;
                            rend.sharedMaterial = mat;
                        }
                        // keep this prefab in the scene hidden - we will instantiate clones of it
                        temp.SetActive(false);
                        um.unitPrefab = temp;
                    }

                    Debug.Log("SceneBootstrapper: spawning test unit at node 0");
                    um.SpawnUnitAtNode(0, 1);

                    onProgress?.Invoke(1f);
                    onComplete?.Invoke();
                }
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message + "\n" + ex.StackTrace);
            }

            yield break;
        }


        // Helper JSON types and factory methods for playtest scene instantiation
        [Serializable]
        public class RootConfig
        {

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

            public CameraConfig camera;
            public StarFieldConfig universalStarField;
            public StarFieldConfig galacticStarField;
            public DirectionalLightConfig directionalLight;
        }

        [Serializable]
        public class CameraConfig
        {
            public Color backgroundColor = new Color(0.066037714f, 0.066037714f, 0.066037714f, 0f);
            public string clearFlags = "SolidColor";
            public float fieldOfView = 60f;
            public float nearClip = 0.3f;
            public float farClip = 10000f;
            public Vector3 position = Vector3.zero;
        }

        [Serializable]
        public class StarFieldConfig
        {
            public string shape;
            public float radius = 800f;
            public float donutRadius = 200f;
            public float arc = 360f;
            public float startLifetime = 100000000f;
            public float startSpeed = 0.001f;
            public float startSize = 4f;
            public int emissionRateOverTime = 10;
            public int burstCount = 1000;
            public int maxParticles = 1000;
            public Vector3 position = Vector3.zero;
            public Vector3 scale = Vector3.one;
            // Tunables for torus (galactic) emission
            public float innerBiasSkew = 2.2f;
            public float torusTiltDegrees = 12f;
        }

        [Serializable]
        public class DirectionalLightConfig
        {
            public string name = "Directional Light";
            public Color color = Color.white;
            public float intensity = 1f;
            public bool sunFlareEnabled = true;
            public string flareName = "sunburst";
            public float flareBrightness = 1f;
            public Vector3 rotation = new Vector3(51.459f, -30f, 0f);
            public Vector3 position = new Vector3(200f, 100f, 0f);
        }

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

        private void CreateCameraFromConfig(RootConfig root)
        {
            CameraConfig c = root?.camera ?? new CameraConfig();
            var go = new GameObject("Main Camera");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = c.backgroundColor;
            cam.fieldOfView = c.fieldOfView;
            cam.nearClipPlane = c.nearClip;
            cam.farClipPlane = c.farClip;
            go.transform.position = c.position;
            // ensure there's an AudioListener only if none exist
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
            {
                go.AddComponent<AudioListener>();
            }

            // Add FlareLayer so legacy flares assigned to lights will render in the Built-in pipeline.
            // This is safe to call even if FlareLayer is not supported by current pipeline; Unity will ignore it.
            try
            {
                if (go.GetComponent<FlareLayer>() == null)
                {
                    go.AddComponent<FlareLayer>();
                }
            }
            catch { /* ignore if FlareLayer not available on this Unity version/pipeline */ }

            // Configure CameraController if a CameraTarget exists
            var targetGO = GameObject.Find("CameraTarget");
            if (targetGO != null)
            {
                var controller = go.AddComponent<CameraController>();
                controller.target = targetGO.transform;
                // Values from MainScene-backup: distance ~80, minDistance ~31, maxDistance ~90
                controller.distance = 80f;
                controller.minDistance = 31f;
                controller.maxDistance = 90f;
                // default lat/long
                controller.latitude = 0f;
                controller.longitude = 0f;
                // tune zoom smoothing to feel similar to scene
                controller.zoomSmoothTime = 0.15f;
            }
        }

        private void CreateStarFieldFromConfig(StarFieldConfig s, string name)
        {
            if (s == null) s = new StarFieldConfig();
            var go = new GameObject(name);
            go.transform.position = s.position;
            go.transform.localScale = s.scale;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = s.startLifetime;
            main.startSpeed = s.startSpeed;
            main.startSize = s.startSize;
            // Ensure particles are white by default (the scene expects white stars)
            main.startColor = Color.white;
            main.maxParticles = s.maxParticles;

            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(s.emissionRateOverTime);
            // Configure burst at time 0 if burstCount provided
            if (s.burstCount > 0)
            {
                var burst = new ParticleSystem.Burst(0f, (short)Mathf.Clamp(s.burstCount, 0, short.MaxValue));
                emission.SetBursts(new ParticleSystem.Burst[] { burst });
            }

            var shape = ps.shape;
            // If spherical and radius is not set or zero, default to a universal void radius of 1000
            if (string.IsNullOrEmpty(s.shape) || s.shape.ToLower().Contains("sphere"))
            {
                if (s.radius <= 0f)
                {
                    s.radius = 1000f; // universal void default
                }
            }
            // Ensure particles are static after initial burst: disable looping and playOnAwake
            main.playOnAwake = false;
            main.loop = false;

            // ensure renderer uses a particle-friendly unlit material and white tint
            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                var mat = new Material(Shader.Find("Particles/Standard Unlit"));
                mat.color = Color.white;
                psr.material = mat;
            }

            // If config requests a donut/torus, procedurally emit particles on a torus surface and pause.
            if (!string.IsNullOrEmpty(s.shape) && s.shape.ToLower().Contains("donut"))
            {
                // Torus parameters: major radius R, minor radius r
                float R = s.radius; // distance from center to tube center
                float r = s.donutRadius; // tube radius

                // Determine how many particles to emit: prefer burstCount if present, otherwise maxParticles.
                int count = (s.burstCount > 0) ? s.burstCount : s.maxParticles;
                count = Mathf.Clamp(count, 0, s.maxParticles);

                ps.Clear();

                var emitParams = new ParticleSystem.EmitParams();
                emitParams.startSize = s.startSize;
                emitParams.startLifetime = s.startLifetime;
                emitParams.startColor = Color.white;
                emitParams.velocity = Vector3.zero;

                // Emit particles distributed over the torus surface and apply configured tilt
                Quaternion tilt = Quaternion.Euler(s.torusTiltDegrees, 0f, 0f);
                // Skew sampling toward the inner side of the torus (v around PI) so particles are denser toward the ring center.
                float innerBiasSkew = Mathf.Max(1f, s.innerBiasSkew); // >=1
                for (int i = 0; i < count; i++)
                {
                    float u = UnityEngine.Random.value * Mathf.PI * 2f; // around major circle

                    // generate a small offset around PI (inner side) with a skewed distribution
                    float t = UnityEngine.Random.value; // 0..1
                    float offsetFactor = Mathf.Pow(t, innerBiasSkew); // skews toward 0
                    // sample in range [-PI, PI] then scale by offsetFactor so values cluster near 0
                    float raw = (UnityEngine.Random.value - 0.5f) * 2f * Mathf.PI;
                    float v = Mathf.PI + raw * offsetFactor;

                    // Parametric torus equation (X,Z plane major circle):
                    float cosv = Mathf.Cos(v);
                    float sinv = Mathf.Sin(v);
                    float x = (R + r * cosv) * Mathf.Cos(u);
                    float y = r * sinv;
                    float z = (R + r * cosv) * Mathf.Sin(u);

                    Vector3 localPos = new Vector3(x, y, z);
                    // apply tilt rotation
                    localPos = tilt * localPos;
                    emitParams.position = localPos;
                    ps.Emit(emitParams, 1);
                }

                ps.Pause();
            }
            else
            {
                // default spherical shape
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = s.radius;
                shape.arc = s.arc;

                // If a burstCount is provided, procedurally emit that many particles across the sphere surface so
                // we can pause them deterministically and achieve higher density.
                if (s.burstCount > 0)
                {
                    int count = Mathf.Clamp(s.burstCount, 0, s.maxParticles);
                    ps.Clear();
                    var emitParams = new ParticleSystem.EmitParams();
                    emitParams.startSize = s.startSize;
                    emitParams.startLifetime = s.startLifetime;
                    emitParams.startColor = Color.white;
                    emitParams.velocity = Vector3.zero;
                    for (int i = 0; i < count; i++)
                    {
                        // sample uniformly on sphere by using random directions
                        var dir = UnityEngine.Random.onUnitSphere;
                        emitParams.position = dir * s.radius;
                        ps.Emit(emitParams, 1);
                    }
                    ps.Pause();
                }
                else
                {
                    // Simulate the emission up to 1 second so burst particles are created, then pause so they remain in place.
                    ps.Clear();
                    ps.Simulate(1f, true, true);
                    ps.Pause();
                }
            }
        }

        private void CreateDirectionalLightFromConfig(DirectionalLightConfig l)
        {
            if (l == null) l = new DirectionalLightConfig();

            // Try to find an existing directional light in the scene to reuse
            Light targetLight = null;
            try
            {
                var allLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                if (allLights != null)
                {
                    foreach (var lt in allLights)
                    {
                        if (lt != null && lt.type == LightType.Directional)
                        {
                            targetLight = lt;
                            break;
                        }
                    }
                }
            }
            catch { /* ignore find errors */ }

            GameObject go;
            if (targetLight != null)
            {
                go = targetLight.gameObject;
            }
            else
            {
                go = new GameObject(l.name ?? "Directional Light");
                targetLight = go.AddComponent<Light>();
                targetLight.type = LightType.Directional;
            }

            // Ensure the directional light GameObject is named "Sun" for clarity in the scene hierarchy.
            try
            {
                go.name = "Sun";
            }
            catch { /* ignore if renaming not allowed in this context */ }

            // Apply color/intensity/transform
            targetLight.color = l.color;
            targetLight.intensity = l.intensity;

            // If the config requests a halo/glare, try to attach a Flare asset to the light.
            if (l.sunFlareEnabled)
            {
                string preferred = string.IsNullOrEmpty(l.flareName) ? "sunburst" : l.flareName;

                // Runtime: look for a flare in Resources named as configured, then 'sunburst'
                Flare flare = Resources.Load<Flare>(preferred);
                if (flare == null) flare = Resources.Load<Flare>("sunburst");
                if (flare != null)
                {
                    targetLight.flare = flare;
                    // Ensure a LensFlare component is present and assigned (legacy component used by some pipelines)
                    try
                    {
                        var lf = go.GetComponent<LensFlare>();
                        if (lf == null)
                        {
                            lf = go.AddComponent<LensFlare>();
                        }
                        lf.flare = flare;
                        // Set brightness from explicit flareBrightness if provided (>0), otherwise use the directional light intensity.
                        // Note: Playtest JSON commonly sets sunIntensity to large values (e.g. 100). We will assign the value directly
                        // so designers can control brightness precisely; clamp is intentionally avoided to allow high-intensity flares.
                        if (l != null && l.flareBrightness > 0f)
                        {
                            lf.brightness = l.flareBrightness;
                        }
                        else
                        {
                            lf.brightness = l != null ? l.intensity : targetLight.intensity;
                        }
                        lf.color = targetLight.color;
                    }
                    catch { /* ignore if LensFlare not available */ }
                    // Ensure cameras have a FlareLayer so the flare will render (built-in pipeline)
                    try
                    {
                        var cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                        if (cams != null)
                        {
                            foreach (var c in cams)
                            {
                                if (c != null && c.GetComponent<FlareLayer>() == null)
                                {
                                    c.gameObject.AddComponent<FlareLayer>();
                                }
                                try
                                {
                                    // Enable HDR which can affect flare rendering intensity in some pipelines
                                    c.allowHDR = true;
                                }
                                catch {
                                    // allowHDR may not exist on all platforms/Unity versions
                                }
                                try
                                {
                                    // Force forward rendering which is compatible with legacy flares
                                    c.renderingPath = RenderingPath.Forward;
                                }
                                catch { }
                            }
                        }
                    }
                    catch { /* ignore if FlareLayer not available */ }
                    
                    // Increase likelihood the light's flare renders: prefer pixel/important render mode
                    try
                    {
                        targetLight.renderMode = LightRenderMode.ForcePixel;
                    }
                    catch { /* ignore if LightRenderMode unsupported */ }
                }
                else
                {
                    Debug.LogWarning("SceneBootstrapper: sun halo requested but no Flare asset named 'sunburst' or 'PlaytestSunFlare' found. Place a Flare in Assets/Resources/sunburst.flare or add a Flare asset to the project.");
                }
            }

            go.transform.rotation = Quaternion.Euler(l.rotation);
            go.transform.position = l.position;
        }
    }
}
