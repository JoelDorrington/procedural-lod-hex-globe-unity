using UnityEditor;
using UnityEngine;

// Utility to apply recommended import settings for sun/flare textures
public static class FlareImportFixer
{
    [MenuItem("Assets/HexGlobe/Fix Sun Flare Import (Selected)")]
    public static void FixSelected()
    {
        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning($"FlareImportFixer: Selected asset is not a texture: {path}");
                continue;
            }

            Debug.Log($"FlareImportFixer: Fixing import settings for {path}");
            ti.textureType = TextureImporterType.Default; // allow usage in Flare/LensFlare
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = Mathf.Clamp(ti.maxTextureSize, 512, 2048);
            // Keep sRGB (color) so colors render as expected
            ti.sRGBTexture = true;

            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();
        }

        Debug.Log("FlareImportFixer: Done. Review the Flare asset or LensFlare component and set brightness/color as needed.");
    }

    [MenuItem("Assets/HexGlobe/Fix Sun Flare Import (Selected)", true)]
    public static bool FixSelectedValidate()
    {
        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null) return true;
        }
        return false;
    }
}
