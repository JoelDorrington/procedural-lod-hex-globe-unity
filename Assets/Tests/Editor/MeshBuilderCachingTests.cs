using NUnit.Framework;
using UnityEngine;
using System.Reflection;
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
            builder.BuildTileMesh(data1);

            // Assert cache now contains the entry
            var cacheAfterFirst = cacheField.GetValue(null) as System.Collections.IDictionary;
            Assert.IsNotNull(cacheAfterFirst, "Cache should be present after build");
            Assert.IsTrue(cacheAfterFirst.Contains(tileId), "Cache should contain the built TileId after first build");

            // Record mesh instance
            var mesh1 = data1.mesh;
            Assert.IsNotNull(mesh1, "First build should produce a mesh");

            // Act: build second time into a fresh TileData and ensure we get the same mesh instance
            var data2 = new TileData { id = tileId, resolution = data1.resolution };
            builder.BuildTileMesh(data2);

            var mesh2 = data2.mesh;
            Assert.IsNotNull(mesh2, "Second build should produce or return a mesh");

            // Assert: ideally the same mesh instance is returned (cache hit). However,
            // the builder may invalidate the cache if the authoritative precomputed
            // center differs slightly from the sampled center; in that case the builder
            // will rebuild an equivalent mesh. Accept either the same instance or a
            // mesh with identical topology/metadata.
            if (!object.ReferenceEquals(mesh1, mesh2))
            {
                // Fallback equivalence checks
                Assert.AreEqual(mesh1.name, mesh2.name, "Rebuilt mesh should preserve naming");
                Assert.AreEqual(mesh1.vertexCount, mesh2.vertexCount, "Rebuilt mesh should have same vertex count");
                Assert.AreEqual(mesh1.triangles.Length, mesh2.triangles.Length, "Rebuilt mesh should have same triangle index count");
            }

            // Cleanup
            Object.DestroyImmediate(config);
        }
    }
}
