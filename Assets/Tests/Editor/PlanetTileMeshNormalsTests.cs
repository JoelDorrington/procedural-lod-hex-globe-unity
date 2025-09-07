using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections.Generic;
using System.Reflection;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileMeshNormalsTests
    {
        [Test]
        public void BuildTileMesh_NormalsAreNotPurelyRadial_WhenGeometryHasRelief()
        {
            // Arrange
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 10f;
            config.baseResolution = 8;
            config.heightScale = 1f;
            config.seaLevel = 0f;
            config.recalcNormals = true;

            // ensure a concrete provider that produces non-zero variation
            config.heightProvider = new SimplePerlinHeightProvider();

            var builder = new PlanetTileMeshBuilder(config);

            var tileId = new TileId(0, 0, 0, 0);
            var data = new TileData { id = tileId, resolution = 8 };

            // Populate precomputed registry for depth 0
            var registryField = typeof(PlanetTileVisibilityManager).GetField("s_precomputedRegistry", BindingFlags.NonPublic | BindingFlags.Static);
            var registry = (Dictionary<int, List<PlanetTileVisibilityManager.PrecomputedTileEntry>>)registryField.GetValue(null);
            if (registry == null)
            {
                registry = new Dictionary<int, List<PlanetTileVisibilityManager.PrecomputedTileEntry>>();
                registryField.SetValue(null, registry);
            }
            var entry = new PlanetTileVisibilityManager.PrecomputedTileEntry();
            entry.normal = new Vector3(0f, 0f, 1f);
            entry.face = 0; entry.x = 0; entry.y = 0;
            entry.uCenter = 0.5f; entry.vCenter = 0.5f; entry.tilesPerEdge = 1;
            entry.tileOffsetU = 0f; entry.tileOffsetV = 0f;
            entry.centerWorld = entry.normal * (config.baseRadius * 1.01f);
            entry.cornerWorldPositions = new Vector3[3] { entry.centerWorld, entry.centerWorld, entry.centerWorld };
            registry[0] = new List<PlanetTileVisibilityManager.PrecomputedTileEntry> { entry };

            float rawMin = float.MaxValue, rawMax = float.MinValue;

            // Act
            builder.BuildTileMesh(data, ref rawMin, ref rawMax);

            // Assert: mesh created
            Assert.IsNotNull(data.mesh);
            Assert.Greater(data.mesh.vertexCount, 0);

            var verts = data.mesh.vertices;
            var norms = data.mesh.normals;
            Assert.AreEqual(verts.Length, norms.Length, "Vertex and normal counts should match");

            // Compute fraction of vertices where angle between normal and radial direction > threshold
            int countDiff = 0;
            float thresholdDeg = 0.5f; // half-degree tolerance
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                if (v.sqrMagnitude <= 1e-9f) continue;
                var radial = v.normalized;
                float angleDeg = Vector3.Angle(radial, norms[i]);
                if (angleDeg > thresholdDeg) countDiff++;
            }

            // Expect some measurable number of normals to deviate from radial direction when relief exists
            Assert.Greater(countDiff, 0, "Expected some vertex normals to deviate from purely radial direction (showing geometric slopes)");
        }

        [Test]
        public void HeightProvider_SamplesVaryAcrossDirections()
        {
            // Arrange
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 10f;
            config.heightScale = 1f;
            config.heightProvider = new SimplePerlinHeightProvider();

            var provider = config.heightProvider;
            Assert.IsNotNull(provider);

            // Sample multiple directions
            var dirs = new Vector3[] { Vector3.up, Vector3.right, Vector3.forward, new Vector3(0.3f, 0.7f, 0.2f).normalized };
            float prev = provider.Sample(in dirs[0], 8);
            bool varied = false;
            for (int i = 1; i < dirs.Length; i++)
            {
                float s = provider.Sample(in dirs[i], 8);
                if (Mathf.Abs(s - prev) > 1e-4f) varied = true;
                prev = s;
            }

            Assert.IsTrue(varied, "SimplePerlinHeightProvider should produce variation across directions");
        }
    }
}
