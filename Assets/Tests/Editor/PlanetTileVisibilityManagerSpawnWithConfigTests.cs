using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;
using System.Reflection;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileVisibilityManagerSpawnWithConfigTests
    {
        [Test]
        public void SetDepth_WithRuntimeConfig_AllowsImmediateSpawn()
        {
            var mgrGO = new GameObject("mgr_with_config");
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();

            // Create a runtime TerrainConfig and assign it to the private serialized field
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 10f;
            cfg.baseResolution = 8;
            cfg.heightScale = 1f;
            cfg.heightProvider = null; // let builder use fallback

            var cfgField = typeof(PlanetTileVisibilityManager).GetField("config", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(cfgField, "Expected a 'config' field on PlanetTileVisibilityManager");
            cfgField.SetValue(mgr, cfg);

            // Set a planet transform so spawned tiles are parented
            var planet = new GameObject("PlanetRT");
            var ptField = typeof(PlanetTileVisibilityManager).GetField("planetTransform", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(ptField, "Expected a 'planetTransform' private field on manager");
            ptField.SetValue(mgr, planet.transform);

            // Ensure registry cleared first
            var regField = typeof(PlanetTileVisibilityManager).GetField("s_precomputedRegistry", BindingFlags.NonPublic | BindingFlags.Static);
            var registry = (System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<PlanetTileVisibilityManager.PrecomputedTileEntry>>)regField.GetValue(null);
            if (registry == null) { registry = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<PlanetTileVisibilityManager.PrecomputedTileEntry>>(); regField.SetValue(null, registry); }
            registry.Clear();

            // Act
            mgr.SetDepth(0);

            var id = new TileId(0, 0, 0, 0);
            var spawned = mgr.TrySpawnTile(id, resolution: 8);

            Assert.IsNotNull(spawned, "Manager should spawn a tile when a valid runtime TerrainConfig is assigned and SetDepth is called");

            Object.DestroyImmediate(mgrGO);
            Object.DestroyImmediate(planet);
            ScriptableObject.DestroyImmediate(cfg);
        }
    }
}
