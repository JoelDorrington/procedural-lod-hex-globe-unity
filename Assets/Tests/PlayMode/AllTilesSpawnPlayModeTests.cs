using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.PlayMode
{
    public class AllTilesSpawnPlayModeTests
    {
        [UnityTest]
        public IEnumerator TrySpawn_AllTiles_AreCreatedAndActive()
        {
            // Arrange
            var managerGo = new GameObject("PTVM_Test");
            var manager = managerGo.AddComponent<PlanetTileVisibilityManager>();

            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 1f;
            cfg.baseResolution = 8;
            manager.config = cfg;

            var planet = new GameObject("Planet");
            // set private serialized field planetTransform
            var ptField = typeof(PlanetTileVisibilityManager).GetField("planetTransform", BindingFlags.NonPublic | BindingFlags.Instance);
            ptField.SetValue(manager, planet.transform);

            // Choose a modest depth to exercise multiple tiles
            int depth = 1;
            manager.SetDepth(depth);

            // Act: attempt to spawn every tile at this depth
            int tilesPerEdge = 1 << depth;
            int expected = 20 * tilesPerEdge * tilesPerEdge;
            var created = new List<GameObject>();
            var missing = new List<TileId>();
            var inactive = new List<TileId>();

            for (int f = 0; f < 20; f++)
            {
                for (int x = 0; x < tilesPerEdge; x++)
                {
                    for (int y = 0; y < tilesPerEdge; y++)
                    {
                        var id = new TileId(f, x, y, depth);
                        var go = manager.TrySpawnTile(id);
                        if (go != null)
                        {
                            created.Add(go);
                            if (!go.activeInHierarchy) inactive.Add(id);
                            // collect a small diagnostic snapshot for the first few tiles
                            if (created.Count <= 6)
                            {
                                var comp = go.GetComponent<PlanetTerrainTile>();
                                if (comp != null)
                                {
                                    try
                                    {
                                        var info = comp.GetDiagnosticInfo(manager.GameCamera != null ? manager.GameCamera.GetComponent<Camera>() : null);
                                        Debug.Log("[TileDiag] " + info.ToString());
                                    }
                                    catch { }
                                }
                            }
                        }
                        else
                        {
                            missing.Add(id);
                        }
                        yield return null; // yield to allow any internal processing
                    }
                }
            }

            // Allow one frame for any late activation
            yield return null;

            // Assert
            if (created.Count != expected)
            {
                string msg = $"Expected {expected} spawned tiles but got {created.Count}. Missing: {missing.Count}. Inactive: {inactive.Count}.\n";
                if (missing.Count > 0)
                {
                    msg += "Missing IDs: ";
                    foreach (var id in missing) msg += id.ToString() + ",";
                    msg += "\n";
                }
                if (inactive.Count > 0)
                {
                    msg += "Inactive IDs: ";
                    foreach (var id in inactive) msg += id.ToString() + ",";
                    msg += "\n";
                }
                Assert.Fail(msg);
            }
            foreach (var g in created)
            {
                Assert.IsTrue(g.activeInHierarchy, "Spawned tile must be active in hierarchy");
            }

            // Cleanup
            Object.Destroy(managerGo);
            Object.Destroy(planet);
            Object.Destroy(cfg);
        }
    }
}
