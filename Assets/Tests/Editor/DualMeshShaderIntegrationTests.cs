using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Graphics;
using HexGlobeProject.HexMap;
using HexGlobeProject.Graphics.DataStructures;

namespace HexGlobeProject.Tests.Editor
{
    public class DualMeshShaderIntegrationTests
    {
        [Test]
        public void TerrainConfig_Applied_ToMaterial_And_SegmentsUploaded()
        {
            // Create a TerrainConfig and set overlay parameters
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.overlayEnabled = true;
            cfg.overlayColor = Color.cyan;
            cfg.overlayOpacity = 1f;
            cfg.overlayLineThickness = 0.05f;
            cfg.overlayEdgeExtrusion = 0.5f;

            // Create a temporary material using the PlanetTerrain shader if available
            Shader sh = Shader.Find("HexGlobe/PlanetTerrain");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh);

            // Apply config to material
            TerrainShaderGlobals.Apply(cfg, mat);

            // Generate a small icosphere mesh
            float baseRadius = cfg.baseRadius > 0 ? cfg.baseRadius : 30f;
            var mesh = IcosphereGenerator.GenerateIcosphere(baseRadius, 1);

            // Upload segments to material (no transform, test content in mesh-space)
            int pairs = DualOverlayBuffer.UploadSegmentsToMaterial(mat, mesh, baseRadius, null);

            // Assertions: material has overlay enabled, and buffers created
            Assert.IsTrue(mat.HasProperty("_OverlayEnabled"));
            Assert.AreEqual(1f, mat.GetFloat("_OverlayEnabled"));
            Assert.Greater(pairs, 0, "Expected some dual segment pairs to be generated");

            var buf = DualOverlayBuffer.GetBuffer(mat);
            var bbuf = DualOverlayBuffer.GetBoundsBuffer(mat);
            Assert.IsNotNull(buf, "Segment compute buffer should exist");
            Assert.IsNotNull(bbuf, "Bounds compute buffer should exist");
            Assert.AreEqual(pairs, mat.GetInt("_DualSegmentCount"));

            // Cleanup
            DualOverlayBuffer.ReleaseAll();
            Object.DestroyImmediate(mat);
            Object.DestroyImmediate(cfg);
        }
    }
}
