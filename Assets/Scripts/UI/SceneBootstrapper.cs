using System;
using System.Collections;
using UnityEngine;
using HexGlobeProject.HexMap.Model;
using HexGlobeProject.HexMap.Runtime;

namespace HexGlobeProject.UI
{
    /// <summary>
    /// Simple non-invasive scene bootstrapper for quick playtests.
    /// It builds a tiny deterministic topology, creates a managed GameModel, and spawns a single unit.
    /// Reports coarse progress to the MainMenuController.
    /// </summary>
    public class SceneBootstrapper : MonoBehaviour, IBootstrapper
    {
        public UnitManager unitManagerPrefab; // optional prefab to instantiate

        public IEnumerator RunBootstrapper(Action<float> onProgress, Action<string> onError, Action onComplete)
        {
            try
            {
                // Stage 1: build a tiny topology (0..0.4)
                onProgress?.Invoke(0.05f);
                var cfg = new TopologyConfig();
                cfg.entries = new System.Collections.Generic.List<TopologyConfig.TileEntry>();

                // Build a tiny 4-node test (a cross)
                cfg.entries.Add(new TopologyConfig.TileEntry { tileId = 100, neighbors = new[] { 101, 102 }, center = Vector3.right });
                cfg.entries.Add(new TopologyConfig.TileEntry { tileId = 101, neighbors = new[] { 100, 103 }, center = Vector3.up });
                cfg.entries.Add(new TopologyConfig.TileEntry { tileId = 102, neighbors = new[] { 100, 103 }, center = Vector3.left });
                cfg.entries.Add(new TopologyConfig.TileEntry { tileId = 103, neighbors = new[] { 101, 102 }, center = Vector3.down });

                var topology = TopologyBuilder.Build(cfg, new SparseMapIndex());
                onProgress?.Invoke(0.35f);

                // Stage 2: create a managed GameModel (0.35..0.7)
                var model = new GameModel();
                model.Initialize(topology);
                onProgress?.Invoke(0.7f);

                // Stage 3: spawn UnitManager and one unit (0.7..1.0)
                UnitManager um = null;
                if (unitManagerPrefab != null)
                {
                    var go = Instantiate(unitManagerPrefab.gameObject);
                    um = go.GetComponent<UnitManager>();
                }
                else
                {
                    var go = new GameObject("UnitManager");
                    um = go.AddComponent<UnitManager>();
                }

                // wire topology and model for playtest
                um.topology = topology;
                um.modelManaged = model;
                um.planetRadius = 10f;
                um.planetTransform = null;

                onProgress?.Invoke(0.85f);

                // spawn a single unit at node 0
                um.unitPrefab = null; // leave null so no prefab is instantiated; if you want visible units, assign a prefab in inspector
                um.SpawnUnitAtNode(0, 1);

                onProgress?.Invoke(1f);
                onComplete?.Invoke();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message + "\n" + ex.StackTrace);
            }

            yield break;
        }
    }
}
