using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

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
            DrawLodSection();
            DrawResolutionSection();
            DrawOctaveSection();
            DrawElevationSection();
            DrawOceanSnowSection();
            DrawUnderwaterSection();
            DrawCrossFadeSection();
            DrawDetailSection();
            EditorGUILayout.Space();
            DrawHeightProvider(config);

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(config);
            }
        }

        #region Section Drawing
        private static bool _foldCore = true, _foldLod = true, _foldRes = true, _foldOct = true, _foldElev = false, _foldOcean = false, _foldUnder = false, _foldFade = false, _foldDetail = false;

        private void DrawCoreSection()
        {
            _foldCore = EditorGUILayout.Foldout(_foldCore, "Core", true);
            if (!_foldCore) return;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseRadius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseResolution"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    var maxLodProp = serializedObject.FindProperty("maxLod");
                    EditorGUILayout.PropertyField(maxLodProp, new GUIContent("Max Lod (Deprecated)"));
                    if (GUILayout.Button("?", GUILayout.Width(22)))
                        EditorUtility.DisplayDialog("Deprecated", "'Max Lod' no longer drives generation; use depth fields below.", "OK");
                }
                EditorGUILayout.PropertyField(serializedObject.FindProperty("heightScale"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("realisticHeights"));
                if (serializedObject.FindProperty("realisticHeights").boolValue)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxElevationPercent"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("modalElevationPercent"));
                    }
                }
            }
            EditorGUILayout.Space();
        }

        private void DrawLodSection()
        {
            _foldLod = EditorGUILayout.Foldout(_foldLod, "LOD Depths", true);
            if (!_foldLod) return;
            using (new EditorGUI.IndentLevelScope())
            {
                var low = serializedObject.FindProperty("lowDepth");
                var med = serializedObject.FindProperty("mediumDepth");
                var high = serializedObject.FindProperty("highDepth");
                var ultra = serializedObject.FindProperty("ultraDepth");
                var extreme = serializedObject.FindProperty("extremeMinDepth");
                EditorGUILayout.PropertyField(low);
                EditorGUILayout.PropertyField(med);
                EditorGUILayout.PropertyField(high);
                EditorGUILayout.PropertyField(ultra);
                EditorGUILayout.PropertyField(extreme);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseScreenError"));
                // Order validation
                int l = low.intValue, m = med.intValue, h = high.intValue, u = ultra.intValue, ex = extreme.intValue;
                if (!(l <= m && m <= h && h <= u))
                {
                    EditorGUILayout.HelpBox("Depth order should be Low <= Medium <= High <= Ultra. Fix suggested.", MessageType.Warning);
                    if (GUILayout.Button("Normalize Ordering"))
                    {
                        if (m < l) med.intValue = l;
                        if (h < med.intValue) high.intValue = med.intValue;
                        if (u < high.intValue) ultra.intValue = high.intValue + 1;
                    }
                }
                if (u <= h)
                {
                    EditorGUILayout.HelpBox("Ultra disabled (must be > High to add an extra tier).", MessageType.Info);
                }
                if (ex <= h)
                {
                    EditorGUILayout.HelpBox("Extreme streaming depth should exceed High/Ultra for future streaming.", MessageType.None);
                }
            }
            EditorGUILayout.Space();
        }

        private void DrawResolutionSection()
        {
            _foldRes = EditorGUILayout.Foldout(_foldRes, "Per-Level Resolutions", true);
            if (!_foldRes) return;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lowResolution"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("mediumResolution"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("highResolution"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ultraResolution"));
                EditorGUILayout.HelpBox("0 = derive from baseResolution / 2^depth (with slight boosts on higher levels).", MessageType.None);
            }
            EditorGUILayout.Space();
        }

        private void DrawOctaveSection()
        {
            _foldOct = EditorGUILayout.Foldout(_foldOct, "Octave Masks", true);
            if (!_foldOct) return;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lowMaxOctave"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("mediumMaxOctave"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("highMaxOctave"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ultraMaxOctave"));
                EditorGUILayout.HelpBox("-1 = all octaves. Lower values calm distant tiles.", MessageType.None);
            }
            EditorGUILayout.Space();
        }

        private void DrawElevationSection()
        {
            _foldElev = EditorGUILayout.Foldout(_foldElev, "Elevation Envelope & Peaks", true);
            if (!_foldElev) return;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("useElevationEnvelope"));
                if (serializedObject.FindProperty("useElevationEnvelope").boolValue)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("envelopeScaleOverSea"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("typicalEnvelopeFill"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("envelopeCompressionExponent"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("peakProbability"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("peakExtraHeightPercent"));
                    }
                    EditorGUILayout.HelpBox("Most land compressed into lower band, sparse peaks exceed envelope.", MessageType.None);
                }
            }
            EditorGUILayout.Space();
        }

        private void DrawOceanSnowSection()
        {
            _foldOcean = EditorGUILayout.Foldout(_foldOcean, "Ocean & Snow", true);
            if (!_foldOcean) return;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("generateOcean"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("seaLevel"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("autoSyncSeaLevel"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("shallowWaterBand"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("oceanResolution"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("oceanMaterial"));
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("snowStartOffset"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("snowFullOffset"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("snowSlopeBoost"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("snowColor"));
            }
            EditorGUILayout.Space();
        }

        private void DrawUnderwaterSection()
        {
            _foldUnder = EditorGUILayout.Foldout(_foldUnder, "Underwater Culling", true);
            if (!_foldUnder) return;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cullBelowSea"));
                if (serializedObject.FindProperty("cullBelowSea").boolValue)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("removeFullySubmergedTris"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("seaClampEpsilon"));
                    }
                    EditorGUILayout.HelpBox("Triangle removal leaves coastline holes (good with ocean sphere). Disable to just clamp.", MessageType.Info);
                }
            }
            EditorGUILayout.Space();
        }

        private void DrawCrossFadeSection()
        {
            _foldFade = EditorGUILayout.Foldout(_foldFade, "LOD Cross-Fade", true);
            if (!_foldFade) return;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("enableCrossFade"));
                if (serializedObject.FindProperty("enableCrossFade").boolValue)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("lodFadeDuration"));
                    }
                }
            }
            EditorGUILayout.Space();
        }

        private void DrawDetailSection()
        {
            _foldDetail = EditorGUILayout.Foldout(_foldDetail, "Distance Detail Boost", true);
            if (!_foldDetail) return;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("enableDistanceDetail"));
                if (serializedObject.FindProperty("enableDistanceDetail").boolValue)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("detailActivateDistance"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("detailDeactivateDistance"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("detailResolutionMultiplier"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("detailRebuildCooldown"));
                    }
                }
            }
            EditorGUILayout.Space();
        }
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
