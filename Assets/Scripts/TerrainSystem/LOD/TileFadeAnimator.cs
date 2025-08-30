using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class CoroutineLimiter
    {
        private readonly int maxConcurrent;
        private readonly Queue<IEnumerator> queue = new Queue<IEnumerator>();
        private int runningCount = 0;

        public CoroutineLimiter(int maxConcurrent)
        {
            this.maxConcurrent = maxConcurrent;
        }

        public void Enqueue(MonoBehaviour owner, IEnumerator coroutine)
        {
            queue.Enqueue(coroutine);
            TryStartNext(owner);
        }

        private void TryStartNext(MonoBehaviour owner)
        {
            while (runningCount < maxConcurrent && queue.Count > 0)
            {
                var co = queue.Dequeue();
                owner.StartCoroutine(RunCoroutine(co, owner));
                runningCount++;
            }
        }

        private IEnumerator RunCoroutine(IEnumerator coroutine, MonoBehaviour owner)
        {
            yield return owner.StartCoroutine(coroutine);
            runningCount--;
            TryStartNext(owner);
        }
    }

    public class TileFadeAnimator : MonoBehaviour
    {

        private CoroutineLimiter fadeLimiter;

        private void Awake()
        {
            fadeLimiter = new CoroutineLimiter(10); // Limit concurrency to 10
        }

        public Coroutine FadeOut(GameObject tile, float duration)
        {
            var meshRenderers = tile.GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in meshRenderers)
            {
                mr.material = new Material(mr.material);
                mr.material.SetFloat("_FadeDirection", -1f); // Parent tile fades out
            }
            fadeLimiter.Enqueue(this, FadeOutCoroutine(tile, duration, null));
            return null;
        }

        // Overload: FadeOut with child tiles to temporarily disable
        public Coroutine FadeOut(GameObject tile, float duration, GameObject[] childTilesToDisable)
        {
            var meshRenderers = tile.GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in meshRenderers)
            {
                mr.material = new Material(mr.material);
                mr.material.SetFloat("_FadeDirection", -1f); // Parent tile fades out
            }
            if (childTilesToDisable != null)
            {
                foreach (var child in childTilesToDisable)
                {
                    if (child != null && child.activeSelf)
                    {
                        child.SetActive(false);
                    }
                }
            }
            fadeLimiter.Enqueue(this, FadeOutCoroutine(tile, duration, childTilesToDisable));
            return null;
        }

        private IEnumerator FadeOutCoroutine(GameObject tile, float duration)
        {
            yield return FadeOutCoroutine(tile, duration, null);

        }

        // Overload: FadeOutCoroutine with child tiles to re-enable after fade
        private IEnumerator FadeOutCoroutine(GameObject tile, float duration, GameObject[] childTilesToReenable)
        {
            var renderer = tile.gameObject.GetComponent<Renderer>();
            if (renderer == null) yield break;
            var meshRenderers = tile.gameObject.GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in meshRenderers)
            {
                // Force unique material instance for each renderer
                mr.material = new Material(mr.material);
                SetMaterialTransparent(mr.material);
            }
            float startAlpha = 1f;
            float endAlpha = 0f;
            float startFade = 1f;
            float endFade = 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float fadeProgress = Mathf.Lerp(startFade, endFade, t);
                float alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                foreach (var mr in meshRenderers)
                {
                    var material = mr.material;
                    material.SetFloat("_FadeProgress", fadeProgress);
                    Color color = material.color;
                    color.a = alpha;
                    material.color = color;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            foreach (var mr in meshRenderers)
            {
                var material = mr.material;
                material.SetFloat("_FadeProgress", endFade);
                Color finalColor = material.color;
                finalColor.a = endAlpha;
                material.color = finalColor;
            }
            // Re-enable child tiles after fade
            if (childTilesToReenable != null)
            {
                foreach (var child in childTilesToReenable)
                {
                    if (child != null && !child.activeSelf)
                    {
                        child.SetActive(true);
                    }
                }
            }
        }



        public Coroutine FadeIn(GameObject tile, float duration)
        {
            var meshRenderers = tile.GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in meshRenderers)
            {
                mr.material = new Material(mr.material);
                mr.material.SetFloat("_FadeDirection", 1f); // Child tile fades in
            }
            fadeLimiter.Enqueue(this, FadeInCoroutine(tile, duration));
            return null;
        }

        private IEnumerator FadeInCoroutine(GameObject tile, float duration)
        {
            var renderer = tile.gameObject.GetComponent<Renderer>();
            if (renderer == null) yield break;
            var meshRenderers = tile.gameObject.GetComponentsInChildren<MeshRenderer>();
                foreach (var mr in meshRenderers)
                {
                    mr.material = new Material(mr.material);
                    SetMaterialTransparent(mr.material);
                }
            float startAlpha = 0f;
            float endAlpha = 1f;
            float startFade = 0f;
            float endFade = 1f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float fadeProgress = Mathf.Lerp(startFade, endFade, t);
                float alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                    foreach (var mr in meshRenderers)
                    {
                        var material = mr.material;
                        material.SetFloat("_FadeProgress", fadeProgress);
                        Color color = material.color;
                        color.a = alpha;
                        material.color = color;
                    }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
                foreach (var mr in meshRenderers)
                {
                    var material = mr.material;
                    material.SetFloat("_FadeProgress", endFade);
                    Color finalColor = material.color;
                    finalColor.a = endAlpha;
                    material.color = finalColor;
                    SetMaterialOpaque(material);
                }
        }

        private void SetMaterialTransparent(Material mat)
        {
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 1); // Keep ZWrite enabled for transparent fades
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetFloat("_FadeProgress", 0f);
            // Set child tiles to higher renderQueue (3001), parent tiles to 3000
            if (mat.name.Contains("Child"))
                mat.renderQueue = 3001;
            else
                mat.renderQueue = 3000;
        }

        private void SetMaterialOpaque(Material mat)
        {
            mat.SetFloat("_Mode", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1); // Restore ZWrite for opaque
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
        }
    }
}
