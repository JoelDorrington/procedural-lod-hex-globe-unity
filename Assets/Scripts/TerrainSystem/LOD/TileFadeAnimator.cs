using UnityEngine;
using System.Collections;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class TileFadeAnimator : MonoBehaviour
    {
        public Coroutine FadeOutAndDestroy(GameObject tile, float duration)
        {
            return StartCoroutine(FadeOutAndDestroyCoroutine(tile, duration));
        }

        public Coroutine FadeOutThenIn(GameObject fromTile, GameObject toTile, float duration)
        {
            return StartCoroutine(FadeOutThenInCoroutine(fromTile, toTile, duration));
        }

        private IEnumerator FadeOutThenInCoroutine(GameObject fromTile, GameObject toTile, float duration)
        {
            yield return FadeOutCoroutine(fromTile, duration * 0.5f);
            if (fromTile != null)
                fromTile.SetActive(false);
            if (toTile != null)
            {
                toTile.SetActive(true);
                yield return FadeInCoroutine(toTile, duration * 0.5f);
            }
        }

        public IEnumerator FadeOutCoroutine(GameObject tile, float duration)
        {
            var renderer = tile.GetComponent<Renderer>();
            if (renderer == null) yield break;
            Material mat = renderer.material;
            SetMaterialTransparent(mat);
            float t = 0f;
            float startTime = Time.unscaledTime;
            while (t < duration)
            {
                t = Time.unscaledTime - startTime;
                float fadeProgress = 1f - Mathf.Clamp01(t / duration);
                if (mat.HasProperty("_FadeProgress")) mat.SetFloat("_FadeProgress", fadeProgress);
                yield return new WaitForEndOfFrame();
            }
            if (mat.HasProperty("_FadeProgress")) mat.SetFloat("_FadeProgress", 0f);
        }

        private IEnumerator FadeOutAndDestroyCoroutine(GameObject tile, float duration)
        {
            var renderer = tile.GetComponent<Renderer>();
            if (renderer == null) yield break;
            Material mat = renderer.material;
            SetMaterialTransparent(mat);
            float t = 0f;
            float startTime = Time.unscaledTime;
            while (t < duration)
            {
                t = Time.unscaledTime - startTime;
                float fadeProgress = 1f - Mathf.Clamp01(t / duration);
                if (mat.HasProperty("_FadeProgress")) mat.SetFloat("_FadeProgress", fadeProgress);
                yield return new WaitForEndOfFrame();
            }
            if (mat.HasProperty("_FadeProgress")) mat.SetFloat("_FadeProgress", 0f);
            if (tile != null && tile)
            {
                tile.SetActive(false);
                if (Application.isPlaying) Destroy(tile); else DestroyImmediate(tile);
            }
        }

        public Coroutine FadeIn(GameObject tile, float duration)
        {
            return StartCoroutine(FadeInCoroutine(tile, duration));
        }

        private IEnumerator FadeInCoroutine(GameObject tile, float duration)
        {
            var renderer = tile.GetComponent<Renderer>();
            if (renderer == null) yield break;
            Material mat = renderer.material;
            SetMaterialTransparent(mat);
            yield return new WaitForEndOfFrame();
            float t = 0f;
            float startTime = Time.unscaledTime;
            while (t < duration)
            {
                t = Time.unscaledTime - startTime;
                float fadeProgress = Mathf.Clamp01(t / duration);
                if (mat.HasProperty("_FadeProgress")) mat.SetFloat("_FadeProgress", fadeProgress);
                yield return new WaitForEndOfFrame();
            }
            if (mat.HasProperty("_FadeProgress")) mat.SetFloat("_FadeProgress", 1f);
            SetMaterialOpaque(mat);
        }

        private void SetMaterialTransparent(Material mat)
        {
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetFloat("_FadeProgress", 0f);
            mat.renderQueue = 3000;
        }

        private void SetMaterialOpaque(Material mat)
        {
            mat.SetFloat("_Mode", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
        }
    }
}
