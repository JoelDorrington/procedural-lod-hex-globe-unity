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
                            var json = System.IO.File.ReadAllText(jsonPath);
                            cfg = JsonUtility.FromJson<RootConfig>(json);
                            // Fallback: older/flat PlaytestSceneConfig may serialize fields at root rather than nested.
                            // If galacticStarField is null or missing, attempt to map flat fields into it.
                            if (cfg != null && cfg.galacticStarField != null)
                            {
                                try
                                {
                                    // Parse into a quick helper object matching the flat PlaytestSceneConfig layout
                                    var flat = JsonUtility.FromJson<PlaytestSceneConfigFlat>(json);
                                    if (flat != null)
                                    {
                                        cfg.galacticStarField = new StarFieldConfig();
                                        cfg.galacticStarField.shape = "Donut";
                                        cfg.galacticStarField.donutRadius = flat.galacticDonutRadius;
                                        cfg.galacticStarField.radius = flat.galacticRadius;
                                        cfg.galacticStarField.arc = flat.galacticArc;
                                        cfg.galacticStarField.emissionRateOverTime = flat.galacticRateOverTime;
                                        cfg.galacticStarField.maxParticles = flat.galacticMaxParticles;
                                        cfg.galacticStarField.innerBiasSkew = flat.galacticInnerBiasSkew;
                                        cfg.galacticStarField.torusTiltDegrees = flat.galacticTorusTiltDegrees;
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
            public bool drawHalo = true;
            public Vector3 rotation = new Vector3(51.459f, -30f, 0f);
            public Vector3 position = new Vector3(200f, 100f, 0f);
        }

        // Helper mapping for older flat PlaytestSceneConfig JSON files
        [Serializable]
        public class PlaytestSceneConfigFlat
        {
            public float universalRadius;
            public float universalVoidRadius;
            public float universalArc;
            public int universalRateOverTime;
            public int universalBurstCount;

            public float galacticDonutRadius;
            public float galacticRadius;
            public float galacticArc;
            public int galacticRateOverTime;
            public int galacticMaxParticles;
            public float galacticInnerBiasSkew;
            public float galacticTorusTiltDegrees;

            // directional light fields (optional)
            public Color sunColor;
            public float sunIntensity;
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
            var go = new GameObject(l.name ?? "Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = l.color;
            light.intensity = l.intensity;
            // If the config requests a halo/glare, try to attach a Flare asset to the light.
            if (l.drawHalo)
            {
                Flare flare = null;
#if UNITY_EDITOR
                // Editor: try to find any Flare asset in the project called 'PlaytestSunFlare' or any flare if available.
                var guids = UnityEditor.AssetDatabase.FindAssets("t:Flare PlaytestSunFlare");
                if (guids != null && guids.Length > 0)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    flare = UnityEditor.AssetDatabase.LoadAssetAtPath<Flare>(path);
                }
                else
                {
                    // fallback: find any Flare
                    var all = UnityEditor.AssetDatabase.FindAssets("t:Flare");
                    if (all != null && all.Length > 0)
                    {
                        var p = UnityEditor.AssetDatabase.GUIDToAssetPath(all[0]);
                        flare = UnityEditor.AssetDatabase.LoadAssetAtPath<Flare>(p);
                    }
                }
#else
                // Runtime: look for a flare in Resources named PlaytestSunFlare
                flare = Resources.Load<Flare>("PlaytestSunFlare");
#endif
                if (flare != null)
                {
                    light.flare = flare;
                }
                else
                {
                    Debug.LogWarning("SceneBootstrapper: sun halo requested but no Flare asset found in project (searching for 'PlaytestSunFlare').");
                }
            }
            else
            {
                // Legacy comment: no halo requested.
            }
            go.transform.rotation = Quaternion.Euler(l.rotation);
            go.transform.position = l.position;
        }
    }
}
