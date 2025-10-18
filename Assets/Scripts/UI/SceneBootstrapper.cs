using System;
using System.Reflection;
using System.Collections;
using UnityEngine;
using HexGlobeProject.HexMap.Runtime;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Graphics;

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
            // allow a frame for UI to update
            yield return null;

            // create starfields (may be heavy); run as coroutine so we can yield while emitting many particles
            yield return StartCoroutine(CreateStarFieldFromConfig(cfg?.universalStarField, "UniversalStarField"));
            onProgress?.Invoke(0.7f);
            yield return null;
            yield return StartCoroutine(CreateStarFieldFromConfig(cfg?.galacticStarField, "GalacticStarField"));
            onProgress?.Invoke(0.85f);
            yield return null;

            // create directional light
            CreateDirectionalLightFromConfig(cfg?.directionalLight);
            // Ensure in-game controls UI is present so the player can Advance Turn / Pause during playtests
            EnsureInGameControls();
            yield return null;

            // Optionally spawn the planet if the config requests it
            bool shouldSpawnPlanet = false;
            try
            {
                if (cfg != null && cfg is RootConfig rc)
                {
                    // RootConfig is built from the flat PlaytestSceneConfig JSON; check flat.spawnPlanet via reading the file again if needed
                    // The PlaytestSceneConfigFlat is parsed into RootConfig in the RootConfig ctor, so inspect the JSON directly for spawnPlanet
                    var json = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, playtestConfigRelativePath.Replace("Assets/", "")));
                    // quick check for "spawnPlanet":true (case-insensitive)
                    if (!string.IsNullOrEmpty(json) && json.IndexOf("\"spawnPlanet\"", StringComparison.OrdinalIgnoreCase) >= 0 && json.IndexOf("true", json.IndexOf("\"spawnPlanet\"", StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        shouldSpawnPlanet = true;
                    }
                }
            }
            catch { }
            if (shouldSpawnPlanet)
            {
                CreatePlanetUnderCameraTarget();
                // planet creation may schedule heavy work on Start; allow a frame for initialization
                // wait for Planet.GeneratePlanet to complete (poll isGenerated). Timeout after 10 seconds to avoid deadlock.
                float waitStart = Time.realtimeSinceStartup;
                float timeout = 10f;
                // find the Planet component on the created GameObject (named 'Planet')
                HexGlobeProject.HexMap.Planet planetComp = null;
                try { planetComp = GameObject.Find("Planet")?.GetComponent<HexGlobeProject.HexMap.Planet>(); } catch { planetComp = null; }
                while (planetComp != null && !planetComp.isGenerated && Time.realtimeSinceStartup - waitStart < timeout)
                {
                    yield return null;
                }
                yield return null;
            }
            onProgress?.Invoke(1f);
            yield return null;
            onComplete?.Invoke();

            yield break;
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

        private IEnumerator CreateStarFieldFromConfig(StarFieldConfig s, string name)
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
                // disable roll so particles don't rotate around their forward axis
                try { psr.allowRoll = false; } catch { }
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
                    if ((i & 63) == 0) yield return null; // yield every 64 emits to keep UI responsive
                }
                    ps.Pause();
                    yield break;
                }
                else
                {
                    // Sphere shape: use the particle system's Shape module with radiusThickness and let it emit
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = s.radius;
                    try { shape.radiusThickness = Mathf.Clamp01(s.radiusThickness); } catch { }
                    shape.arc = s.arc;

                    if (s.burstCount > 0)
                    {
                        // Play briefly so configured bursts happen, then pause to freeze them
                        ps.Clear();
                        ps.Play();
                        yield return null;
                        ps.Pause();
                        yield break;
                    }
                    else
                    {
                        ps.Clear();
                        ps.Simulate(1f, true, true);
                        ps.Pause();
                        yield break;
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
                        lf.fadeSpeed = 100f;
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
                                catch
                                {
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

            // Attach SunFlareOccluder if a Planet/CameraTarget exists in the scene to occlude the flare
            try
            {
                var planetGO = GameObject.Find("Planet") ?? GameObject.Find("CameraTarget");
                if (planetGO != null)
                {
                    var oc = go.GetComponent<Visual.SunFlareOccluder>();
                    if (oc == null) oc = go.AddComponent<Visual.SunFlareOccluder>();
                    oc.targetLight = targetLight;
                    oc.lensFlare = go.GetComponent<LensFlare>();
                    oc.planetTransform = planetGO.transform;
                    oc.fadeSpeed = 9999f;
                    // attempt to pull radius from TerrainConfig asset if present
                    try
                    {
                        // Try to load TerrainConfig via editor API if available, otherwise fall back gracefully
                        var conf = LoadAssetAtPathRuntimeSafe<TerrainConfig>("Assets/Configs/TerrainConfig.asset");
                        if (conf != null) oc.planetRadius = conf.baseRadius;
                        else
                        {
                            // attempt to find any TerrainConfig asset by type (editor-only flow via reflection)
                            var gu = FindAssetsRuntime("t:TerrainConfig");
                            if (gu != null && gu.Length > 0)
                            {
                                var p = GUIDToAssetPathRuntime(gu[0]);
                                if (!string.IsNullOrEmpty(p))
                                {
                                    var conf2 = LoadAssetAtPathRuntimeSafe<TerrainConfig>(p);
                                    if (conf2 != null) oc.planetRadius = conf2.baseRadius;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // Create a Planet object under the existing CameraTarget so the planet is centered on the camera target origin
        private void CreatePlanetUnderCameraTarget()
        {
            var target = GameObject.Find("CameraTarget");
            if (target == null)
            {
                Debug.LogWarning("CreatePlanetUnderCameraTarget: CameraTarget not found.");
                return;
            }

            // Reuse the existing CameraTarget GameObject as the Planet root. Rename for clarity.
            var planetGO = target;
            planetGO.name = "Planet";
            planetGO.layer = LayerMask.NameToLayer("TerrainTiles") >= 0 ? LayerMask.NameToLayer("TerrainTiles") : planetGO.layer;
            planetGO.transform.localPosition = new Vector3(0f, 0f, -3.3f);

            // Add or get Planet component on the CameraTarget (now Planet)
            var planet = planetGO.GetComponent<HexMap.Planet>();
            if (planet == null) planet = planetGO.AddComponent<HexMap.Planet>();
            // Apply default playtest values (matching your YAML defaults)
            planet.wireframeColor = new Color(0.4811321f, 0.4811321f, 0.4811321f, 1f);
            planet.lineThickness = 1f;
            // enableWireframe is a private serialized field; set it via reflection
            try
            {
                var pf = typeof(HexMap.Planet).GetField("enableWireframe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (pf != null) pf.SetValue(planet, true);
            }
            catch { }
            planet.dualSmoothingIterations = 64;
            // Subdivision level is now controlled by TerrainConfig. Planet.GeneratePlanet reads the canonical value at runtime.
            planet.hideOceanRenderer = true;

            // Add or get PlanetTileVisibilityManager component on the same GameObject
            var mgr = planetGO.GetComponent<PlanetTileVisibilityManager>();
            if (mgr == null) mgr = planetGO.AddComponent<PlanetTileVisibilityManager>();

            // Attempt to assign CameraController reference from existing main camera or controller created earlier
            try
            {
                var camGO = GameObject.Find("Main Camera");
                if (camGO != null)
                {
                    // CameraController lives in the global namespace; qualify with global:: to avoid namespace collisions
                    var controller = camGO.GetComponent<CameraController>();
                    if (controller != null)
                    {
                        mgr.GameCamera = controller;
                    }
                    else
                    {
                        // fallback: find any CameraController in scene
                        var any = FindAnyObjectByType<CameraController>();
                        if (any != null) mgr.GameCamera = any;
                    }
                }
            }
            catch { }

            // Assign planetTransform to the created planet's transform (private serialized field)
            try
            {
                var f = typeof(PlanetTileVisibilityManager).GetField("planetTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) f.SetValue(mgr, planetGO.transform);
            }
            catch { }

            // Assign terrainMaterial if a default exists at Assets/Materials/Land.mat
            try
            {
                    var mat = LoadAssetAtPathRuntimeSafe<Material>("Assets/Materials/Land.mat");
                    if (mat != null)
                    {
                        var tf = typeof(PlanetTileVisibilityManager).GetField("terrainMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (tf != null) tf.SetValue(mgr, mat);
                        // Setup overlay cubemap and assign to material for immediate visual feedback
                        try { SetupOverlayCubemap(planetGO, mat); } catch { }

                        // Apply terrain config (if assigned later) or leave until config assignment block runs
                    }
            }
            catch { }

            // Assign TerrainConfig asset if present at Assets/Configs/TerrainConfig.asset
            try
            {
                // Attempt to load TerrainConfig via editor API when available, otherwise try to discover via reflected find.
                var conf = LoadAssetAtPathRuntimeSafe<TerrainConfig>("Assets/Configs/TerrainConfig.asset");
                if (conf == null)
                {
                    var gu = FindAssetsRuntime("t:TerrainConfig");
                    if (gu != null && gu.Length > 0)
                    {
                        var p = GUIDToAssetPathRuntime(gu[0]);
                        if (!string.IsNullOrEmpty(p)) conf = LoadAssetAtPathRuntimeSafe<TerrainConfig>(p);
                    }
                }
                if (conf != null)
                {
                    conf.overlayEnabled = false;
                    // config is a public field on the manager; assign directly
                    try { mgr.config = conf; } catch { }

                    // Apply shader globals now that TerrainConfig is available.
                    try
                    {
                        // retrieve assigned material from manager via reflection
                        var matField = typeof(PlanetTileVisibilityManager).GetField("terrainMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        Material assignedMat = null;
                        if (matField != null) assignedMat = matField.GetValue(mgr) as Material;
                        if (assignedMat != null)
                        {
                            TerrainShaderGlobals.Apply(conf, assignedMat);
                            try { mgr.ApplyShaderConfigToAllTiles(conf); } catch { }
                        }
                    }
                    catch { /* non-critical apply failure */ }
                }
            }
            catch { }

            // Manager lives on the same GameObject as the Planet; no parenting necessary
        }

        // Create or find an OverlayCubemapGenerator on the planet root, generate a cubemap, and assign to the provided material.
        private void SetupOverlayCubemap(GameObject planetRoot, Material targetMaterial)
        {
            if (planetRoot == null || targetMaterial == null) return;
            // Try to find existing generator
            // OverlayCubemapGenerator gen = planetRoot.GetComponent<OverlayCubemapGenerator>();
            // if (gen == null) gen = planetRoot.AddComponent<OverlayCubemapGenerator>();

            // var conf = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainConfig>("Assets/Configs/TerrainConfig.asset");

            // Configure generator defaults for immediate visual feedback
            // gen.faceSize = 128;
            // gen.hexSize = 0.05f;
            // gen.edgeThickness = 0.007f;
            // gen.saveAsset = true;
            // gen.assetName = "DualOverlayCube_Playtest";

            // Generate cubemap (editor-time API is safe to call at runtime in editor)
            try
            {
                // var cube = gen.GenerateCubemap(conf.baseRadius, conf.icosphereSubdivisions, 512);
                var cube = UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Resources/DualOverlayCube_Playtest.cubemap");
                if (cube != null)
                {
                    targetMaterial.SetTexture("_DualOverlayCube", cube);
                    targetMaterial.SetFloat("_OverlayEnabled", 0f);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("SetupOverlayCubemap: failed to generate or assign cubemap: " + ex.Message);
            }
        }

        // Runtime-safe helpers that try to call UnityEditor.AssetDatabase via reflection when available.
        // These keep the code identical between editor and player builds while avoiding direct UnityEditor references.
        private static T LoadAssetAtPathRuntimeSafe<T>(string path) where T : UnityEngine.Object
        {
            try
            {
                // Try to find UnityEditor.AssetDatabase type dynamically
                var asm = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var a in asm)
                {
                    try
                    {
                        var t = a.GetType("UnityEditor.AssetDatabase");
                        if (t != null)
                        {
                            var m = t.GetMethod("LoadAssetAtPath", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
                            if (m != null)
                            {
                                var res = m.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { path });
                                return res as T;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // fallback: try Resources.Load by trimming extensions and folder prefixes
            try
            {
                string trimmed = path;
                if (trimmed.StartsWith("Assets/")) trimmed = trimmed.Substring("Assets/".Length);
                // remove extension
                var dot = trimmed.LastIndexOf('.');
                if (dot >= 0) trimmed = trimmed.Substring(0, dot);
                return Resources.Load<T>(trimmed);
            }
            catch { }
            return null;
        }

        private static string[] FindAssetsRuntime(string filter)
        {
            try
            {
                var asm = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var a in asm)
                {
                    try
                    {
                        var t = a.GetType("UnityEditor.AssetDatabase");
                        if (t != null)
                        {
                            var m = t.GetMethod("FindAssets", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
                            if (m != null)
                            {
                                var res = m.Invoke(null, new object[] { filter }) as string[];
                                return res;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        private static string GUIDToAssetPathRuntime(string guid)
        {
            try
            {
                var asm = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var a in asm)
                {
                    try
                    {
                        var t = a.GetType("UnityEditor.AssetDatabase");
                        if (t != null)
                        {
                            var m = t.GetMethod("GUIDToAssetPath", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
                            if (m != null)
                            {
                                var res = m.Invoke(null, new object[] { guid }) as string;
                                return res;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        // Ensure an InGameControlsController exists in the scene (create a persistent GameObject if necessary)
        private void EnsureInGameControls()
        {
            try
            {
                var existing = UnityEngine.Object.FindAnyObjectByType<HexGlobeProject.UI.InGameControlsController>();
                if (existing != null) return;

                var go = new GameObject("InGameControls");
                DontDestroyOnLoad(go);
                go.AddComponent<HexGlobeProject.UI.InGameControlsController>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("EnsureInGameControls: failed to create InGameControlsController: " + ex.Message);
            }
        }
    }
}
