using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Core;

namespace HexGlobeProject.Tests.Editor
{
    public class TileMesh_TransformConsistencyTests
    {
        [SetUp]
        public void SetUp()
        {
            PlanetTileMeshBuilder.ClearCache();
        }

        [Test]
        public void BuiltMeshVerticesMatchIcosphereWorldPositions()
        {
            // Arrange
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 30f;
            config.baseResolution = 8;
            config.heightScale = 1f;
            config.recalcNormals = false;
            var provider = new SimplePerlinHeightProvider();

            var builder = new PlanetTileMeshBuilder(config, provider, Vector3.zero);

            var tileId = new TileId(0, 0, 0, 1);
            var tileData = new TileData { id = tileId, resolution = config.baseResolution };

            // Act
            builder.BuildTileMesh(tileData);

            Assert.IsNotNull(tileData.mesh, "Mesh should be created");

            var verts = tileData.mesh.vertices;

            // Build expected world positions from bary coordinates
            var expected = new System.Collections.Generic.List<Vector3>(verts.Length);
            int res = tileData.resolution;
            int idx = 0;
            foreach (var uv in IcosphereMapping.TileVertexBarys(res))
            {
                var global = IcosphereMapping.BaryLocalToGlobal(tileData.id, uv, res);
                var dir = IcosphereMapping.BaryToWorldDirection(tileData.id.face, global.x, global.y);
                // sample height using provider
                float h = provider.Sample(in dir, res) * config.heightScale;
                var world = dir * (config.baseRadius + h) + tileData.center;
                expected.Add(world);
                idx++;
            }

            Assert.AreEqual(expected.Count, verts.Length, "vertex counts should match between mapping and mesh");

            // Assert vertex world positions match expected
            for (int i = 0; i < verts.Length; i++)
            {
                var worldPos = verts[i] + tileData.center;
                float d2 = (worldPos - expected[i]).sqrMagnitude;
                // We need to allow a little deviation for the height sampling
                Assert.LessOrEqual(d2, 50f, $"Vertex world mismatch at index {i}: expected {expected[i]} got {worldPos} (sqr={d2})");
            }

            // Cleanup
            Object.DestroyImmediate(config);
        }
    }
}
