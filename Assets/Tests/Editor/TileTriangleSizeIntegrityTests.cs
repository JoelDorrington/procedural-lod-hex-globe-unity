using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Core;
using System.Collections.Generic;
using System.Linq;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Ensure triangle areas inside each generated tile are reasonably uniform
    /// (no triangles spanning the entire tile). This helps catch indexing bugs
    /// that create very large triangles connecting distant vertices.
    /// </summary>
    public class TileTriangleSizeIntegrityTests
    {
        [Test]
        public void TriangleAreas_AreUniformAcrossTile()
        {
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 100f;
            cfg.baseResolution = 16;
            cfg.heightScale = 5f;

            var provider = new SimplePerlinHeightProvider { seed = 42 };
            var builder = new PlanetTileMeshBuilder(cfg, provider, Vector3.zero);

            // Check depths 1..3 to reproduce depth-specific issues
            for (int depth = 1; depth <= 3; depth++)
            {
                int tilesPerEdge = 1 << depth;
                for (int face = 0; face < 20; face++)
                {
                    for (int x = 0; x < tilesPerEdge; x++)
                    for (int y = 0; y < tilesPerEdge; y++)
                    {
                        if (!IcosphereTestHelpers.IsValidTileIndex(x, y, depth)) continue;
                        var id = new TileId(face, x, y, depth);
                        var td = new TileData { id = id, resolution = cfg.baseResolution };
                        builder.BuildTileMesh(td);
                        Assert.IsNotNull(td.mesh, $"Mesh missing for {id}");

                        var areas = TriangleAreas(td.mesh, td.center);
                        // Remove degenerate/near-zero areas
                        areas = areas.Where(a => a > 1e-8f).ToArray();
                        Assert.Greater(areas.Length, 0, $"No valid triangles for {id}");

                        float median = areas[(int)(areas.Length / 2)];
                        // Fail if any triangle is unreasonably large (e.g. > 8x median)
                        float maxAllowedFactor = 8f;
                        float worstFactor = areas.Max(a => a / median);
                        Assert.LessOrEqual(worstFactor, maxAllowedFactor, $"Tile {id} has a triangle {worstFactor:F1}x larger than median (median={median}, max={areas.Max()})");

                        // Also ensure standard deviation relative to mean is acceptable
                        float mean = areas.Average();
                        float variance = areas.Average(a => (a - mean) * (a - mean));
                        float sd = Mathf.Sqrt(variance);
                        Assert.LessOrEqual(sd / mean, 1.5f, $"Tile {id} triangle area spread too large (sd/mean={sd/mean:F2})");
                    }
                }
            }
        }

        private float[] TriangleAreas(Mesh mesh, Vector3 center)
        {
            var verts = mesh.vertices; var tris = mesh.triangles;
            var areas = new List<float>(tris.Length / 3);
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = verts[tris[i]] + center;
                var b = verts[tris[i + 1]] + center;
                var c = verts[tris[i + 2]] + center;
                float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                areas.Add(area);
            }
            return areas.ToArray();
        }
    }
}
