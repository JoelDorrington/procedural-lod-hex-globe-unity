using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;
using UnityEngine.TestTools;
using System.Collections;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileVisibilityManagerCreatesPrecomputedTilesTests
    {
        [UnityTest]
        public IEnumerator SetDepth_CreatesActivePrecomputedTiles()
        {
            var go = new GameObject("mgr_spawn_check");
            var mgr = go.AddComponent<PlanetTileVisibilityManager>();

            // Provide a runtime config so precomputation has baseRadius and provider
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 10f;
            cfg.baseResolution = 8;
            cfg.heightScale = 1f;
            var cfgField = typeof(PlanetTileVisibilityManager).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            cfgField.SetValue(mgr, cfg);

            // Provide a planet transform so spawned tiles parent correctly
            var planet = new GameObject("PlanetRoot_Check");
            var ptField = typeof(PlanetTileVisibilityManager).GetField("planetTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ptField.SetValue(mgr, planet.transform);

            // Act: request depth precomputation and allow one frame for any initialization
            mgr.SetDepth(0);
            yield return null;

            // Expectation: manager should have created at least one active tile (GetActiveTiles should not be empty)
            var active = mgr.GetActiveTiles();
            Assert.IsNotNull(active, "GetActiveTiles() should return a list, not null");
            Assert.IsNotEmpty(active, "SetDepth should create active precomputed tile GameObjects accessible via GetActiveTiles()");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(planet);
            ScriptableObject.DestroyImmediate(cfg);
        }
    }
}
