using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(OverlayCubemapGenerator))]
public class OverlayCubemapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        OverlayCubemapGenerator gen = (OverlayCubemapGenerator)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generate & Assign", EditorStyles.boldLabel);
        gen.saveAsset = EditorGUILayout.Toggle("Save Asset", gen.saveAsset);
        gen.assetName = EditorGUILayout.TextField("Asset Name", gen.assetName);

        gen.faceSize = EditorGUILayout.IntField("Face Size", gen.faceSize);
        gen.hexSize = EditorGUILayout.FloatField("Hex Size", gen.hexSize);
        gen.edgeThickness = EditorGUILayout.FloatField("Edge Thickness", gen.edgeThickness);

        EditorGUILayout.Space();
        Material targetMat = (Material)EditorGUILayout.ObjectField("Target Material", null, typeof(Material), true);

        // if (GUILayout.Button("Generate Cubemap"))
        // {
        //     Cubemap c = gen.GenerateCubemap();
        //     if (c != null) EditorUtility.DisplayDialog("Overlay Cubemap", "Cubemap generated at Assets/Resources/" + gen.assetName + ".cubemap.asset", "OK");
        // }

        // if (targetMat != null && GUILayout.Button("Generate & Assign to Material"))
        // {
        //     Cubemap c = gen.GenerateCubemap();
        //     if (c != null)
        //     {
        //         targetMat.SetTexture("_DualOverlayCube", c);
        //         EditorUtility.SetDirty(targetMat);
        //         AssetDatabase.SaveAssets();
        //         EditorUtility.DisplayDialog("Overlay Cubemap", "Generated and assigned to material.", "OK");
        //     }
        // }
    }
}
#endif
