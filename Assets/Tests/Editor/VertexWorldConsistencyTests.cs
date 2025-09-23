using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Core;

namespace HexGlobeProject.Tests.Editor
{
    public class VertexWorldConsistencyTests
    {
        [Test]
        public void MeshVertices_MatchExpectedWorldPositions()
        {
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 1f;
            cfg.baseResolution = 8;
            cfg.heightScale = 0.1f;

            int depth = 1;
            int tilesPerEdge = 1 << depth;
            var planetCenter = Vector3.zero;
            var registry = new TerrainTileRegistry(depth, cfg.baseRadius, planetCenter);
            var builder = new PlanetTileMeshBuilder(cfg, null, planetCenter);

            foreach (var entry in registry.GetAllTiles())
            {
                var id = new TileId(entry.face, entry.x, entry.y, entry.depth);
                var data = new TileData { id = id, resolution = cfg.baseResolution };
                builder.BuildTileMesh(data, registry);
                Assert.IsNotNull(data.mesh, "Mesh was not created");

                var mesh = data.mesh;
                // For each mesh vertex, compute expected world position and compare
                var verts = mesh.vertices;
                int res = data.resolution;
                int idx = 0;
                foreach (var global in IcosphereMapping.TileVertexBarys(res, entry.depth, entry.x, entry.y))
                {
                    // TileVertexBarys currently yields global barycentric coordinates
                    // (it already accounts for tile x/y and depth). Use them directly.
                    var dir = IcosphereMapping.BaryToWorldDirection(entry.face, global);
                    // Use builder's height provider chain (default SimplePerlin)
                    var provider = cfg.heightProvider ?? new SimplePerlinHeightProvider();
                    float raw = provider.Sample(in dir, res);
                    float worldRad = cfg.baseRadius + raw * cfg.heightScale;
                    var expectedWorld = dir * worldRad + planetCenter;

                    var actualWorld = data.center + verts[idx];
                    float d = (expectedWorld - actualWorld).magnitude;
                    Assert.Less(d, 1e-4f, $"Vertex mismatch for tile {id} bary {global}: delta={d}");
                    idx++;
                }
            }
        }
    }
}
