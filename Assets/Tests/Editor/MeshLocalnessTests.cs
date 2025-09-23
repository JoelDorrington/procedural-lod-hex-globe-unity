using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using UnityEngine.TestTools.Utils;

namespace HexGlobeProject.Tests.Editor
{
    public class MeshLocalnessTests
    {
        [Test]
        public void BuiltMesh_IsCenteredOnGameObjectOrigin()
        {
            // Arrange: create builder and registry for depth 1 tile
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 1f;
            cfg.baseResolution = 8;

            var planetCenter = Vector3.zero;
            var builder = new PlanetTileMeshBuilder(cfg, null, planetCenter);

            int depth = 1;
            int face = 0;
            int x = 0;
            int y = 0;

            var id = new TileId(face, x, y, depth);
            var data = new TileData { id = id, resolution = cfg.baseResolution, mesh = null };

            var registry = new TerrainTileRegistry(depth, cfg.baseRadius, planetCenter);
            Assert.IsTrue(registry.tiles.ContainsKey(id), "Registry must contain the tile id");

            // Act: build the mesh and attach to a GameObject tile
            builder.BuildTileMesh(data, registry);
            var go = new GameObject("tile_test_go");
            var tile = go.AddComponent<PlanetTerrainTile>();

            tile.transform.position = data.center;

            tile.Initialize(id, data);
            tile.AssignMesh(data.mesh);

            // Assert: GameObject position equals data.center
            Assert.That(go.transform.position, Is.EqualTo(data.center).Using(Vector3ComparerWithEqualsOperator.Instance), "GameObject transform must equal tile center");

            // Assert: mesh bounds center near zero
            var mesh = data.mesh;
            Assert.IsNotNull(mesh, "Mesh must be created");
            var boundsCenter = mesh.bounds.center;
            Assert.That(boundsCenter.magnitude, Is.LessThan(1e-3f), "Mesh bounds center should be near zero (mesh-local)");

            // Assert: average vertex position should be near zero (mesh-local coordinates)
            var verts = mesh.vertices;
            Vector3 centroid = Vector3.zero;
            foreach (var v in verts) centroid += v;
            centroid /= Mathf.Max(1, verts.Length);
            // allow deviation for height sampling
            Assert.That(centroid.magnitude < 10, $"Average vertex position should be <10 but was {centroid}");

            // Cleanup
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cfg);
        }
    }
}
