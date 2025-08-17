#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HexGlobeProject.HexMap;

[CustomEditor(typeof(Planet))]
public class PlanetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var planet = (Planet)target;
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate Planet"))
            {
                planet.GeneratePlanet();
                EditorUtility.SetDirty(planet);
                if (planet.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(planet.gameObject.scene);
            }
            if (GUILayout.Button("Blender Preset"))
            {
                planet.dualSmoothingMode = Planet.DualSmoothingMode.TaubinTangent;
                planet.dualSmoothingIterations = 12;
                planet.smoothLambda = 0.5f;
                planet.smoothMu = -0.53f;
                planet.projectEachSmoothingPass = true;
                planet.dualProjectionBlend = 1f;
                planet.GeneratePlanet();
                EditorUtility.SetDirty(planet);
                if (planet.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(planet.gameObject.scene);
            }
        }
    }
}

public static class PlanetMenu
{
    [MenuItem("HexGlobe/Generate Planet (Selected)")]
    private static void GenerateSelected()
    {
        foreach (var obj in Selection.objects)
        {
            if (obj is GameObject go)
            {
                var planet = go.GetComponent<Planet>();
                if (planet != null)
                {
                    planet.GeneratePlanet();
                    EditorUtility.SetDirty(planet);
                    if (planet.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(planet.gameObject.scene);
                }
            }
        }
    }
}
#endif
