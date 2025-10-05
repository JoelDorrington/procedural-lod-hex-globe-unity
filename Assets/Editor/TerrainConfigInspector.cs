using UnityEditor;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Graphics;

[CustomEditor(typeof(TerrainConfig))]
public class TerrainConfigInspector : Editor
{
    SerializedProperty waterColorProp;
    SerializedProperty coastMaxProp; SerializedProperty coastColorProp;
    SerializedProperty lowlandsMaxProp; SerializedProperty lowlandsColorProp;
    SerializedProperty highlandsMaxProp; SerializedProperty highlandsColorProp;
    SerializedProperty mountainsMaxProp; SerializedProperty mountainsColorProp;
    SerializedProperty snowcapsMaxProp; SerializedProperty snowcapsColorProp;

    bool showShaderTransitions = true;

    void OnEnable()
    {
    waterColorProp = serializedObject.FindProperty("waterColor");
    coastMaxProp = serializedObject.FindProperty("coastMax");
    coastColorProp = serializedObject.FindProperty("coastColor");
    lowlandsMaxProp = serializedObject.FindProperty("lowlandsMax");
    lowlandsColorProp = serializedObject.FindProperty("lowlandsColor");
    highlandsMaxProp = serializedObject.FindProperty("highlandsMax");
    highlandsColorProp = serializedObject.FindProperty("highlandsColor");
    mountainsMaxProp = serializedObject.FindProperty("mountainsMax");
    mountainsColorProp = serializedObject.FindProperty("mountainsColor");
    snowcapsMaxProp = serializedObject.FindProperty("snowcapsMax");
    snowcapsColorProp = serializedObject.FindProperty("snowcapsColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

    // draw the default inspector for existing fields except the shader transition group (we'll render those explicitly below)
    // We can't selectively draw default inspector easily; draw everything first then overlay the shader transition foldout to show/hide its properties.
    DrawDefaultInspector();

        EditorGUILayout.Space();
    EditorGUILayout.LabelField("Shader Tuning (Tiered)", EditorStyles.boldLabel);

        EditorGUILayout.Space();
        showShaderTransitions = EditorGUILayout.Foldout(showShaderTransitions, "Tier Colors & Heights (above sea level)", true);
        if (showShaderTransitions)
        {
            EditorGUILayout.PropertyField(waterColorProp, new GUIContent("Water Color"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(coastMaxProp, new GUIContent("Coastline Max Height"));
            EditorGUILayout.PropertyField(coastColorProp, new GUIContent("Coastline Color"));
            EditorGUILayout.PropertyField(lowlandsMaxProp, new GUIContent("Lowlands Max Height"));
            EditorGUILayout.PropertyField(lowlandsColorProp, new GUIContent("Lowlands Color"));
            EditorGUILayout.PropertyField(highlandsMaxProp, new GUIContent("Highlands Max Height"));
            EditorGUILayout.PropertyField(highlandsColorProp, new GUIContent("Highlands Color"));
            EditorGUILayout.PropertyField(mountainsMaxProp, new GUIContent("Mountains Max Height"));
            EditorGUILayout.PropertyField(mountainsColorProp, new GUIContent("Mountains Color"));
            EditorGUILayout.PropertyField(snowcapsMaxProp, new GUIContent("Snowcaps Max Height"));
            EditorGUILayout.PropertyField(snowcapsColorProp, new GUIContent("Snowcaps Color"));
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Open Terrain Tuning Window"))
        {
            TerrainShaderConfigWindow.OpenWindow();
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}
