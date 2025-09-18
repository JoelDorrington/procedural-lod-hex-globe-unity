using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;

public class OverlayCubemapGeneratorWindow : EditorWindow
{
    // Parameters mirrored from OverlayCubemapGenerator
    int faceSize = 128;
    float lineThickness = 0.05f;
    float baseRadius = 30f;
    bool saveAsset = true;
    string assetName = "DualOverlayCube";
    float hexSize = 0.05f;
    float edgeThickness = 0.007f;
    int arcSampleCount = 16;
    bool rasterizeBoxes = true;
    float boxPadding = 0.05f;
    int subdivisions = 4;
    Material targetMaterial = null;

    [MenuItem("Tools/HexGlobe/Overlay Cubemap Generator")]
    public static void ShowWindow()
    {
        var w = GetWindow<OverlayCubemapGeneratorWindow>("Overlay Cubemap Generator");
        w.minSize = new Vector2(420, 220);
    }

    void OnGUI()
    {
        GUILayout.Label("Cubemap Generation Parameters", EditorStyles.boldLabel);
        faceSize = EditorGUILayout.IntField("Face Size", faceSize);
        subdivisions = EditorGUILayout.IntField("Icosphere Subdivisions", subdivisions);
        baseRadius = EditorGUILayout.FloatField("Base Radius", baseRadius);
        lineThickness = EditorGUILayout.FloatField("Line Thickness", lineThickness);
        hexSize = EditorGUILayout.FloatField("Hex Size", hexSize);
        edgeThickness = EditorGUILayout.FloatField("Edge Thickness", edgeThickness);
        arcSampleCount = EditorGUILayout.IntField("Arc Sample Count", arcSampleCount);
        rasterizeBoxes = EditorGUILayout.Toggle("Rasterize Boxes", rasterizeBoxes);
        boxPadding = EditorGUILayout.Slider("Box Padding", boxPadding, 0f, 0.5f);

        EditorGUILayout.Space();
        GUILayout.Label("Output", EditorStyles.boldLabel);
        saveAsset = EditorGUILayout.Toggle("Save Asset", saveAsset);
        assetName = EditorGUILayout.TextField("Asset Name", assetName);
        targetMaterial = (Material)EditorGUILayout.ObjectField("Target Material", targetMaterial, typeof(Material), true);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate"))
        {
            GenerateAndOptionallyAssign(false);
        }
        if (GUILayout.Button("Generate & Assign"))
        {
            GenerateAndOptionallyAssign(true);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("This utility creates a hidden OverlayCubemapGenerator, calls GenerateCubemap, optionally saves the cubemap asset and assigns it to the chosen material. Ensure your cubemap importer is set to generate mipmaps for glow LOD.", MessageType.Info);
    }

    void GenerateAndOptionallyAssign(bool assign)
    {
        // Create a temporary GameObject to host the generator
        GameObject tmp = new GameObject("__OverlayCubemapGenerator_Temp");
        tmp.hideFlags = HideFlags.HideAndDontSave;
        var gen = tmp.AddComponent<OverlayCubemapGenerator>();

        // copy params
        gen.faceSize = Mathf.Max(4, faceSize);
        gen.lineThickness = Mathf.Max(1e-6f, lineThickness);
        gen.baseRadius = Mathf.Max(1e-6f, baseRadius);
        gen.saveAsset = saveAsset;
        gen.assetName = string.IsNullOrEmpty(assetName) ? "DualOverlayCube" : assetName;
        gen.hexSize = hexSize;
        gen.edgeThickness = edgeThickness;
        gen.arcSampleCount = Mathf.Max(1, arcSampleCount);
        gen.rasterizeBoxes = rasterizeBoxes;
        gen.boxPadding = Mathf.Clamp01(boxPadding);

        try
        {
            Cubemap result = gen.GenerateCubemap(gen.baseRadius, subdivisions, gen.faceSize);
            if (result != null)
            {
                if (saveAsset)
                {
                    string dir = "Assets/Resources";
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, gen.assetName + ".cubemap.asset");
                    AssetDatabase.CreateAsset(result, path);
                    AssetDatabase.SaveAssets();
                    Debug.Log("Saved overlay cubemap to: " + path);
                }
                if (assign && targetMaterial != null)
                {
                    targetMaterial.SetTexture("_DualOverlayCube", result);
                    EditorUtility.SetDirty(targetMaterial);
                    AssetDatabase.SaveAssets();
                    Debug.Log("Assigned generated cubemap to material: " + targetMaterial.name);
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Overlay Cubemap", "Generation failed (null result)", "OK");
            }
        }
        finally
        {
            DestroyImmediate(tmp);
        }
    }
}
#endif
