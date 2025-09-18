#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

[InitializeOnLoad]
static class DualOverlayBufferCleanup
{
    static DualOverlayBufferCleanup()
    {
        // Release on assembly reloads
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        // Release on playmode state changes (exit playmode)
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        // Release on editor quitting
        EditorApplication.quitting += OnEditorQuitting;
    }

    static void OnBeforeAssemblyReload()
    {
        HexGlobeProject.HexMap.DualOverlayBuffer.ReleaseAll();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.ExitingEditMode)
        {
            HexGlobeProject.HexMap.DualOverlayBuffer.ReleaseAll();
        }
    }

    static void OnEditorQuitting()
    {
        HexGlobeProject.HexMap.DualOverlayBuffer.ReleaseAll();
    }
}
#endif
