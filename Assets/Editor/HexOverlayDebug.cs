using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem.Graphics;

public static class HexOverlayDebug
{
    [MenuItem("Tools/Hex Overlay/Apply High Visibility to Scene Materials")]
    public static void ApplyHighVisibilityToScene()
    {
        int count = 0;
        // Find all renderers in the scene and apply to their materials when shader name contains 'PlanetTerrain' or material has overlay properties
    var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        // Hex overlay editor debug utilities disabled for playtest.
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
        Debug.Log("Applied all debug presets.");
    }

    [MenuItem("Tools/Hex Overlay/Debug/Toggle Dual Hit Mode")]
    public static void ToggleDualHitMode()
    {
        // Toggle between 0 and 2
        ModifySceneMaterials(mat => {
            if (!mat.HasProperty("_OverlayDebugMode")) return;
            float cur = mat.GetFloat("_OverlayDebugMode");
            float next = cur > 1.5f ? 0f : 2f;
            Undo.RecordObject(mat, "Toggle Overlay Debug Mode");
            mat.SetFloat("_OverlayDebugMode", next);
            EditorUtility.SetDirty(mat);
        });
        Debug.Log("Toggled Overlay debug dual-hit mode on scene materials.");
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
