using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using System.Linq;

namespace HexGlobeProject.Tests.Editor
{
    public class TileMesh_DepthScalingTests
    {
        [Test]
        public void TileUVs_ScaleWithDepth_TileSizeMatchesExpected()
        {
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 1f;
            cfg.baseResolution = 8;

            int face = 0;
            // Build meshes at depths 0..3 and measure global bary extents per tile
            for (int depth = 0; depth <= 3; depth++)
            {
                int tilesPerEdge = 1 << depth;
                // pick tile in center-ish (x=0,y=0 is fine) to avoid edge reflections
                int x = 0, y = 0;
                var id = new TileId(face, x, y, depth);
                var builder = new PlanetTileMeshBuilder(cfg, null, Vector3.zero);
                var data = new TileData { id = id, resolution = cfg.baseResolution << depth };
                builder.BuildTileMesh(data);
                Assert.IsNotNull(data.mesh, $"Mesh expected at depth {depth}");

                // Convert mesh UVs to global barycentric coordinates (they should already be global)
                var uvs = data.mesh.uv;
                Assert.IsTrue(uvs.Length > 0, "Mesh should have UVs");

                float minU = uvs.Min(u => u.x);
                float maxU = uvs.Max(u => u.x);
                float minV = uvs.Min(u => u.y);
                float maxV = uvs.Max(u => u.y);

                float observedUspan = maxU - minU;
                float observedVspan = maxV - minV;

                // The tile global bary extents should be approximately 1 / (3^depth)
                float expectedSpan = 1f / (1<< depth); // 1 / (2^depth)
                float tol = expectedSpan * 0.25f; // accept 25% tolerance for sampling/rounding differences

                Assert.AreEqual(expectedSpan, observedUspan, tol, $"U span mismatch at depth {depth}: observed {observedUspan} expected {expectedSpan}");
                Assert.AreEqual(expectedSpan, observedVspan, tol, $"V span mismatch at depth {depth}: observed {observedVspan} expected {expectedSpan}");
            }

            Object.DestroyImmediate(cfg);
        }
    }
}
