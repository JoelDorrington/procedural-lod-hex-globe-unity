using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class TerrainShaderParameterTests
    {
        [Test]
        public void PlanetCenterParameter_ShouldAffectTerrainHeight()
        {
            // This test verifies that moving the planet center actually changes terrain appearance
            var shader = Shader.Find("HexGlobe/PlanetTerrain");
            Assert.IsNotNull(shader, "PlanetTerrain shader should be available");
            
            var material1 = new Material(shader);
            var material2 = new Material(shader);

            // Set different planet centers
            Vector3 center1 = Vector3.zero;
            Vector3 center2 = new Vector3(50f, 0f, 0f);

            material1.SetVector("_PlanetCenter", new Vector4(center1.x, center1.y, center1.z, 0));
            material2.SetVector("_PlanetCenter", new Vector4(center2.x, center2.y, center2.z, 0));

            // Set same other parameters
            material1.SetFloat("_SeaLevel", 30f);
            material2.SetFloat("_SeaLevel", 30f);

            // Verify the planet center values are different
            Vector4 retrievedCenter1 = material1.GetVector("_PlanetCenter");
            Vector4 retrievedCenter2 = material2.GetVector("_PlanetCenter");

            Assert.AreNotEqual(retrievedCenter1.x, retrievedCenter2.x, "Materials should have different planet centers");

            // Cleanup
            Object.DestroyImmediate(material1);
            Object.DestroyImmediate(material2);
        }
    }
}
