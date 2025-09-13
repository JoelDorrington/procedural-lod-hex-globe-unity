using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    [TestFixture]
    public class MeshBuilderCachingTests
    {
        [Test]
        public void BuildTileMesh_IsCached_AvoidsDuplicateRebuilds()
        {
            // Arrange
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 1f;
            config.baseResolution = 8;

            // Use an arbitrary planet center
            Vector3 planetCenter = Vector3.zero;
            var builder = new PlanetTileMeshBuilder(config, null, planetCenter);

            // Pick a valid tile id (face 0, x=0,y=0 at depth=2 should be valid)
            int depth = 2;
            int face = 0;
            int x = 0, y = 0;
            var tileId = new TileId(face, x, y, depth);

            // Prepare TileData
            var data1 = new TileData { id = tileId, resolution = Mathf.Max(8, config.baseResolution << depth) };
            float rawMin1 = float.MaxValue, rawMax1 = float.MinValue;

            // Access private static cache via reflection
            var builderType = typeof(PlanetTileMeshBuilder);
            var cacheField = builderType.GetField("s_meshCache", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(cacheField, "Expected private static s_meshCache field on PlanetTileMeshBuilder");

            var cache = cacheField.GetValue(null) as System.Collections.IDictionary;
            // Clear cache if present (ensure clean slate)
            if (cache != null)
            {
                cache.Clear();
            }

            // Act: build first time
            builder.BuildTileMesh(data1, ref rawMin1, ref rawMax1);

            // Assert cache now contains the entry
            var cacheAfterFirst = cacheField.GetValue(null) as System.Collections.IDictionary;
            Assert.IsNotNull(cacheAfterFirst, "Cache should be present after build");
            Assert.IsTrue(cacheAfterFirst.Contains(tileId), "Cache should contain the built TileId after first build");

            // Record mesh instance
            var mesh1 = data1.mesh;
            Assert.IsNotNull(mesh1, "First build should produce a mesh");

            // Act: build second time into a fresh TileData and ensure we get the same mesh instance
            var data2 = new TileData { id = tileId, resolution = data1.resolution };
            float rawMin2 = float.MaxValue, rawMax2 = float.MinValue;
            builder.BuildTileMesh(data2, ref rawMin2, ref rawMax2);

            var mesh2 = data2.mesh;
            Assert.IsNotNull(mesh2, "Second build should produce or return a mesh");

            // Assert: same mesh instance returned (cache hit)
            Assert.AreSame(mesh1, mesh2, "Repeated BuildTileMesh for identical TileId/resolution should return the same Mesh instance from cache");

            // Cleanup
            Object.DestroyImmediate(config);
        }
    }
}
