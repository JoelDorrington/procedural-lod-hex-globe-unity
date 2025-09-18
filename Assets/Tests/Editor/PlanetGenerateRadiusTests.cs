using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Core;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetGenerateRadiusTests
    {
        [Test]
        public void GeneratePlanet_UsesTerrainRootBaseRadiusWithCurvedMultiplier()
        {
            // Arrange: create TerrainConfig and TerrainRoot
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 12f;
            config.seaLevel = 0f;

            var terrainGO = new GameObject("TerrainRootTest");
            var terrainRoot = terrainGO.AddComponent<TerrainRoot>();
            terrainRoot.config = config;

            var planetGO = new GameObject("PlanetTest");
            var planet = planetGO.AddComponent<HexGlobeProject.HexMap.Planet>();
            planet.hideOceanRenderer = true; // avoid visual renderer enabling issues

            // Act
            planet.GeneratePlanet();

            var mesh = planetGO.GetComponent<MeshFilter>()?.sharedMesh;
            Assert.IsNotNull(mesh, "Planet must have generated an icosphere mesh");

            // Compute average vertex radius from origin
            var verts = mesh.vertices;
            float sum = 0f;
            for (int i = 0; i < verts.Length; i++) sum += verts[i].magnitude;
            float avg = sum / verts.Length;

            float expected = config.baseRadius * 1.01f;

            // Allow small tolerance due to normalization/subdivision rounding
            Assert.AreEqual(expected, avg, expected * 0.01f, $"Generated sphere average radius should be approx {expected}");

            // Cleanup
            Object.DestroyImmediate(planetGO);
            Object.DestroyImmediate(terrainGO);
            Object.DestroyImmediate(config);
        }
    }
}
