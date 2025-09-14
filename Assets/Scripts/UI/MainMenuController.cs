using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace HexGlobeProject.UI
{
    /// <summary>
    /// Minimal Main Menu controller that starts a bootstrap coroutine and reports progress to a UI loading bar.
    /// This is intentionally small and expects a bootstrapper function to be provided via Inspector or code.
    /// If no UI references are assigned in the inspector this class will create a small runtime UI so the Start
    /// button is visible for quick playtests.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("UI References")] 
        public GameObject menuPanel; // contains Start button
        public GameObject loadingPanel; // contains loading bar
        public Image loadingFill; // fill image with fillAmount 0..1
        public CanvasGroup fadeGroup; // for fading out

        [Header("Bootstrap Settings")]
        // If null, MainMenuController will attempt to find a Bootstrapper in scene by interface IBootstrapper
        public MonoBehaviour bootstrapper; // should implement IBootstrapper

        bool isBootstrapping = false;

        public void StartButton()
        {
            if (isBootstrapping) return;
            // If no bootstrapper assigned, try to find or create a SceneBootstrapper so Start runs a real test flow
            if (bootstrapper == null)
            {
                var existing = GetBootstrapper();
                if (existing == null)
                {
                    var go = this.gameObject;
                    var sb = go.GetComponent<SceneBootstrapper>();
                    if (sb == null)
                    {
                        Debug.Log("No IBootstrapper found — adding SceneBootstrapper to MainMenuController GameObject.");
                        sb = go.AddComponent<SceneBootstrapper>();
                    }
                    bootstrapper = sb as MonoBehaviour;
                }
                else
                {
                    bootstrapper = existing as MonoBehaviour;
                }
            }

            StartCoroutine(StartGameCoroutine());
        }

        public IEnumerator StartGameCoroutine()
        {
            isBootstrapping = true;
            if (menuPanel) menuPanel.SetActive(false);
            if (loadingPanel) loadingPanel.SetActive(true);
            SetLoadingProgress(0f);

            var bs = GetBootstrapper();

            // If no bootstrapper present, treat Start as a noop but show UI progress to completion.
            if (bs == null)
            {
                Debug.Log("No bootstrapper found. Start button performing noop.");
                // Simulate a short progress
                float t = 0f;
                while (t < 0.6f)
                {
                    t += Time.deltaTime;
                    SetLoadingProgress(t / 0.6f * 0.5f);
                    yield return null;
                }

                SetLoadingProgress(0.5f);
                yield return new WaitForSeconds(0.25f);
                SetLoadingProgress(1f);
                yield return StartCoroutine(FadeOutAndEnable(0.4f));
                isBootstrapping = false;
                yield break;
            }

            bool finished = false;
            string error = null;

            // Kick off the bootstrapper; it will call back with progress
            StartCoroutine(bs.RunBootstrapper((p) => {
                SetLoadingProgress(p);
            }, (err) => {
                error = err;
                finished = true;
            }, () => {
                finished = true;
            }));

            // Wait for bootstrap to finish
            while (!finished)
                yield return null;

            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("Bootstrap failed: " + error);
                // Show menu again so player can retry
                if (loadingPanel) loadingPanel.SetActive(false);
                if (menuPanel) menuPanel.SetActive(true);
                isBootstrapping = false;
                yield break;
            }

            SetLoadingProgress(1f);
            yield return StartCoroutine(FadeOutAndEnable(0.6f));

            isBootstrapping = false;
        }

        IBootstrapper GetBootstrapper()
        {
            if (bootstrapper != null && bootstrapper is IBootstrapper) return (IBootstrapper)bootstrapper;
            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in all)
            {
                if (mb is IBootstrapper) return (IBootstrapper)mb;
            }
            return null;
        }

        void SetLoadingProgress(float p)
        {
            if (loadingFill) loadingFill.fillAmount = Mathf.Clamp01(p);
        }

        void Awake()
        {
            // If no menu panel assigned, create a minimal runtime UI so Start is visible in an empty scene.
            if (menuPanel == null || loadingPanel == null || loadingFill == null)
            {
                SetupRuntimeUI();
            }
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
            var canvasGO = new GameObject("MainMenuCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasGO);

            // Ensure at least one Camera exists so Unity doesn't show the "No cameras rendering" overlay
            var anyCam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (anyCam == null || !anyCam.enabled)
            {
                var camGO = new GameObject("MainMenu_Camera");
                var cam = camGO.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Depth;
                cam.cullingMask = 0; // render nothing, only to satisfy the engine
                cam.depth = 1000;
                camGO.AddComponent<AudioListener>();
                DontDestroyOnLoad(camGO);
            }

            // Panel
            var panelGO = new GameObject("MenuPanel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var image = panelGO.AddComponent<Image>();
            image.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(panelGO.transform, false);
            var titleRect = titleGO.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.7f);
            titleRect.anchorMax = new Vector2(0.5f, 0.7f);
            titleRect.sizeDelta = new Vector2(600, 120);
            var titleText = titleGO.AddComponent<Text>();
            titleText.text = "HexGlobe";
            titleText.fontSize = 48;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            var builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtinFont == null)
            {
                // LegacyRuntime.ttf recommended; if it's not available, create a dynamic font from OS
                builtinFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }
            titleText.font = builtinFont;

            // Start Button
            var buttonGO = new GameObject("StartButton");
            buttonGO.transform.SetParent(panelGO.transform, false);
            var btnRect = buttonGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.45f);
            btnRect.anchorMax = new Vector2(0.5f, 0.45f);
            btnRect.sizeDelta = new Vector2(320, 80);
            var btnImage = buttonGO.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.6f, 0.2f, 1f);
            var button = buttonGO.AddComponent<Button>();
            button.onClick.AddListener(() => { Debug.Log("Start button clicked"); StartButton(); });

            var btnTextGO = new GameObject("Text");
            btnTextGO.transform.SetParent(buttonGO.transform, false);
            var btnText = btnTextGO.AddComponent<Text>();
            btnText.text = "Start Play Test";
            btnText.fontSize = 28;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.font = builtinFont;
            var btnTextRect = btnTextGO.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            // Loading Panel
            var loadingGO = new GameObject("LoadingPanel");
            loadingGO.transform.SetParent(canvasGO.transform, false);
            var loadingRect = loadingGO.AddComponent<RectTransform>();
            loadingRect.anchorMin = new Vector2(0.2f, 0.2f);
            loadingRect.anchorMax = new Vector2(0.8f, 0.3f);
            loadingRect.offsetMin = Vector2.zero;
            loadingRect.offsetMax = Vector2.zero;
            var loadingImage = loadingGO.AddComponent<Image>();
            loadingImage.color = new Color(0f, 0f, 0f, 0.0f);

            // Loading fill (simple Image as bar)
            var fillGO = new GameObject("LoadingFill");
            fillGO.transform.SetParent(loadingGO.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0.25f);
            fillRect.anchorMax = new Vector2(0f, 0.75f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.sizeDelta = new Vector2(0, 0);
            var fillImage = fillGO.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.6f, 0.9f, 1f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 0f;

            // Assign to fields
            menuPanel = panelGO;
            loadingPanel = loadingGO;
            loadingFill = fillImage;
            loadingPanel.SetActive(false);

            // Fade group
            var fadeGO = new GameObject("FadeGroup");
            fadeGO.transform.SetParent(canvasGO.transform, false);
            var fg = fadeGO.AddComponent<CanvasGroup>();
            fg.alpha = 1f;
            fadeGroup = fg;
        }

        IEnumerator FadeOutAndEnable(float duration)
        {
            if (fadeGroup == null)
            {
                if (loadingPanel) loadingPanel.SetActive(false);
                yield break;
            }

            float t = 0f;
            float start = fadeGroup.alpha;
            while (t < duration)
            {
                t += Time.deltaTime;
                fadeGroup.alpha = Mathf.Lerp(start, 0f, t / duration);
                yield return null;
            }

            fadeGroup.alpha = 0f;
            if (loadingPanel) loadingPanel.SetActive(false);
            // enable gameplay inputs - we rely on a PlayerController or UnitInputController to enable itself based on scene state.
        }
    }

    public interface IBootstrapper
    {
        // run bootstrapper with onProgress(0..1), onError(string) and onComplete()
        IEnumerator RunBootstrapper(Action<float> onProgress, Action<string> onError, Action onComplete);
    }
}
