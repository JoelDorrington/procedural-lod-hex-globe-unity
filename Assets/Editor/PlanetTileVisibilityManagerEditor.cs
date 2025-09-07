#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

[CustomEditor(typeof(PlanetTileVisibilityManager))]
public class PlanetTileVisibilityManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw default inspector except the terrainTileLayer field which we'll draw as a Layer popup
        SerializedProperty prop = serializedObject.GetIterator();
        prop.NextVisible(true); // enter
        while (prop.NextVisible(false))
        {
            if (prop.name == "terrainTileLayer") continue;
            EditorGUILayout.PropertyField(prop, true);
        }

        // terrainTileLayer rendered as layer popup
        SerializedProperty layerProp = serializedObject.FindProperty("terrainTileLayer");
        if (layerProp != null)
        {
            int current = layerProp.intValue;
            int newLayer = EditorGUILayout.LayerField(new GUIContent("Terrain Tile Layer", "Layer assigned to spawned terrain tiles"), current);
            if (newLayer != current)
            {
                layerProp.intValue = newLayer;
            }

            // Auto-detect button
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto-detect \"TerrainTiles\" Layer"))
            {
                int found = LayerMask.NameToLayer("TerrainTiles");
                if (found != -1) layerProp.intValue = found;
                else EditorUtility.DisplayDialog("Layer not found", "No layer named \"TerrainTiles\" was found. Please add it in Tags & Layers.", "OK");
            }
            if (GUILayout.Button("Exclude from Raycasts Now"))
            {
                foreach (var t in targets)
                {
                    PlanetTileVisibilityManager cam = t as PlanetTileVisibilityManager;
                    if (cam == null) continue;
                    // This will only modify the serialized field; runtime Awake() also enforces this at playtime.
                    SerializedObject so = new SerializedObject(cam);
                    var lp = so.FindProperty("terrainTileLayer");
                    if (lp != null)
                    {
                        int layerIdx = lp.intValue;
                        // set raycastLayerMask to exclude this layer
                        SerializedProperty maskProp = so.FindProperty("raycastLayerMask");
                        if (maskProp != null)
                        {
                            // In the serialized object, LayerMask is stored as an int
                            maskProp.intValue = ~(1 << layerIdx);
                            so.ApplyModifiedProperties();
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
