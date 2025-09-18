using System;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace HexGlobeProject.UI
{
    /// <summary>
    /// In-game controls UI controller. Provides simple runtime UI for play-time controls such as
    /// Advance Turn, Pause/Resume and a turn timer display. If UI references are not set in the
    /// Inspector this class will create a minimal runtime UI so controls are available for quick playtests.
    /// </summary>
    public class InGameControlsController : MonoBehaviour
    {
        // Optional references assignable from inspector
        public GameObject controlsPanel; // parent panel for in-game controls
        public Button advanceTurnButton;
        public Button pauseResumeButton;
    public Button toggleHexGridButton;
        public Text turnTimerText;

        private bool isPaused = false;
        private float turnTimer = 0f; // seconds elapsed in current turn
        private Coroutine timerCoroutine;
    private bool hexGridVisible = false; // start disabled by default

        public event Action OnAdvanceTurn;
        public event Action<bool> OnPauseChanged; // bool = isPaused
    public event Action<bool> OnToggleHexGrid; // bool = hex grid visible

        void Awake()
        {
            // If the inspector didn't wire up the UI, create a minimal runtime set
            if (controlsPanel == null || advanceTurnButton == null || pauseResumeButton == null || turnTimerText == null)
            {
                SetupRuntimeUI();
            }

            // Wire up buttons to methods
            if (advanceTurnButton != null)
            {
                advanceTurnButton.onClick.RemoveAllListeners();
                advanceTurnButton.onClick.AddListener(() => { AdvanceTurn(); });
            }

            if (pauseResumeButton != null)
            {
                pauseResumeButton.onClick.RemoveAllListeners();
                pauseResumeButton.onClick.AddListener(() => { TogglePause(); });
            }

            if (toggleHexGridButton != null)
            {
                toggleHexGridButton.onClick.RemoveAllListeners();
                toggleHexGridButton.onClick.AddListener(() => { ToggleHexGrid(); });
            }

            // Ensure button label and shader state match initial value
            ApplyOverlayState();
        }

        void OnEnable()
        {
            StartTurnTimer();
            // ensure overlay state applied when controller becomes active
            ApplyOverlayState();
        }

        void OnDisable()
        {
            StopTurnTimer();
        }

        void Update()
        {
            // Update timer text each frame as a fallback if coroutine not used
            if (turnTimerText != null)
            {
                turnTimerText.text = FormatTime(turnTimer);
            }
        }

        public void AdvanceTurn()
        {
            // Raise event for game systems to handle advancing turn
            OnAdvanceTurn?.Invoke();
            // Reset timer for the next turn
            ResetTurnTimer();
        }

        public void TogglePause()
        {
            isPaused = !isPaused;
            // Update button label
            if (pauseResumeButton != null)
            {
                var txt = pauseResumeButton.GetComponentInChildren<Text>();
                if (txt != null) txt.text = isPaused ? "Resume" : "Pause";
            }
            // Inform listeners
            OnPauseChanged?.Invoke(isPaused);

            // Stop or start the timer coroutine
            if (isPaused) StopTurnTimer(); else StartTurnTimer();
        }

        public void ToggleHexGrid()
        {
            hexGridVisible = !hexGridVisible;
            // Update button label
            if (toggleHexGridButton != null)
            {
                var txt = toggleHexGridButton.GetComponentInChildren<Text>();
                if (txt != null) txt.text = hexGridVisible ? "Hide Hex Grid" : "Show Hex Grid";
            }

            // Best-effort: if a Planet exists, toggle its private 'enableWireframe' field used elsewhere in the project
            try
            {
                var planet = UnityEngine.Object.FindAnyObjectByType<HexGlobeProject.HexMap.Planet>();
                if (planet != null)
                {
                    var f = typeof(HexGlobeProject.HexMap.Planet).GetField("enableWireframe", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null)
                    {
                        f.SetValue(planet, hexGridVisible);
                    }
                    else
                    {
                        // try a public property fallback
                        var p = typeof(HexGlobeProject.HexMap.Planet).GetProperty("enableWireframe", BindingFlags.Public | BindingFlags.Instance);
                        if (p != null && p.CanWrite) p.SetValue(planet, hexGridVisible);
                    }
                }
            }
            catch { /* best-effort only */ }

            // Inject into shader(s) so GPU-driven overlays can react. Use a global shader variable and also attempt
            // to update any known terrain material(s) directly as a best-effort.
            try
            {
                Shader.SetGlobalFloat("_OverlayEnabled", hexGridVisible ? 1f : 0f);

                // Best-effort: if there's a PlanetTileVisibilityManager in the scene with an assigned material, set the property
                var mgr = UnityEngine.Object.FindAnyObjectByType<HexGlobeProject.TerrainSystem.LOD.PlanetTileVisibilityManager>();
                if (mgr != null)
                {
                    try
                    {
                        var mf = typeof(HexGlobeProject.TerrainSystem.LOD.PlanetTileVisibilityManager).GetField("terrainMaterial", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (mf != null)
                        {
                            var mat = mf.GetValue(mgr) as Material;
                            if (mat != null && mat.HasProperty("_OverlayEnabled")) mat.SetFloat("_OverlayEnabled", hexGridVisible ? 1f : 0f);
                        }
                    }
                    catch { }
                }
                // Also update existing tile material instances so already-spawned tiles react immediately.
                try
                {
                    var tiles = UnityEngine.Object.FindObjectsByType<HexGlobeProject.TerrainSystem.LOD.PlanetTerrainTile>(FindObjectsSortMode.None);
                    if (tiles != null)
                    {
                        foreach (var t in tiles)
                        {
                            try
                            {
                                var mr = t.meshRenderer ?? t.GetComponent<UnityEngine.MeshRenderer>();
                                if (mr != null)
                                {
                                    var matInst = mr.material; // get/create instance
                                    if (matInst != null && matInst.HasProperty("_OverlayEnabled")) matInst.SetFloat("_OverlayEnabled", hexGridVisible ? 1f : 0f);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            catch { }

            OnToggleHexGrid?.Invoke(hexGridVisible);
            // Apply the overlay flag to shader/materials
            ApplyOverlayState();
        }

        void ApplyOverlayState()
        {
            try
            {
                Shader.SetGlobalFloat("_OverlayEnabled", hexGridVisible ? 1f : 0f);
            }
            catch { }

            // Update button label
            if (toggleHexGridButton != null)
            {
                var txt = toggleHexGridButton.GetComponentInChildren<Text>();
                if (txt != null) txt.text = hexGridVisible ? "Hide Hex Grid" : "Show Hex Grid";
            }

            // Mirror to existing materials (same logic as ToggleHexGrid) so initial state applies to tiles already created
            try
            {
                var mgr = UnityEngine.Object.FindAnyObjectByType<HexGlobeProject.TerrainSystem.LOD.PlanetTileVisibilityManager>();
                if (mgr != null)
                {
                    try
                    {
                        var mf = typeof(HexGlobeProject.TerrainSystem.LOD.PlanetTileVisibilityManager).GetField("terrainMaterial", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (mf != null)
                        {
                            var mat = mf.GetValue(mgr) as Material;
                            if (mat != null && mat.HasProperty("_OverlayEnabled")) mat.SetFloat("_OverlayEnabled", hexGridVisible ? 1f : 0f);
                        }
                    }
                    catch { }
                }

                var tiles = UnityEngine.Object.FindObjectsByType<HexGlobeProject.TerrainSystem.LOD.PlanetTerrainTile>(FindObjectsSortMode.None);
                if (tiles != null)
                {
                    foreach (var t in tiles)
                    {
                        try
                        {
                            var mr = t.meshRenderer ?? t.GetComponent<UnityEngine.MeshRenderer>();
                            if (mr != null)
                            {
                                var matInst = mr.material; // get/create instance
                                if (matInst != null && matInst.HasProperty("_OverlayEnabled")) matInst.SetFloat("_OverlayEnabled", hexGridVisible ? 1f : 0f);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public void ResetTurnTimer()
        {
            turnTimer = 0f;
        }

        void StartTurnTimer()
        {
            if (timerCoroutine == null && !isPaused)
            {
                timerCoroutine = StartCoroutine(TurnTimerCoroutine());
            }
        }

        void StopTurnTimer()
        {
            if (timerCoroutine != null)
            {
                StopCoroutine(timerCoroutine);
                timerCoroutine = null;
            }
        }

        IEnumerator TurnTimerCoroutine()
        {
            while (true)
            {
                yield return null;
                if (!isPaused) turnTimer += Time.deltaTime;
            }
        }

        string FormatTime(float seconds)
        {
            var ts = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            if (ts.Hours > 0)
                return string.Format("{0:D2}:{1:D2}:{2:D2}", ts.Hours, ts.Minutes, ts.Seconds);
            return string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
        }

        void SetupRuntimeUI()
        {
            // Ensure EventSystem exists
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(es);
            }

            // Create Canvas
            var canvasGO = new GameObject("InGameControlsCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasGO);

            // Panel
            var panelGO = new GameObject("InGameControlsPanel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.01f, 0.01f);
            panelRect.anchorMax = new Vector2(0.3f, 0.2f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var image = panelGO.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.5f);

            var builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtinFont == null) builtinFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

            // Advance Turn Button
            var advGO = new GameObject("AdvanceTurnButton");
            advGO.transform.SetParent(panelGO.transform, false);
            var advRect = advGO.AddComponent<RectTransform>();
            advRect.anchorMin = new Vector2(0.05f, 0.55f);
            advRect.anchorMax = new Vector2(0.45f, 0.95f);
            advRect.sizeDelta = new Vector2(0, 0);
            var advImg = advGO.AddComponent<Image>();
            advImg.color = new Color(0.2f, 0.6f, 0.2f, 1f);
            var advBtn = advGO.AddComponent<Button>();
            var advTextGO = new GameObject("Text");
            advTextGO.transform.SetParent(advGO.transform, false);
            var advText = advTextGO.AddComponent<Text>();
            advText.text = "Advance Turn";
            advText.font = builtinFont;
            advText.alignment = TextAnchor.MiddleCenter;
            advText.color = Color.white;

            // Pause/Resume Button
            var pauseGO = new GameObject("PauseResumeButton");
            pauseGO.transform.SetParent(panelGO.transform, false);
            var pauseRect = pauseGO.AddComponent<RectTransform>();
            pauseRect.anchorMin = new Vector2(0.55f, 0.55f);
            pauseRect.anchorMax = new Vector2(0.95f, 0.95f);
            pauseRect.sizeDelta = new Vector2(0, 0);
            var pauseImg = pauseGO.AddComponent<Image>();
            pauseImg.color = new Color(0.6f, 0.6f, 0.2f, 1f);
            var pauseBtn = pauseGO.AddComponent<Button>();
            var pauseTextGO = new GameObject("Text");
            pauseTextGO.transform.SetParent(pauseGO.transform, false);
            var pauseText = pauseTextGO.AddComponent<Text>();
            pauseText.text = "Pause";
            pauseText.font = builtinFont;
            pauseText.alignment = TextAnchor.MiddleCenter;
            pauseText.color = Color.white;

            // Turn Timer Text
            var timerGO = new GameObject("TurnTimerText");
            timerGO.transform.SetParent(panelGO.transform, false);
            var timerRect = timerGO.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.05f, 0.05f);
            timerRect.anchorMax = new Vector2(0.95f, 0.45f);
            timerRect.sizeDelta = new Vector2(0, 0);
            var timerText = timerGO.AddComponent<Text>();
            timerText.text = "00:00";
            timerText.font = builtinFont;
            timerText.fontSize = 20;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.color = Color.white;

            // Assign to fields
            controlsPanel = panelGO;
            advanceTurnButton = advBtn;
            pauseResumeButton = pauseBtn;
            turnTimerText = timerText;

            // Toggle Hex Grid Button (placed under the pause button)
            var toggleGO = new GameObject("ToggleHexGridButton");
            toggleGO.transform.SetParent(panelGO.transform, false);
            var toggleRect = toggleGO.AddComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0.55f, 0.05f);
            toggleRect.anchorMax = new Vector2(0.95f, 0.45f);
            toggleRect.sizeDelta = new Vector2(0, 0);
            var toggleImg = toggleGO.AddComponent<Image>();
            toggleImg.color = new Color(0.5f, 0.4f, 0.6f, 1f);
            var toggleBtn = toggleGO.AddComponent<Button>();
            var toggleTextGO = new GameObject("Text");
            toggleTextGO.transform.SetParent(toggleGO.transform, false);
            var toggleText = toggleTextGO.AddComponent<Text>();
            toggleText.text = hexGridVisible ? "Hide Hex Grid" : "Show Hex Grid";
            toggleText.font = builtinFont;
            toggleText.alignment = TextAnchor.MiddleCenter;
            toggleText.color = Color.white;

            toggleHexGridButton = toggleBtn;

            // Ensure button text components are children named "Text" so GetComponentInChildren<Text>() finds them
        }
    }
}
