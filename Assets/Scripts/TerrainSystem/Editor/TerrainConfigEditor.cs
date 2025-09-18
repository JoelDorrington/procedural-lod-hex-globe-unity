using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem.Core;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Graphics;

namespace HexGlobeProject.TerrainSystem.Editor
{
    [CustomEditor(typeof(TerrainConfig))]
    public class TerrainConfigEditor : UnityEditor.Editor
    {
        private Type[] _providerTypes;
        private string[] _providerTypeNames;
        private int _selectedIndex = -1;

        private void OnEnable()
        {
            BuildTypeCache();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var config = (TerrainConfig)target;
            DrawCoreSection();
            DrawElevationSection();
            DrawOverlaySection();
            EditorGUILayout.Space();
            DrawHeightProvider(config);

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(config);
                // Push changes live to any TerrainRoot instances so material parameters update immediately
                var roots = FindObjectsByType<TerrainRoot>(FindObjectsSortMode.None);
                foreach (var r in roots)
                {
                    if (r != null && r.terrainMaterial != null)
                    {
                        TerrainShaderGlobals.Apply(config, r.terrainMaterial);
                        EditorUtility.SetDirty(r.terrainMaterial);
                    }
                }
            }
        }

        #region Section Drawing
    private static bool _foldCore = true;

        private void DrawCoreSection()
        {
            _foldCore = EditorGUILayout.Foldout(_foldCore, "Core", true);
            if (!_foldCore) return;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseRadius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseResolution"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("icosphereSubdivisions"));
                EditorGUILayout.HelpBox("Icosphere subdivisions: higher = smaller, more numerous cells. This controls the dual-mesh subdivision level and directly affects the game model cell count and memory/CPU cost.", MessageType.None);
                // maxLod removed
                EditorGUILayout.PropertyField(serializedObject.FindProperty("heightScale"));
                // Realistic height scaling fields removed
            }
            EditorGUILayout.Space();
        }

        private void DrawOverlaySection()
        {
            var propEnabled = serializedObject.FindProperty("overlayEnabled");
            EditorGUILayout.PropertyField(propEnabled);
            if (propEnabled.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("overlayColor"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("overlayOpacity"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("overlayLineThickness"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("overlayEdgeExtrusion"));
                }
            }
        }

        // LOD section removed from editor

    // ElevationSection removed (envelope & peaks feature deprecated)
    private void DrawElevationSection() { }

        // Underwater culling UI removed (fields deprecated)
        #endregion

        private void DrawHeightProvider(TerrainConfig config)
        {
            if (_providerTypes == null) BuildTypeCache();

            TerrainHeightProviderBase current = config.heightProvider;
            if (current != null)
            {
                // find current index
                string fullName = current.GetType().FullName;
                _selectedIndex = Array.FindIndex(_providerTypes, t => t.FullName == fullName);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Height Provider", EditorStyles.boldLabel);

            if (_providerTypes == null || _providerTypes.Length == 0)
            {
                EditorGUILayout.HelpBox("No concrete TerrainHeightProviderBase implementations found.", MessageType.Warning);
                if (GUILayout.Button("Refresh Types")) BuildTypeCache();
                return;
            }

            int newIndex = EditorGUILayout.Popup("Type", Mathf.Max(_selectedIndex, 0), _providerTypeNames);
            if (newIndex != _selectedIndex)
            {
                _selectedIndex = newIndex;
                if (_selectedIndex >= 0 && _selectedIndex < _providerTypes.Length)
                {
                    config.heightProvider = (TerrainHeightProviderBase)Activator.CreateInstance(_providerTypes[_selectedIndex]);
                    EditorUtility.SetDirty(config);
                }
            }

            if (config.heightProvider == null)
            {
                EditorGUILayout.HelpBox("No height provider selected.", MessageType.Info);
            }
            else
            {
                var provider = config.heightProvider;
                // Global randomize seeds button (only shown if any seed-like int fields exist)
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
                var seedFields = provider.GetType().GetFields(flags)
                    .Where(f => f.FieldType == typeof(int) && f.Name.ToLower().Contains("seed"))
                    .ToArray();
                if (seedFields.Length > 0)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("Randomize Seeds", "Assign new random values to all seed fields"), GUILayout.Width(140)))
                        {
                            Undo.RecordObject(config, "Randomize Seeds");
                            foreach (var sf in seedFields)
                            {
                                sf.SetValue(provider, UnityEngine.Random.Range(0, 1_000_000));
                            }
                            EditorUtility.SetDirty(config);
                            Repaint();
                        }
                    }
                }

                using (new EditorGUI.IndentLevelScope())
                {
                    // Draw provider's serializable fields via reflection with per-seed randomize buttons
                    foreach (var field in provider.GetType().GetFields(flags))
                    {
                        object val = field.GetValue(provider);
                        string nicified = ObjectNames.NicifyVariableName(field.Name);
                        bool isSeed = field.FieldType == typeof(int) && field.Name.ToLower().Contains("seed");

                        if (field.FieldType == typeof(float))
                        {
                            EditorGUI.BeginChangeCheck();
                            float f = EditorGUILayout.FloatField(nicified, (float)val);
                            if (EditorGUI.EndChangeCheck()) { field.SetValue(provider, f); EditorUtility.SetDirty(config); }
                        }
                        else if (field.FieldType == typeof(int))
                        {
                            if (isSeed)
                            {
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    EditorGUI.BeginChangeCheck();
                                    int i = EditorGUILayout.IntField(nicified, (int)val);
                                    if (EditorGUI.EndChangeCheck()) { field.SetValue(provider, i); EditorUtility.SetDirty(config); }
                                    if (GUILayout.Button(new GUIContent("🎲", "Randomize this seed"), GUILayout.Width(28)))
                                    {
                                        Undo.RecordObject(config, "Randomize Seed");
                                        field.SetValue(provider, UnityEngine.Random.Range(0, 1_000_000));
                                        EditorUtility.SetDirty(config);
                                        Repaint();
                                    }
                                }
                            }
                            else
                            {
                                EditorGUI.BeginChangeCheck();
                                int i = EditorGUILayout.IntField(nicified, (int)val);
                                if (EditorGUI.EndChangeCheck()) { field.SetValue(provider, i); EditorUtility.SetDirty(config); }
                            }
                        }
                        else if (field.FieldType == typeof(Vector2))
                        {
                            EditorGUI.BeginChangeCheck();
                            Vector2 v2 = EditorGUILayout.Vector2Field(nicified, (Vector2)val);
                            if (EditorGUI.EndChangeCheck()) { field.SetValue(provider, v2); EditorUtility.SetDirty(config); }
                        }
                        else if (field.FieldType == typeof(Vector3))
                        {
                            EditorGUI.BeginChangeCheck();
                            Vector3 v3 = EditorGUILayout.Vector3Field(nicified, (Vector3)val);
                            if (EditorGUI.EndChangeCheck()) { field.SetValue(provider, v3); EditorUtility.SetDirty(config); }
                        }
                        else if (field.FieldType == typeof(bool))
                        {
                            EditorGUI.BeginChangeCheck();
                            bool b = EditorGUILayout.Toggle(nicified, (bool)val);
                            if (EditorGUI.EndChangeCheck()) { field.SetValue(provider, b); EditorUtility.SetDirty(config); }
                        }
                        else
                        {
                            EditorGUILayout.LabelField(nicified, $"Unsupported field type ({field.FieldType.Name})");
                        }
                    }
                }
            }
        }

        private void BuildTypeCache()
        {
            var baseType = typeof(TerrainHeightProviderBase);
            _providerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => {
                    Type[] types = Array.Empty<Type>();
                    try { types = a.GetTypes(); } catch { }
                    return types;
                })
                .Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericType)
                .OrderBy(t => t.Name)
                .ToArray();
            _providerTypeNames = _providerTypes.Select(t => t.Name).ToArray();
        }
    }
}
