using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.HexMap;
using HexGlobeProject.TerrainSystem;
using System.Reflection;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetOceanVisibilityTests
    {
        [Test]
        public void PlanetHideOcean_TogglesPlanetAndOceanAndTerrainRoot()
        {
            // Create Planet GameObject
            var planetGO = new GameObject("PlanetTest");
            var planet = planetGO.AddComponent<Planet>();
            // Use canonical config value for subdivisions by creating a TerrainRoot that holds the config
            var cfg = ScriptableObject.CreateInstance<HexGlobeProject.TerrainSystem.TerrainConfig>();
            var trGO = new GameObject("TerrainRootTestConfig");
            var tr = trGO.AddComponent<HexGlobeProject.TerrainSystem.TerrainRoot>();
            tr.config = cfg;
            planet.hideOceanRenderer = true;

            // Call GeneratePlanet (public)
            planet.GeneratePlanet();

            var mr = planetGO.GetComponent<MeshRenderer>();
            Assert.IsNotNull(mr, "Planet should have a MeshRenderer after GeneratePlanet");
            Assert.IsFalse(mr.enabled, "Planet MeshRenderer should be disabled when hideOceanRenderer is true");

            // Fallback: create a GameObject named Ocean and ensure ApplyHideOceanToTerrainRoot disables it
            var oceanGO = new GameObject("Ocean");
            var oceanMr = oceanGO.AddComponent<MeshRenderer>();
            oceanMr.enabled = true; // initially visible

            // Use reflection to invoke the private ApplyHideOceanToTerrainRoot method
            var applyMethod = typeof(Planet).GetMethod("ApplyHideOceanToTerrainRoot", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(applyMethod, "ApplyHideOceanToTerrainRoot should exist");

            // Invoke and assert ocean renderer disabled
            applyMethod.Invoke(planet, null);
            Assert.IsFalse(oceanMr.enabled, "Fallback Ocean GameObject's MeshRenderer should be disabled when hideOceanRenderer is true on Planet");

            // TerrainRoot path: create a TerrainRoot and ensure it's updated
            var terrainGO = new GameObject("TerrainRootTest");
            var terrainRoot = terrainGO.AddComponent<TerrainRoot>();
            // Ensure initial state is false
            terrainRoot.hideOceanRenderer = false;
            planet.hideOceanRenderer = true;
            // Invoke apply again - it should find TerrainRoot and set its flag
            applyMethod.Invoke(planet, null);
            Assert.IsTrue(terrainRoot.hideOceanRenderer, "TerrainRoot.hideOceanRenderer should be set when Planet.hideOceanRenderer is true and TerrainRoot exists in scene");

            // Cleanup
            Object.DestroyImmediate(planetGO);
            Object.DestroyImmediate(oceanGO);
            Object.DestroyImmediate(terrainGO);
        }
    }
}
