using System.IO;
using UnityEditor;
using UnityEngine;
using HexGlobeProject.TerrainSystem.Graphics;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;

public class TerrainShaderConfigWindow : EditorWindow
{
    // Consolidated: we tune TerrainConfig directly
    // TerrainConfig (fallback/source-of-truth) editing
    TerrainConfig terrainConfig;
    SerializedObject tcs;

    [MenuItem("Tools/HexGlobe/Terrain Tuning...")]
    public static void OpenWindow()
    {
    var w = GetWindow<TerrainShaderConfigWindow>("Terrain Tuning");
        w.minSize = new Vector2(420, 300);
    }

    void OnEnable()
    {
        RefreshTarget();
    }

    public void RefreshTarget()
    {
        // locate TerrainConfig asset too (prefer well-known path)
        var tpath = "Assets/Configs/TerrainConfig.asset";
        terrainConfig = AssetDatabase.LoadAssetAtPath<TerrainConfig>(tpath);
        if (terrainConfig == null)
        {
            var gu = AssetDatabase.FindAssets("t:TerrainConfig");
            if (gu != null && gu.Length > 0)
            {
                var p = AssetDatabase.GUIDToAssetPath(gu[0]);
                terrainConfig = AssetDatabase.LoadAssetAtPath<TerrainConfig>(p);
            }
        }
        if (terrainConfig != null) tcs = new SerializedObject(terrainConfig);
        else tcs = null;
    }

    void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Terrain Tuning", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Edit TerrainConfig colors and transition heights; apply to materials or update globals for live tuning.", MessageType.Info);

        // TerrainConfig section
        EditorGUILayout.Space();
    EditorGUILayout.LabelField("TerrainConfig", EditorStyles.boldLabel);
    EditorGUILayout.HelpBox("Source of truth for shader parameters.", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Locate TerrainConfig Asset"))
        {
            RefreshTarget();
            if (terrainConfig != null) Selection.activeObject = terrainConfig;
            else EditorUtility.DisplayDialog("Not found", "No TerrainConfig asset found. Create one or place it under Assets/Configs/TerrainConfig.asset.", "OK");
        }
        if (GUILayout.Button("Ping TerrainConfig"))
        {
            if (terrainConfig != null) EditorGUIUtility.PingObject(terrainConfig);
        }
        EditorGUILayout.EndHorizontal();

        if (tcs == null)
        {
            EditorGUILayout.HelpBox("No TerrainConfig available to edit.", MessageType.Warning);
        }
        else
        {
            tcs.Update();
            // Tiered colors and absolute heights above sea level
            EditorGUILayout.PropertyField(tcs.FindProperty("waterColor"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Coastline", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(tcs.FindProperty("coastMax"), new GUIContent("Max Height (above sea)"));
            EditorGUILayout.PropertyField(tcs.FindProperty("coastColor"), new GUIContent("Color"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Lowlands", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(tcs.FindProperty("lowlandsMax"), new GUIContent("Max Height (above sea)"));
            EditorGUILayout.PropertyField(tcs.FindProperty("lowlandsColor"), new GUIContent("Color"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Highlands", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(tcs.FindProperty("highlandsMax"), new GUIContent("Max Height (above sea)"));
            EditorGUILayout.PropertyField(tcs.FindProperty("highlandsColor"), new GUIContent("Color"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mountains", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(tcs.FindProperty("mountainsMax"), new GUIContent("Max Height (above sea)"));
            EditorGUILayout.PropertyField(tcs.FindProperty("mountainsColor"), new GUIContent("Color"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Snowcaps", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(tcs.FindProperty("snowcapsMax"), new GUIContent("Max Height (above sea)"));
            EditorGUILayout.PropertyField(tcs.FindProperty("snowcapsColor"), new GUIContent("Color"));

            tcs.ApplyModifiedProperties();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save TerrainConfig"))
            {
                EditorUtility.SetDirty(terrainConfig);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            if (GUILayout.Button("Apply TerrainConfig to Land.mat"))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Land.mat");
                if (mat == null) EditorUtility.DisplayDialog("Material not found", "Assets/Materials/Land.mat not found.", "OK");
                else
                {
                    TerrainShaderGlobals.Apply(terrainConfig, mat);
                    EditorUtility.SetDirty(mat);
                    AssetDatabase.SaveAssets();
                    // Propagate to all active tiles
                    var mgr = Object.FindAnyObjectByType<HexGlobeProject.TerrainSystem.LOD.PlanetTileVisibilityManager>();
                    if (mgr != null) mgr.ApplyShaderConfigToAllTiles(terrainConfig);
                    EditorUtility.DisplayDialog("Applied", "TerrainConfig applied to Land.mat", "OK");
                }
            }
            if (GUILayout.Button("Apply to Selected Material"))
            {
                var o = Selection.activeObject as Material;
                if (o == null) EditorUtility.DisplayDialog("No material selected", "Select a Material in the Project window and retry.", "OK");
                else
                {
                    TerrainShaderGlobals.Apply(terrainConfig, o);
                    EditorUtility.SetDirty(o);
                    AssetDatabase.SaveAssets();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

            // Shader fields editor for the selected material (or show TerrainConfig values when no material selected)
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shader Fields (selected material)", EditorStyles.boldLabel);
            Material selMat = Selection.activeObject as Material;
            if (selMat == null)
            {
                EditorGUILayout.HelpBox("No Material selected. Select a Material in the Project window to edit shader fields directly. Falling back to TerrainConfig values if available.", MessageType.Info);
            }

            // Helper local holders (tiered)
            Color matWaterColor = Color.black;
            Color matCoastColor = Color.black;
            Color matLowlandsColor = Color.black;
            Color matHighlandsColor = Color.black;
            Color matMountainsColor = Color.black;
            Color matSnowcapsColor = Color.white;
            float matPlanetRadius = 30f;
            float matSeaLevel = 0f;
            float matCoastMax = 1f;
            float matLowlandsMax = 3f;
            float matHighlandsMax = 5f;
            float matMountainsMax = 8f;
            float matSnowcapsMax = 10f;

            // If a material is selected read its current properties
            if (selMat != null)
            {
                if (selMat.HasProperty("_WaterColor")) matWaterColor = selMat.GetColor("_WaterColor");
                if (selMat.HasProperty("_CoastColor")) matCoastColor = selMat.GetColor("_CoastColor");
                if (selMat.HasProperty("_LowlandsColor")) matLowlandsColor = selMat.GetColor("_LowlandsColor");
                if (selMat.HasProperty("_HighlandsColor")) matHighlandsColor = selMat.GetColor("_HighlandsColor");
                if (selMat.HasProperty("_MountainsColor")) matMountainsColor = selMat.GetColor("_MountainsColor");
                if (selMat.HasProperty("_SnowcapsColor")) matSnowcapsColor = selMat.GetColor("_SnowcapsColor");

                if (selMat.HasProperty("_PlanetRadius")) matPlanetRadius = selMat.GetFloat("_PlanetRadius");
                if (selMat.HasProperty("_SeaLevel")) matSeaLevel = selMat.GetFloat("_SeaLevel");
                if (selMat.HasProperty("_CoastMax")) matCoastMax = selMat.GetFloat("_CoastMax");
                if (selMat.HasProperty("_LowlandsMax")) matLowlandsMax = selMat.GetFloat("_LowlandsMax");
                if (selMat.HasProperty("_HighlandsMax")) matHighlandsMax = selMat.GetFloat("_HighlandsMax");
                if (selMat.HasProperty("_MountainsMax")) matMountainsMax = selMat.GetFloat("_MountainsMax");
                if (selMat.HasProperty("_SnowcapsMax")) matSnowcapsMax = selMat.GetFloat("_SnowcapsMax");
            }
            else if (terrainConfig != null)
            {
                // fallback to TerrainConfig values
                matWaterColor = terrainConfig.waterColor;
                matCoastColor = terrainConfig.coastColor;
                matLowlandsColor = terrainConfig.lowlandsColor;
                matHighlandsColor = terrainConfig.highlandsColor;
                matMountainsColor = terrainConfig.mountainsColor;
                matSnowcapsColor = terrainConfig.snowcapsColor;
                matPlanetRadius = terrainConfig.baseRadius;
                matSeaLevel = terrainConfig.seaLevel;
                matCoastMax = terrainConfig.coastMax;
                matLowlandsMax = terrainConfig.lowlandsMax;
                matHighlandsMax = terrainConfig.highlandsMax;
                matMountainsMax = terrainConfig.mountainsMax;
                matSnowcapsMax = terrainConfig.snowcapsMax;
            }

            // Draw editable fields
            EditorGUI.BeginChangeCheck();
            matWaterColor = EditorGUILayout.ColorField("Water Color", matWaterColor);
            matCoastColor = EditorGUILayout.ColorField("Coast Color", matCoastColor);
            matLowlandsColor = EditorGUILayout.ColorField("Lowlands Color", matLowlandsColor);
            matHighlandsColor = EditorGUILayout.ColorField("Highlands Color", matHighlandsColor);
            matMountainsColor = EditorGUILayout.ColorField("Mountains Color", matMountainsColor);
            matSnowcapsColor = EditorGUILayout.ColorField("Snowcaps Color", matSnowcapsColor);

            matPlanetRadius = EditorGUILayout.FloatField("Planet Radius", matPlanetRadius);
            matSeaLevel = EditorGUILayout.FloatField("Sea Level", matSeaLevel);
            matCoastMax = EditorGUILayout.FloatField("Coast Max", matCoastMax);
            matLowlandsMax = EditorGUILayout.FloatField("Lowlands Max", matLowlandsMax);
            matHighlandsMax = EditorGUILayout.FloatField("Highlands Max", matHighlandsMax);
            matMountainsMax = EditorGUILayout.FloatField("Mountains Max", matMountainsMax);
            matSnowcapsMax = EditorGUILayout.FloatField("Snowcaps Max", matSnowcapsMax);

            EditorGUILayout.Space();
            // no separate mountain/snow sliders in simplified tier system

            if (EditorGUI.EndChangeCheck())
            {
                // no immediate action here; wait for Apply buttons
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Shader Fields to Selected Material") && selMat != null)
            {
                // clamp some values before applying
                matPlanetRadius = Mathf.Max(0.0001f, matPlanetRadius);
                // clamp some values
                matPlanetRadius = Mathf.Max(0.0001f, matPlanetRadius);
                matCoastMax = Mathf.Max(0f, matCoastMax);
                matLowlandsMax = Mathf.Max(matCoastMax, matLowlandsMax);
                matHighlandsMax = Mathf.Max(matLowlandsMax, matHighlandsMax);
                matMountainsMax = Mathf.Max(matHighlandsMax, matMountainsMax);
                matSnowcapsMax = Mathf.Max(matMountainsMax, matSnowcapsMax);

                if (selMat.HasProperty("_WaterColor")) selMat.SetColor("_WaterColor", matWaterColor);
                if (selMat.HasProperty("_CoastColor")) selMat.SetColor("_CoastColor", matCoastColor);
                if (selMat.HasProperty("_LowlandsColor")) selMat.SetColor("_LowlandsColor", matLowlandsColor);
                if (selMat.HasProperty("_HighlandsColor")) selMat.SetColor("_HighlandsColor", matHighlandsColor);
                if (selMat.HasProperty("_MountainsColor")) selMat.SetColor("_MountainsColor", matMountainsColor);
                if (selMat.HasProperty("_SnowcapsColor")) selMat.SetColor("_SnowcapsColor", matSnowcapsColor);

                if (selMat.HasProperty("_PlanetRadius")) selMat.SetFloat("_PlanetRadius", matPlanetRadius);
                if (selMat.HasProperty("_SeaLevel")) selMat.SetFloat("_SeaLevel", matSeaLevel);
                if (selMat.HasProperty("_CoastMax")) selMat.SetFloat("_CoastMax", matCoastMax);
                if (selMat.HasProperty("_LowlandsMax")) selMat.SetFloat("_LowlandsMax", matLowlandsMax);
                if (selMat.HasProperty("_HighlandsMax")) selMat.SetFloat("_HighlandsMax", matHighlandsMax);
                if (selMat.HasProperty("_MountainsMax")) selMat.SetFloat("_MountainsMax", matMountainsMax);
                if (selMat.HasProperty("_SnowcapsMax")) selMat.SetFloat("_SnowcapsMax", matSnowcapsMax);

                EditorUtility.SetDirty(selMat);
                AssetDatabase.SaveAssets();
                // Propagate to all active tiles
                var mgr = Object.FindAnyObjectByType<HexGlobeProject.TerrainSystem.LOD.PlanetTileVisibilityManager>();
                if (mgr != null) mgr.ApplyShaderConfigToAllTiles(terrainConfig);
            }

            if (GUILayout.Button("Apply Shader Fields to Land.mat"))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Land.mat");
                if (mat == null) EditorUtility.DisplayDialog("Material not found", "Assets/Materials/Land.mat not found.", "OK");
                else
                {
                    // clamp to monotonic tier heights
                    matPlanetRadius = Mathf.Max(0.0001f, matPlanetRadius);
                    matCoastMax = Mathf.Max(0f, matCoastMax);
                    matLowlandsMax = Mathf.Max(matCoastMax, matLowlandsMax);
                    matHighlandsMax = Mathf.Max(matLowlandsMax, matHighlandsMax);
                    matMountainsMax = Mathf.Max(matHighlandsMax, matMountainsMax);
                    matSnowcapsMax = Mathf.Max(matMountainsMax, matSnowcapsMax);

                    if (mat.HasProperty("_WaterColor")) mat.SetColor("_WaterColor", matWaterColor);
                    if (mat.HasProperty("_CoastColor")) mat.SetColor("_CoastColor", matCoastColor);
                    if (mat.HasProperty("_LowlandsColor")) mat.SetColor("_LowlandsColor", matLowlandsColor);
                    if (mat.HasProperty("_HighlandsColor")) mat.SetColor("_HighlandsColor", matHighlandsColor);
                    if (mat.HasProperty("_MountainsColor")) mat.SetColor("_MountainsColor", matMountainsColor);
                    if (mat.HasProperty("_SnowcapsColor")) mat.SetColor("_SnowcapsColor", matSnowcapsColor);

                    if (mat.HasProperty("_PlanetRadius")) mat.SetFloat("_PlanetRadius", matPlanetRadius);
                    if (mat.HasProperty("_SeaLevel")) mat.SetFloat("_SeaLevel", matSeaLevel);
                    if (mat.HasProperty("_CoastMax")) mat.SetFloat("_CoastMax", matCoastMax);
                    if (mat.HasProperty("_LowlandsMax")) mat.SetFloat("_LowlandsMax", matLowlandsMax);
                    if (mat.HasProperty("_HighlandsMax")) mat.SetFloat("_HighlandsMax", matHighlandsMax);
                    if (mat.HasProperty("_MountainsMax")) mat.SetFloat("_MountainsMax", matMountainsMax);
                    if (mat.HasProperty("_SnowcapsMax")) mat.SetFloat("_SnowcapsMax", matSnowcapsMax);

                    EditorUtility.SetDirty(mat);
                    AssetDatabase.SaveAssets();
                    // Propagate to all active tiles
                    var mgr = Object.FindAnyObjectByType<HexGlobeProject.TerrainSystem.LOD.PlanetTileVisibilityManager>();
                    if (mgr != null) mgr.ApplyShaderConfigToAllTiles(terrainConfig);
                }
            }
            // Real-time update button: set shader global properties so tuning updates immediately in the scene
            if (GUILayout.Button("Update Live (Set Global Shader Vars)"))
            {
                // clamp and set globals for immediate preview
                matPlanetRadius = Mathf.Max(0.0001f, matPlanetRadius);
                matCoastMax = Mathf.Max(0f, matCoastMax);
                matLowlandsMax = Mathf.Max(matCoastMax, matLowlandsMax);
                matHighlandsMax = Mathf.Max(matLowlandsMax, matHighlandsMax);
                matMountainsMax = Mathf.Max(matHighlandsMax, matMountainsMax);
                matSnowcapsMax = Mathf.Max(matMountainsMax, matSnowcapsMax);

                Shader.SetGlobalColor("_WaterColor", matWaterColor);
                Shader.SetGlobalColor("_CoastColor", matCoastColor);
                Shader.SetGlobalColor("_LowlandsColor", matLowlandsColor);
                Shader.SetGlobalColor("_HighlandsColor", matHighlandsColor);
                Shader.SetGlobalColor("_MountainsColor", matMountainsColor);
                Shader.SetGlobalColor("_SnowcapsColor", matSnowcapsColor);

                Shader.SetGlobalFloat("_PlanetRadius", matPlanetRadius);
                Shader.SetGlobalFloat("_SeaLevel", matSeaLevel);
                Shader.SetGlobalFloat("_CoastMax", matCoastMax);
                Shader.SetGlobalFloat("_LowlandsMax", matLowlandsMax);
                Shader.SetGlobalFloat("_HighlandsMax", matHighlandsMax);
                Shader.SetGlobalFloat("_MountainsMax", matMountainsMax);
                Shader.SetGlobalFloat("_SnowcapsMax", matSnowcapsMax);

                // Force repaint so scene updates immediately in editor
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        // Removed separate ShaderConfig management; TerrainConfig is the single source now.
    }

    // No separate ShaderConfig asset anymore.
}
