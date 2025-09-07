using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;
using System.Reflection;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileMeshBuilderFallbackTests
    {
        [Test]
        public void BuildTileMesh_WithNullSerializedProvider_UsesFallbackAndProducesNonFlatMesh()
        {
            // Arrange: create a minimal TerrainConfig with null heightProvider
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 10f;
            config.baseResolution = 4;
            config.heightScale = 1f;
            config.seaLevel = 0f;

            // Explicitly ensure heightProvider is null to simulate deserialization failure
            config.heightProvider = null;

            // Create mesh builder with null injected provider (constructor param) too
            var builder = new PlanetTileMeshBuilder(config);

            // Create a simple TileData: pick face 0, x=0,y=0, depth=0 so mapping exists
            var tileId = new TileId(0, 0, 0, 0);
            var data = new TileData();
            data.id = tileId;
            data.resolution = 4; // small grid for test speed

            // The mesh builder relies on a precomputed tile registry inside PlanetTileVisibilityManager.
            // In editor/unit test runs this registry may be empty. Populate it via reflection so the
            // builder can find a precomputed entry for depth=0.
            var registryField = typeof(PlanetTileVisibilityManager).GetField("s_precomputedRegistry", BindingFlags.NonPublic | BindingFlags.Static);
            var registry = (Dictionary<int, List<PlanetTileVisibilityManager.PrecomputedTileEntry>>)registryField.GetValue(null);
            if (registry == null)
            {
                registry = new Dictionary<int, List<PlanetTileVisibilityManager.PrecomputedTileEntry>>();
                registryField.SetValue(null, registry);
            }
            var entry = new PlanetTileVisibilityManager.PrecomputedTileEntry();
            entry.normal = new Vector3(0f, 0f, 1f);
            entry.face = 0;
            entry.x = 0; entry.y = 0;
            entry.uCenter = 0.5f; entry.vCenter = 0.5f;
            entry.tilesPerEdge = 1;
            entry.tileOffsetU = 0f; entry.tileOffsetV = 0f;
            entry.centerWorld = entry.normal * (config.baseRadius * 1.01f);
            entry.cornerWorldPositions = new Vector3[3] { entry.centerWorld, entry.centerWorld, entry.centerWorld };
            registry[0] = new List<PlanetTileVisibilityManager.PrecomputedTileEntry> { entry };

            float rawMin = float.MaxValue;
            float rawMax = float.MinValue;

            // Act
            builder.BuildTileMesh(data, ref rawMin, ref rawMax);

            // Assert
            Assert.IsNotNull(data.mesh, "Mesh should be assigned even when serialized provider is null");
            Assert.IsTrue(rawMax > rawMin, "Raw sampled heights should have variation when using fallback provider");
            Assert.IsTrue(data.mesh.vertexCount > 0, "Mesh should have vertices");
        }
    }
}
