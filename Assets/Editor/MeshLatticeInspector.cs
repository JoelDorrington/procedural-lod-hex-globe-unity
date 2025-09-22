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

            _res = Mathf.FloorToInt(Mathf.Sqrt(2 * _mesh.vertexCount));

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Render matrix as [j,i] rows in a compact, read-only table (no buttons)
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

                        // Build a compact label with index, lattice coords, local and optional world coordinates.
                        string label = $"#{idx} ({j},{i})\nL: {local.x:F3},{local.y:F3},{local.z:F3}";

                        // Render as a non-interactive box to form a table-like grid.
                        GUILayout.Box(label, GUILayout.Width(180), GUILayout.Height(44));
                    }
                    else
                    {
                        GUILayout.Box($"[{j},{i}]\n- - -", GUILayout.Width(180), GUILayout.Height(44));
                    }
                    idx++;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Note:", "Vertex entries are read-only. Use the Scene view to inspect vertices visually.");
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