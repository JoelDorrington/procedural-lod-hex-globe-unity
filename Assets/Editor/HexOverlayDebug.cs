using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem;

public static class HexOverlayDebug
{
    [MenuItem("Tools/Hex Overlay/Apply High Visibility to Scene Materials")]
    public static void ApplyHighVisibilityToScene()
    {
        int count = 0;
        // Find all renderers in the scene and apply to their materials when shader name contains 'PlanetTerrain' or material has overlay properties
    var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            if (r.sharedMaterials == null) continue;
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                if (mat.shader == null) continue;
                if (mat.shader.name.Contains("PlanetTerrain") || mat.HasProperty("_OverlayEnabled") || mat.HasProperty("_CellSize"))
                {
                    Undo.RecordObject(mat, "Apply High Visibility Overlay");
                    TerrainShaderGlobals.ApplyDebugHighVisibility(mat);
                    EditorUtility.SetDirty(mat);
                    count++;
                }
            }
        }
        Debug.Log($"Applied high-visibility overlay to {count} material(s) in the scene.");
    }

    [MenuItem("Tools/Hex Overlay/Log Scene Material Overlay Properties")]
    public static void LogSceneMaterials()
    {
        HashSet<Material> seen = new HashSet<Material>();
    var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            if (r.sharedMaterials == null) continue;
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                if (seen.Contains(mat)) continue;
                seen.Add(mat);
                if (mat.shader == null) continue;
                if (mat.shader.name.Contains("PlanetTerrain") || mat.HasProperty("_OverlayEnabled") || mat.HasProperty("_CellSize"))
                {
                    TerrainShaderGlobals.LogMaterialOverlayProperties(mat);
                }
            }
        }
        Debug.Log($"Logged overlay properties for {seen.Count} unique material(s) in the scene.");
    }

    [MenuItem("Tools/Hex Overlay/Presets/Thicker Lines")]
    public static void PresetThickerLines()
    {
        ModifySceneMaterials(mat => {
            if (mat.HasProperty("_LineThickness")) mat.SetFloat("_LineThickness", 0.05f);
            if (mat.HasProperty("_OverlayEnabled")) mat.SetFloat("_OverlayEnabled", 1f);
            EditorUtility.SetDirty(mat);
        });
        Debug.Log("Applied preset: Thicker Lines");
    }

    [MenuItem("Tools/Hex Overlay/Presets/Larger Cells")]
    public static void PresetLargerCells()
    {
        ModifySceneMaterials(mat => {
            if (mat.HasProperty("_CellSize")) mat.SetFloat("_CellSize", 5f);
            if (mat.HasProperty("_OverlayEnabled")) mat.SetFloat("_OverlayEnabled", 1f);
            EditorUtility.SetDirty(mat);
        });
        Debug.Log("Applied preset: Larger Cells");
    }

    [MenuItem("Tools/Hex Overlay/Presets/Disable Radial Mask (BaseRadius=0)")]
    public static void PresetDisableRadialMask()
    {
        ModifySceneMaterials(mat => {
            if (mat.HasProperty("_BaseRadius")) mat.SetFloat("_BaseRadius", 0f);
            if (mat.HasProperty("_EdgeExtrusion")) mat.SetFloat("_EdgeExtrusion", 0f);
            if (mat.HasProperty("_OverlayEnabled")) mat.SetFloat("_OverlayEnabled", 1f);
            EditorUtility.SetDirty(mat);
        });
        Debug.Log("Applied preset: Disable Radial Mask");
    }

    [MenuItem("Tools/Hex Overlay/Presets/Set BaseRadius From Renderer Bounds")]
    public static void PresetBaseRadiusFromRenderers()
    {
    var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var r in renderers)
        {
            if (r.sharedMaterials == null) continue;
            float radius = Mathf.Max(r.bounds.extents.x, r.bounds.extents.y, r.bounds.extents.z);
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                if (mat.HasProperty("_BaseRadius"))
                {
                    Undo.RecordObject(mat, "Set BaseRadius from Renderer Bounds");
                    mat.SetFloat("_BaseRadius", radius);
                    EditorUtility.SetDirty(mat);
                    count++;
                }
            }
        }
        Debug.Log($"Applied base radius from renderer bounds to {count} material(s).");
    }

    [MenuItem("Tools/Hex Overlay/Presets/Apply All Debug Presets")]
    public static void PresetApplyAll()
    {
        ApplyHighVisibilityToScene();
        PresetThickerLines();
        PresetLargerCells();
        PresetDisableRadialMask();
        PresetBaseRadiusFromRenderers();
        Debug.Log("Applied all debug presets.");
    }

    static void ModifySceneMaterials(System.Action<Material> change)
    {
    var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            if (r.sharedMaterials == null) continue;
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                if (mat.shader == null) continue;
                if (mat.shader.name.Contains("PlanetTerrain") || mat.HasProperty("_OverlayEnabled") || mat.HasProperty("_CellSize"))
                {
                    Undo.RecordObject(mat, "HexOverlay Preset");
                    change(mat);
                }
            }
        }
    }
}
