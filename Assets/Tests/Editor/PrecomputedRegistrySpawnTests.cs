using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class PrecomputedRegistrySpawnTests
    {
        [Test]
        public void PrecomputedRegistry_AllEntriesSpawnAndAlign()
        {
            // Arrange: create manager and ensure terrain config is assigned
            var mgrGO = new GameObject("Test_PTVM");
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();

            var cfg = AssetDatabase.LoadAssetAtPath<TerrainConfig>("Assets/TerrainConfig.asset");
            Assert.IsNotNull(cfg, "TerrainConfig.asset must exist for this test");
            // assign private serialized field via reflection
            var cfgField = typeof(PlanetTileVisibilityManager).GetField("config", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            cfgField.SetValue(mgr, cfg);

            // set a stable planet transform
            var planetGO = new GameObject("TestPlanet");
            planetGO.transform.position = Vector3.zero;
            var ptField = typeof(PlanetTileVisibilityManager).GetField("planetTransform", BindingFlags.NonPublic | BindingFlags.Instance);
            ptField.SetValue(mgr, planetGO.transform);

            try
            {
                // We'll test depth 0 and 1 to balance runtime/time
                for (int depth = 0; depth <= 1; depth++)
                {
                    // Force precompute via private method
                    var method = typeof(PlanetTileVisibilityManager).GetMethod("PrecomputeTileNormalsForDepth", BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert.IsNotNull(method, "PrecomputeTileNormalsForDepth must be present");
                    method.Invoke(mgr, new object[] { depth });

                    int tilesPerEdge = 1 << depth;
                    int missingCount = 0;

                    for (int face = 0; face < 20; face++)
                    {
                        for (int x = 0; x < tilesPerEdge; x++)
                        {
                            for (int y = 0; y < tilesPerEdge; y++)
                            {
                                if (!IcosphereMapping.IsValidTileIndex(x, y, depth)) continue;

                                var id = new TileId(face, x, y, depth);

                                // Registry should contain this entry
                                bool found = PlanetTileVisibilityManager.GetPrecomputedIndex(id, out int idx, out var entry);
                                Assert.IsTrue(found, $"Precomputed registry missing entry for {id}");

                                // Try spawning the tile
                                var go = mgr.TrySpawnTile(id, 16);
                                if (go == null)
                                {
                                    missingCount++;
                                    Debug.LogError($"Failed to spawn tile {id}");
                                    continue;
                                }

                                // Verify GameObject positioned at the precomputed center
                                var pos = go.transform.position;
                                Assert.That(Vector3.Distance(pos, entry.centerWorld), Is.LessThan(0.5f), $"Tile {id} spawn position {pos} diverges from precomputed center {entry.centerWorld}");

                                // Verify PlanetTerrainTile and collider assignment
                                var terrainTile = go.GetComponent<PlanetTerrainTile>();
                                Assert.IsNotNull(terrainTile, $"Spawned GO missing PlanetTerrainTile for {id}");
                                Assert.IsNotNull(terrainTile.meshCollider, $"Tile {id} missing MeshCollider");
                                Assert.IsNotNull(terrainTile.meshCollider.sharedMesh, $"Tile {id} MeshCollider.sharedMesh is null");
                            }
                        }
                    }

                    Assert.AreEqual(0, missingCount, $"Some tiles failed to spawn at depth {depth}: {missingCount} missing");
                }
            }
            finally
            {
                // Cleanup
                Object.DestroyImmediate(mgrGO);
                Object.DestroyImmediate(planetGO);
            }
        }
    }
}
