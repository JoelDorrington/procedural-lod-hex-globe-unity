using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using HexGlobeProject.Config;

public class PlaytestJsonEditorWindow : EditorWindow
{
    private const string JsonPath = "Assets/Configs/playtest_scene_config.json";
    private PlaytestSceneConfig tempConfig;
    private SerializedObject serializedConfig;
    private Vector2 scroll;

    [MenuItem("Window/HexGlobe/Playtest JSON Editor")]
    public static void ShowWindow()
    {
        var w = GetWindow<PlaytestJsonEditorWindow>("Playtest JSON Editor");
        w.minSize = new Vector2(520, 400);
    }

    private void OnEnable()
    {
        LoadFromJson();
    }

    private void OnDisable()
    {
        // Destroy the temporary ScriptableObject we created to avoid leaking editor objects
        if (tempConfig != null)
        {
            DestroyImmediate(tempConfig);
            tempConfig = null;
        }
        if (serializedConfig != null)
        {
            serializedConfig = null;
        }
    }

    private void LoadFromJson()
    {
        if (!File.Exists(JsonPath))
        {
            tempConfig = CreateInstance<PlaytestSceneConfig>();
            serializedConfig = new SerializedObject(tempConfig);
            return;
        }

        var json = File.ReadAllText(JsonPath);
        // Create a temporary ScriptableObject to hold values
        tempConfig = CreateInstance<PlaytestSceneConfig>();
        JsonUtility.FromJsonOverwrite(json, tempConfig);
        serializedConfig = new SerializedObject(tempConfig);
    }

    private void SaveToJson()
    {
        if (tempConfig == null) return;
        var json = JsonUtility.ToJson(tempConfig, true);
        File.WriteAllText(JsonPath, json);
        AssetDatabase.Refresh();
    }

    private void SaveAsAsset()
    {
        var path = EditorUtility.SaveFilePanelInProject("Save PlaytestSceneConfig", "PlaytestSceneConfig", "asset", "Choose location to save the ScriptableObject");
        if (string.IsNullOrEmpty(path)) return;
        var asset = CreateInstance<PlaytestSceneConfig>();
        // Copy fields via JSON roundtrip to ensure simple copying
        var json = JsonUtility.ToJson(tempConfig);
        JsonUtility.FromJsonOverwrite(json, asset);
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void OnGUI()
    {
        // Defensive guards: SerializedObject targets can be destroyed during domain reloads or asset creation.
        if (serializedConfig == null || tempConfig == null)
        {
            EditorGUILayout.HelpBox("No config loaded.", MessageType.Info);
            if (GUILayout.Button("Load from JSON")) LoadFromJson();
            return;
        }

        try
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            serializedConfig.Update();

            EditorGUILayout.PropertyField(serializedConfig.FindProperty("backgroundColor"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("clearFlags"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Universal StarField (spherical)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("universalRadius"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("universalVoidRadius"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("universalArc"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("universalRateOverTime"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("universalBurstCount"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Galactic StarField (donut)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("galacticDonutRadius"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("galacticRadius"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("galacticArc"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("galacticRateOverTime"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("galacticMaxParticles"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("galacticInnerBiasSkew"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("galacticTorusTiltDegrees"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Directional Light (Sun)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("sunColor"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("sunIntensity"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("sunDrawHalo"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("sunRotationEuler"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("sunPosition"));

            serializedConfig.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload from JSON")) LoadFromJson();
            if (GUILayout.Button("Save to JSON")) SaveToJson();
            if (GUILayout.Button("Save as ScriptableObject")) SaveAsAsset();
            EditorGUILayout.EndHorizontal();
        }
        catch (Exception ex)
        {
            // Defensive recovery: if the serialized object was destroyed, clear references and show message.
            Debug.LogWarning("PlaytestJsonEditorWindow: GUI error, recovering: " + ex.Message);
            if (tempConfig != null)
            {
                DestroyImmediate(tempConfig);
                tempConfig = null;
            }
            serializedConfig = null;
        }
    }
}
