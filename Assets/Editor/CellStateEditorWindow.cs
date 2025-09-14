using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HexGlobeProject.HexMap.Model;

public class CellStateEditorWindow : EditorWindow
{
    private List<MigrationRecord> records = new List<MigrationRecord>();
    private Vector2 scroll;
    private GameModel model;
    private int selectedNodeIndex = -1;
    private int inputTileId = 0;
    private bool useTileId = true;

    [MenuItem("HexGlobe/Cell State Editor")]
    public static void Open()
    {
        var w = GetWindow<CellStateEditorWindow>("Cell State Editor");
        w.minSize = new Vector2(480, 300);
    }

    void OnEnable()
    {
        // seed with one example record if empty
        if (records.Count == 0)
        {
            records.Add(new MigrationRecord { tileId = 100, neighbors = new[] { 101 }, center = Vector3.zero, population = 0f, maxPopulation = 5, allegiance = -1, hasUnit = false, unitId = -1 });
        }
    }

    void OnGUI()
    {
        GUILayout.Label("Migration Records (editable)", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(180));
        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            r.tileId = EditorGUILayout.IntField("TileId", r.tileId);
            if (GUILayout.Button("Remove", GUILayout.Width(60))) { records.RemoveAt(i); break; }
            EditorGUILayout.EndHorizontal();
            // neighbors as comma-separated
            string neigh = string.Join(",", r.neighbors ?? new int[0]);
            neigh = EditorGUILayout.TextField("Neighbors (csv)", neigh);
            var parts = neigh.Split(new char[]{','}, System.StringSplitOptions.RemoveEmptyEntries);
            var list = new List<int>();
            foreach (var p in parts) if (int.TryParse(p.Trim(), out int v)) list.Add(v);
            r.neighbors = list.ToArray();
            r.center = EditorGUILayout.Vector3Field("Center", r.center);
            r.population = EditorGUILayout.FloatField("Population", r.population);
            r.maxPopulation = EditorGUILayout.IntField("MaxPopulation", r.maxPopulation);
            r.allegiance = EditorGUILayout.IntField("Allegiance", r.allegiance);
            r.hasUnit = EditorGUILayout.Toggle("HasUnit", r.hasUnit);
            r.unitId = EditorGUILayout.IntField("UnitId", r.unitId);
            records[i] = r;
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Record")) records.Add(new MigrationRecord { tileId = 0, neighbors = new int[0], center = Vector3.zero, population = 0f, maxPopulation = 1, allegiance = -1, hasUnit = false, unitId = -1 });
        if (GUILayout.Button("Build Model")) BuildModelFromRecords();
        if (GUILayout.Button("Clear Model")) { model = null; selectedNodeIndex = -1; }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        GUILayout.Label("Select Cell", EditorStyles.boldLabel);
        useTileId = EditorGUILayout.Toggle("Select by TileId", useTileId);
        if (useTileId)
        {
            inputTileId = EditorGUILayout.IntField("TileId", inputTileId);
            if (GUILayout.Button("Resolve TileId to Index")) ResolveTileId();
        }
        else
        {
            selectedNodeIndex = EditorGUILayout.IntField("Node Index", selectedNodeIndex);
        }

        EditorGUILayout.Space();
        if (model != null && selectedNodeIndex >= 0 && selectedNodeIndex < model.CellCount)
        {
            GUILayout.Label($"Editing node {selectedNodeIndex}", EditorStyles.boldLabel);
            var pop = EditorGUILayout.FloatField("Population", model.population[selectedNodeIndex]);
            var maxPop = EditorGUILayout.IntField("MaxPopulation", model.maxPopulation[selectedNodeIndex]);
            var alleg = EditorGUILayout.IntField("Allegiance", model.allegiance[selectedNodeIndex]);
            var hasU = EditorGUILayout.Toggle("HasUnit", model.hasUnit[selectedNodeIndex] != 0);
            var uid = EditorGUILayout.IntField("UnitId", model.unitId[selectedNodeIndex]);

            if (GUILayout.Button("Apply to Model"))
            {
                model.population[selectedNodeIndex] = pop;
                model.maxPopulation[selectedNodeIndex] = maxPop;
                model.allegiance[selectedNodeIndex] = alleg;
                model.hasUnit[selectedNodeIndex] = hasU ? (byte)1 : (byte)0;
                model.unitId[selectedNodeIndex] = uid;
                EditorUtility.SetDirty(this);
            }
        }
        else
        {
            GUILayout.Label("No model or invalid index selected.");
        }

        EditorGUILayout.Space();
        GUILayout.Label("Notes:");
        EditorGUILayout.HelpBox("This tool builds a managed GameModel from the records above. It is editor-only and intended for playtest seeding and quick read/write of cell state.", MessageType.Info);
    }

    private void BuildModelFromRecords()
    {
        model = GameModel.BuildFromRecords(records, new SparseMapIndex());
        selectedNodeIndex = -1;
    }

    private void ResolveTileId()
    {
        if (model == null) { EditorUtility.DisplayDialog("No model", "Build the model first.", "OK"); return; }
        if (model == null) return;
        if (model == null) return;
        if (model == null) return;
        if (model == null) return;
        if (!model.CellCount.Equals(0))
        {
            if (model == null) return;
        }

        if (model == null) return;
        // try resolve via topology index
        if (model == null || model.CellCount == 0) { EditorUtility.DisplayDialog("No model", "Build the model first.", "OK"); return; }
        if (!model.CellCount.Equals(0))
        {
            if (model == null) return;
        }

        // TopologyResult is internal to GameModel; we can get it via reflection or rebuild using TopologyBuilder again.
        // For simplicity, rebuild a topology from current records and attempt to resolve tileId via SparseMapIndex.
        var cfg = new TopologyConfig();
        foreach (var r in records) cfg.entries.Add(new TopologyConfig.TileEntry { tileId = r.tileId, neighbors = r.neighbors, center = r.center });
        var topo = TopologyBuilder.Build(cfg, new SparseMapIndex());
        if (topo.index.TryGetIndex(inputTileId, out int idx))
        {
            selectedNodeIndex = idx;
            Repaint();
        }
        else
        {
            EditorUtility.DisplayDialog("Not found", "TileId not found in current records.", "OK");
        }
    }
}
