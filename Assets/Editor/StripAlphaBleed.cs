using System.IO;
using UnityEditor;
using UnityEngine;

// Editor utility that strips color from fully transparent pixels to remove alpha-bleed/white halos
public static class StripAlphaBleed
{
    [MenuItem("Assets/HexGlobe/Strip Alpha Bleed From Selected Textures")]
    public static void StripSelected()
    {
        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning($"StripAlphaBleed: Not a texture: {path}");
                continue;
            }

            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning($"StripAlphaBleed: Not a texture importer: {path}");
                continue;
            }

            // Ensure texture is readable for processing
            bool originalReadable = ti.isReadable;
            bool changedReadable = false;
            if (!ti.isReadable)
            {
                ti.isReadable = true;
                ti.SaveAndReimport();
                changedReadable = true;
            }

            // Load the texture data from the asset (reimported ensures readable version)
            var working = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (working == null)
            {
                Debug.LogWarning($"StripAlphaBleed: Failed to load texture after reimport: {path}");
                continue;
            }

            // Duplicate texture pixels and strip RGB on fully transparent pixels
            var pixels = working.GetPixels();
            bool anyChange = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                if (c.a <= 0f && (c.r != 0f || c.g != 0f || c.b != 0f))
                {
                    pixels[i] = new Color(0f, 0f, 0f, 0f);
                    anyChange = true;
                }
            }

            if (anyChange)
            {
                var newTex = new Texture2D(working.width, working.height, TextureFormat.RGBA32, false);
                newTex.SetPixels(pixels);
                newTex.Apply();

                // Encode to PNG and overwrite the asset file
                var png = newTex.EncodeToPNG();
                File.WriteAllBytes(Application.dataPath.Replace("Assets", "") + path, png);
                Debug.Log($"StripAlphaBleed: Wrote cleaned PNG to {path}");

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            else
            {
                Debug.Log($"StripAlphaBleed: No transparent color bleed found in {path}");
            }

            // Restore original readable setting if we changed it
            if (changedReadable)
            {
                ti = AssetImporter.GetAtPath(path) as TextureImporter;
                ti.isReadable = originalReadable;
                ti.SaveAndReimport();
            }
        }

        Debug.Log("StripAlphaBleed: Done.");
    }

    [MenuItem("Assets/HexGlobe/Strip Alpha Bleed From Selected Textures", true)]
    public static bool StripSelectedValidate()
    {
        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".png" || ext == ".tga") return true;
        }
        return false;
    }
}
