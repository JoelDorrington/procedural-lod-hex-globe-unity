using UnityEngine;
using System.Collections;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class TileFadeAnimator : MonoBehaviour
    {

        public Coroutine FadeOut(GameObject tile, float duration)
        {
            return FadeOut(tile, duration, null);

        }

        // Overload: FadeOut with child tiles to temporarily disable
        public Coroutine FadeOut(GameObject tile, float duration, GameObject[] childTilesToDisable)
        {
            Debug.Log($"[FadeOut] Starting fade for tile {tile.name}");
            var meshRenderers = tile.GetComponentsInChildren<MeshRenderer>();
            Debug.Log($"[FadeOut] Tile {tile.name} has {meshRenderers.Length} MeshRenderers (parent/child logic unified)");
            foreach (var mr in meshRenderers)
            {
                mr.material = new Material(mr.material);
                Debug.Log($"[FadeOut] Renderer {mr.GetInstanceID()} using material instance {mr.material.GetInstanceID()} (parent tile)");
            }
            if (childTilesToDisable != null)
            {
                foreach (var child in childTilesToDisable)
                {
                    if (child != null && child.activeSelf)
                    {
                        child.SetActive(false);
                        Debug.Log($"[FadeOut] Disabled child tile {child.name} during parent fade");
                    }
                }
            }
            return StartCoroutine(FadeOutCoroutine(tile, duration, childTilesToDisable));
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
            // Re-enable child tiles after fade
            if (childTilesToReenable != null)
            {
                foreach (var child in childTilesToReenable)
                {
                    if (child != null && !child.activeSelf)
                    {
                        child.SetActive(true);
                        Debug.Log($"[FadeOut] Re-enabled child tile {child.name} after parent fade");
                    }
                }
            }
        }



        public Coroutine FadeIn(GameObject tile, float duration)
        {
            Debug.Log($"[FadeIn] Starting fade for tile {tile.name}");
            var meshRenderers = tile.GetComponentsInChildren<MeshRenderer>();
            Debug.Log($"[FadeIn] Tile {tile.name} has {meshRenderers.Length} MeshRenderers (parent/child logic unified)");
            foreach (var mr in meshRenderers)
            {
                mr.material = new Material(mr.material);
                Debug.Log($"[FadeIn] Renderer {mr.GetInstanceID()} using material instance {mr.material.GetInstanceID()} (parent tile)");
            }
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
            mat.SetInt("_ZWrite", 0); // Ensure ZWrite is off for all fades
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetFloat("_FadeProgress", 0f);
            mat.renderQueue = 3000; // Ensure render queue is always Transparent
            Debug.Log($"[SetMaterialTransparent] {mat.name} ZWrite=0, renderQueue={mat.renderQueue}");
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
            Debug.Log($"[SetMaterialOpaque] {mat.name} ZWrite=1, renderQueue={mat.renderQueue}");
        }
    }
}
