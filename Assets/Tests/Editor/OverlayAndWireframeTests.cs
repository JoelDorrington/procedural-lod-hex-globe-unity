using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using HexGlobeProject.Graphics.DataStructures;

namespace HexGlobeProject.Tests.Editor
{
    public class OverlayAndWireframeTests
    {
        [Test]
        public void BuildDualWireframe_CreatesWireframeMesh()
        {
            var go = new GameObject("PlanetTest");
            var planet = go.AddComponent<HexMap.Planet>();

            // Create a small base mesh to feed into the wireframe builder
            Mesh baseMesh = IcosphereGenerator.GenerateIcosphere(radius: 1f, subdivisions: 1);

            // Invoke the private BuildDualWireframe via reflection
            var mi = typeof(HexMap.Planet).GetMethod("BuildDualWireframe", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "BuildDualWireframe method should exist");

            mi.Invoke(planet, new object[] { baseMesh, 1f });

            var wireObj = go.transform.Find("Wireframe");
            Assert.IsNotNull(wireObj, "Wireframe GameObject should be created");
            var mf = wireObj.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf, "Wireframe should have a MeshFilter");
            Assert.IsNotNull(mf.sharedMesh, "Wireframe mesh should be assigned");
            Assert.Greater(mf.sharedMesh.vertexCount, 0, "Wireframe mesh should contain vertices");
            var idx = mf.sharedMesh.GetIndices(0);
            Assert.IsNotNull(idx);
            Assert.Greater(idx.Length, 0, "Wireframe mesh should contain line indices");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void TerrainShaderGlobals_Apply_SetsAndClampsProperties()
        {
            var cfg = ScriptableObject.CreateInstance<HexGlobeProject.TerrainSystem.TerrainConfig>();
            cfg.overlayLineThickness = 10f; // intentionally large
            cfg.overlayEdgeExtrusion = 20f; // intentionally large
            cfg.overlayEnabled = true;
            cfg.overlayOpacity = 0.7f;
            cfg.overlayColor = Color.red;

            var sh = Shader.Find("HexGlobe/PlanetTerrain");
            if (sh == null) Assert.Inconclusive("Shader 'HexGlobe/PlanetTerrain' not available in test environment");

            var mat = new Material(sh);
            HexGlobeProject.TerrainSystem.TerrainShaderGlobals.Apply(cfg, mat);

            // LineThickness clamped to max 0.5
            Assert.AreEqual(Mathf.Clamp(cfg.overlayLineThickness, 0.0005f, 0.5f), mat.GetFloat("_LineThickness"));
            // EdgeExtrusion clamped to max 5
            Assert.AreEqual(Mathf.Clamp(cfg.overlayEdgeExtrusion, 0f, 5f), mat.GetFloat("_EdgeExtrusion"));
            // Overlay enabled
            Assert.AreEqual(1f, mat.GetFloat("_OverlayEnabled"));
            // Overlay color and opacity
            Assert.AreEqual(cfg.overlayColor, mat.GetColor("_OverlayColor"));
            Assert.AreEqual(cfg.overlayOpacity, mat.GetFloat("_OverlayOpacity"));

            Object.DestroyImmediate(mat);
        }

        [Test]
        public void Planet_SetOverlayOnMaterials_AppliesToMaterial()
        {
            var go = new GameObject("PlanetTestMat");
            var planet = go.AddComponent<HexMap.Planet>();

            var sh = Shader.Find("HexGlobe/PlanetTerrain");
            if (sh == null) Assert.Inconclusive("Shader 'HexGlobe/PlanetTerrain' not available in test environment");

            var mat = new Material(sh);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            // Create TerrainRoot with config and assign to scene so Planet.SetOverlayOnMaterials picks it up
            var trGO = new GameObject("TerrainRootForTest");
            var tr = trGO.AddComponent<TerrainSystem.TerrainRoot>();
            var cfg = ScriptableObject.CreateInstance<TerrainSystem.TerrainConfig>();
            cfg.overlayEnabled = true;
            cfg.overlayLineThickness = 0.02f;
            cfg.overlayEdgeExtrusion = 1f;
            cfg.overlayColor = Color.green;
            cfg.overlayOpacity = 0.6f;
            tr.config = cfg;

            // Ensure the Planet's private meshRenderer field points to our MeshRenderer so the method operates
            var meshField = typeof(HexMap.Planet).GetField("meshRenderer", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(meshField, "Planet.meshRenderer field should exist");
            meshField.SetValue(planet, mr);

            // Invoke private SetOverlayOnMaterials
            var mi = typeof(HexMap.Planet).GetMethod("SetOverlayOnMaterials", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "SetOverlayOnMaterials should exist");
            mi.Invoke(planet, new object[] { true });

            // Assert material properties reflect the config (after clamping)
            Assert.AreEqual(1f, mat.GetFloat("_OverlayEnabled"));
            Assert.AreEqual(Mathf.Clamp(cfg.overlayLineThickness, 0.0005f, 0.5f), mat.GetFloat("_LineThickness"));
            Assert.AreEqual(Mathf.Clamp(cfg.overlayEdgeExtrusion, 0f, 5f), mat.GetFloat("_EdgeExtrusion"));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(trGO);
        }

        [Test]
        public void ShaderOverlayMask_SimpleVertexProducesVisibleAlpha()
        {
            // Configure parameters to match shader defaults / expected useful values
            float baseRadius = 30f;
            float sphereR = baseRadius * 1.01f; // same multiplier used in Planet
            float cellSize = 1f;
            float lineThickness = 0.05f;
            float overlayOpacity = 1f;
            float edgeExtrusion = 0f; // keep extrusion zero for simple test

            // Build a small icosphere and pick a sample world-space vertex
            var mesh = HexGlobeProject.Graphics.DataStructures.IcosphereGenerator.GenerateIcosphere(radius: sphereR, subdivisions: 2);
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 0);

            Vector3 worldPos = mesh.vertices[0];
            Vector3 planetCenter = Vector3.zero;

            // Reproduce shader logic in C#
            Vector3 planetToVertex = worldPos - planetCenter;
            float worldR = planetToVertex.magnitude;

            // dominant axis face projection
            float axisX = Mathf.Abs(planetToVertex.x);
            float axisY = Mathf.Abs(planetToVertex.y);
            float axisZ = Mathf.Abs(planetToVertex.z);
            Vector2 faceCoord;
            if (axisX >= axisY && axisX >= axisZ)
            {
                faceCoord = new Vector2(planetToVertex.z, planetToVertex.y) / Mathf.Max(1e-6f, axisX);
            }
            else if (axisY >= axisX && axisY >= axisZ)
            {
                faceCoord = new Vector2(planetToVertex.x, planetToVertex.z) / Mathf.Max(1e-6f, axisY);
            }
            else
            {
                faceCoord = new Vector2(planetToVertex.x, planetToVertex.y) / Mathf.Max(1e-6f, axisZ);
            }

            Vector2 u = faceCoord / cellSize;
            const float K = 0.86602540378f;
            const float H = 0.5f;
            Vector2 q = new Vector2(u.x * K, u.y + u.x * H);
            Vector2 f = new Vector2(q.x - Mathf.Floor(q.x) - 0.5f, q.y - Mathf.Floor(q.y) - 0.5f);
            float d = f.magnitude;

            float edge = 1.0f - Mathf.SmoothStep(Mathf.Max(0.0f, lineThickness - 0.01f), lineThickness + 0.01f, d);
            float edgeFactor = Mathf.Clamp01(1.0f - d / (lineThickness + 0.0001f));
            float edgeExtr = edgeExtrusion * edgeFactor;
            float edgeRadius = baseRadius + edgeExtr;
            float surfaceAboveEdge = edgeRadius <= worldR ? 1f : 0f;

            float overlayAlpha = overlayOpacity * edge * surfaceAboveEdge;

            Assert.Greater(overlayAlpha, 0f, $"Overlay alpha was zero — diagnostics: d={d}, edge={edge}, worldR={worldR}, edgeRadius={edgeRadius}, surfaceAboveEdge={surfaceAboveEdge}");
        }
    }
}
