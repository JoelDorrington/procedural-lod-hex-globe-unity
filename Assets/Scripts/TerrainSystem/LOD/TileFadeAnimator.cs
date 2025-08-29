using UnityEngine;
using System.Collections;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class TileFadeAnimator : MonoBehaviour
    {

        public Coroutine FadeOut(GameObject tile, float duration)
        {
            return StartCoroutine(FadeOutCoroutine(tile, duration));
        }

        private IEnumerator FadeOutCoroutine(GameObject tile, float duration)
        {
            var renderer = tile.gameObject.GetComponent<Renderer>();
            if (renderer == null) yield break;
            var meshRenderers = tile.gameObject.GetComponentsInChildren<MeshRenderer>();
            Debug.Log($"[FadeOut] Tile {tile.gameObject.name} has {meshRenderers.Length} MeshRenderers");
                foreach (var mr in meshRenderers)
                {
                    // Force unique material instance for each renderer
                    mr.material = new Material(mr.material);
                    Debug.Log($"[FadeOut] Renderer {mr.GetInstanceID()} using material instance {mr.material.GetInstanceID()}");
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
                        Debug.Log($"[FadeOut] Tile {tile.gameObject.name} Renderer {mr.GetInstanceID()} t={t:F2} fade={fadeProgress:F2} alpha={alpha:F2} matID={material.GetInstanceID()}");
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
                    Debug.Log($"[FadeOut] Tile {tile.gameObject.name} Renderer {mr.GetInstanceID()} DONE fade={endFade:F2} alpha={endAlpha:F2} matID={material.GetInstanceID()}");
                }
                Debug.Log($"[FadeOut] Tile {tile.gameObject.name} ALL RENDERERS DONE");
        }



        public Coroutine FadeIn(GameObject tile, float duration)
        {
            return StartCoroutine(FadeInCoroutine(tile, duration));
        }

        private IEnumerator FadeInCoroutine(GameObject tile, float duration)
        {
            var renderer = tile.gameObject.GetComponent<Renderer>();
            if (renderer == null) yield break;
            var meshRenderers = tile.gameObject.GetComponentsInChildren<MeshRenderer>();
            Debug.Log($"[FadeIn] Tile {tile.gameObject.name} has {meshRenderers.Length} MeshRenderers");
                foreach (var mr in meshRenderers)
                {
                    mr.material = new Material(mr.material);
                    Debug.Log($"[FadeIn] Renderer {mr.GetInstanceID()} using material instance {mr.material.GetInstanceID()}");
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
                        Debug.Log($"[FadeIn] Tile {tile.gameObject.name} Renderer {mr.GetInstanceID()} t={t:F2} fade={fadeProgress:F2} alpha={alpha:F2} matID={material.GetInstanceID()}");
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
                    Debug.Log($"[FadeIn] Tile {tile.gameObject.name} Renderer {mr.GetInstanceID()} DONE fade={endFade:F2} alpha={endAlpha:F2} matID={material.GetInstanceID()}");
                }
                Debug.Log($"[FadeIn] Tile {tile.gameObject.name} ALL RENDERERS DONE");
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
