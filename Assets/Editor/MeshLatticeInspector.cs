using UnityEditor;
using UnityEngine;

namespace HexGlobeProject.Editor
{
    public class MeshLatticeInspector : EditorWindow
    {
        private Vector2 _scroll;
        private Mesh _mesh;
        private GameObject _selectedGO;
        private int _highlightIndex = -1;

        // New: lattice display settings
        private int _res = 4;
        private bool _showWorldCoords = true;

        [MenuItem("Tools/Mesh Vertex Inspector")]
        public static void ShowWindow() => GetWindow<MeshLatticeInspector>("Mesh Vertices");

        private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
        private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;
        private void OnSelectionChange() => Repaint();

        private void OnGUI()
        {
            _selectedGO = Selection.activeGameObject;
            _mesh = null;
            if (_selectedGO != null)
            {
                var mf = _selectedGO.GetComponent<MeshFilter>();
                if (mf) _mesh = mf.sharedMesh;
            }

            if (_mesh == null)
            {
                var assetMesh = Selection.activeObject as Mesh;
                if (assetMesh != null) _mesh = assetMesh;
            }

            if (_mesh == null)
            {
                EditorGUILayout.LabelField("Select a GameObject with a MeshFilter or a Mesh asset.");
                return;
            }

            EditorGUILayout.LabelField("Mesh:", _mesh.name);
            EditorGUILayout.LabelField("Vertex count:", _mesh.vertexCount.ToString());
            EditorGUILayout.Space();

            // Lattice controls
            EditorGUILayout.BeginHorizontal();
            _res = EditorGUILayout.IntField("Lattice res (res)", _res, GUILayout.Width(220));
            _showWorldCoords = EditorGUILayout.ToggleLeft("World coords", _showWorldCoords, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            int expected = (_res * (_res + 1)) / 2;
            if (_mesh.vertexCount != expected)
            {
                EditorGUILayout.HelpBox($"Expected lattice vertex count for res={_res} is {expected}. Mesh has {_mesh.vertexCount}. Mapping will still attempt row/col layout.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Render matrix as [j,i] rows
            var verts = _mesh.vertices;
            int idx = 0;
            for (int j = 0; j < _res; j++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"j={j}", GUILayout.Width(40));
                int rowLen = _res - j;
                for (int i = 0; i < rowLen; i++)
                {
                    if (idx < verts.Length)
                    {
                        Vector3 local = verts[idx];
                        Vector3 world = (_selectedGO != null && _showWorldCoords) ? _selectedGO.transform.TransformPoint(local) : local;
                        string btnLabel = $"{j},{i}\n#{idx}";
                        if (_showWorldCoords) btnLabel += $"\n{world.x:F3},{world.y:F3},{world.z:F3}";
                        if (GUILayout.Button(btnLabel, GUILayout.Width(140), GUILayout.Height(36)))
                        {
                            _highlightIndex = idx;
                            SceneView.RepaintAll();
                        }
                    }
                    else
                    {
                        GUILayout.Label($"[{j},{i}]\n- - -", GUILayout.Width(140), GUILayout.Height(36));
                    }
                    idx++;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("Ping Mesh")) EditorGUIUtility.PingObject(_mesh);
            if (GUILayout.Button("Clear Highlight")) { _highlightIndex = -1; SceneView.RepaintAll(); }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_mesh == null) return;
            var verts = _mesh.vertices;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 world = (_selectedGO != null) ? _selectedGO.transform.TransformPoint(verts[i]) : verts[i];
                float size = HandleUtility.GetHandleSize(world) * 0.02f;
                if (i == _highlightIndex)
                {
                    Handles.color = Color.red;
                    Handles.SphereHandleCap(0, world, Quaternion.identity, size * 1.8f, EventType.Repaint);
                    Handles.Label(world, $"[{i}] {world:F3}");
                }
                else
                {
                    Handles.color = Color.yellow;
                    Handles.SphereHandleCap(0, world, Quaternion.identity, size, EventType.Repaint);
                }
            }
        }
    }
}