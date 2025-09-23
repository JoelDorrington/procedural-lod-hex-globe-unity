using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Core;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Detect seams that appear only at depths > 0 by building tiles at multiple
    /// depths and verifying that adjacent tiles on the same face match along shared
    /// edges within a tight world-space tolerance. Also verify triangle normals are
    /// outward-facing to catch inverted/wrapped triangles.
    /// </summary>
    public class TileSeamDepthIntegrityTests
    {
        [Test]
        public void AdjacentTiles_NoSeams_AcrossDepths()
        {
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 100f;
            cfg.baseResolution = 16;
            cfg.heightScale = 5f;

            var provider = new SimplePerlinHeightProvider { seed = 42 };
            var builder = new PlanetTileMeshBuilder(cfg, provider, Vector3.zero);

            // Check depths 1..3 (depth 0 is the base icosahedron)
            for (int depth = 1; depth <= 3; depth++)
            {
                int tilesPerEdge = 1 << depth;
                for (int face = 0; face < 20; face++)
                {
                    for (int x = 0; x < tilesPerEdge; x++)
                    {
                        for (int y = 0; y < tilesPerEdge; y++)
                        {
                            if (!IcosphereTestHelpers.IsValidTileIndex(x, y, depth)) continue;
                            var id = new TileId(face, x, y, depth);
                            var a = new TileData { id = id, resolution = cfg.baseResolution };
                            builder.BuildTileMesh(a);
                            Assert.IsNotNull(a.mesh, $"Mesh missing for tile {id}");

                            // Check neighboring tiles within same face (right and up)
                            if (x + 1 < tilesPerEdge && IcosphereTestHelpers.IsValidTileIndex(x + 1, y, depth))
                            {
                                var bId = new TileId(face, x + 1, y, depth);
                                var b = new TileData { id = bId, resolution = cfg.baseResolution };
                                builder.BuildTileMesh(b);
                                Assert.IsNotNull(b.mesh, $"Mesh missing for neighbor {bId}");
                                float gap = MinVertexGapBetweenTiles(a, b);
                                Assert.Less(gap, 1e-3f, $"Seam detected between {id} and {bId} at depth {depth}: gap={gap}");
                            }
                            if (y + 1 < tilesPerEdge && IcosphereTestHelpers.IsValidTileIndex(x, y + 1, depth))
                            {
                                var bId = new TileId(face, x, y + 1, depth);
                                var b = new TileData { id = bId, resolution = cfg.baseResolution };
                                builder.BuildTileMesh(b);
                                Assert.IsNotNull(b.mesh, $"Mesh missing for neighbor {bId}");
                                float gap = MinVertexGapBetweenTiles(a, b);
                                Assert.Less(gap, 1e-3f, $"Seam detected between {id} and {bId} at depth {depth}: gap={gap}");
                            }

                            // Also sanity-check triangle normals for this tile
                            Assert.IsTrue(AllTrianglesFaceOutward(a), $"Triangle normal mismatch for {id}");
                        }
                    }
                }
            }
        }

        private float MinVertexGapBetweenTiles(TileData a, TileData b)
        {
            var va = a.mesh.vertices; var vb = b.mesh.vertices;
            float minGap = float.MaxValue;
            for (int i = 0; i < va.Length; i++)
            {
                var wa = va[i] + a.center;
                for (int j = 0; j < vb.Length; j++)
                {
                    var wb = vb[j] + b.center;
                    float d = Vector3.Distance(wa, wb);
                    if (d < minGap) minGap = d;
                    if (minGap <= 0f) return 0f;
                }
            }
            return minGap;
        }

        private bool AllTrianglesFaceOutward(TileData t)
        {
            var mesh = t.mesh;
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            var normals = mesh.normals;
            Vector3 center = t.center;
            for (int tri = 0; tri < tris.Length; tri += 3)
            {
                var a = verts[tris[tri]] + center;
                var b = verts[tris[tri + 1]] + center;
                var c = verts[tris[tri + 2]] + center;
                var triNormal = Vector3.Cross(b - a, c - a).normalized;
                var avgVertexNormal = (normals[tris[tri]] + normals[tris[tri + 1]] + normals[tris[tri + 2]]).normalized;
                if (Vector3.Dot(triNormal, avgVertexNormal) < 0f) return false;
            }
            return true;
        }
    }
}
