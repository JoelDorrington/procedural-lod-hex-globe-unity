using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using HexGlobeProject.TerrainSystem.Core;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetHideOceanTests
    {
        [Test]
        public void ApplyHideOceanToTerrainRoot_SetsTerrainRootFlag()
        {
            // Arrange
            var terrainGO = new GameObject("TerrainRoot_Test");
            var terrainRoot = terrainGO.AddComponent<TerrainRoot>();

            var planetGO = new GameObject("Planet_Test");
            var planet = planetGO.AddComponent<HexMap.Planet>();
            planet.hideOceanRenderer = true;

            // Act: call the private method via reflection
            var method = typeof(HexMap.Planet).GetMethod("ApplyHideOceanToTerrainRoot", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "Reflection: ApplyHideOceanToTerrainRoot should exist");
            method.Invoke(planet, null);

            // Assert
            Assert.IsTrue(terrainRoot.hideOceanRenderer, "TerrainRoot.hideOceanRenderer should be set by Planet.ApplyHideOceanToTerrainRoot");

            // Cleanup
            Object.DestroyImmediate(planetGO);
            Object.DestroyImmediate(terrainGO);
        }

        [Test]
        public void ApplyHideOceanToTerrainRoot_Fallback_TogglesOceanGameObject()
        {
            // Arrange: ensure there is no TerrainRoot
            // (nothing to do if none exists)
            var oceanGO = new GameObject("Ocean");
            var mr = oceanGO.AddComponent<MeshRenderer>();
            mr.enabled = true; // start visible

            var planetGO = new GameObject("Planet_Test2");
            var planet = planetGO.AddComponent<HexGlobeProject.HexMap.Planet>();
            planet.hideOceanRenderer = true;

            // Act
            var method = typeof(HexGlobeProject.HexMap.Planet).GetMethod("ApplyHideOceanToTerrainRoot", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(planet, null);

            // Assert: fallback should disable the ocean MeshRenderer
            var mrAfter = GameObject.Find("Ocean").GetComponent<MeshRenderer>();
            Assert.IsFalse(mrAfter.enabled, "Fallback ocean GameObject MeshRenderer should be disabled when hideOceanRenderer is true");

            // Cleanup
            Object.DestroyImmediate(planetGO);
            Object.DestroyImmediate(oceanGO);
        }
    }
}
