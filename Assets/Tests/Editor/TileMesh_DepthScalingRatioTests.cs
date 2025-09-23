using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using System.Linq;

namespace HexGlobeProject.Tests.Editor
{
    public class TileMesh_DepthScalingRatioTests
    {
        [Test]
        public void UVSpanRatio_EqualsPow3Depth()
        {
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 1f;
            cfg.baseResolution = 8;

            int face = 0;
            int x = 0, y = 0;

            // Build reference mesh at depth 0
            var id0 = new TileId(face, x, y, 0);
            var builder0 = new PlanetTileMeshBuilder(cfg, null, Vector3.zero);
            var data0 = new TileData { id = id0, resolution = cfg.baseResolution };
            builder0.BuildTileMesh(data0);
            Assert.IsNotNull(data0.mesh, "Depth0 mesh expected");
            var uvs0 = data0.mesh.uv;
            float u0span = uvs0.Max(u => u.x) - uvs0.Min(u => u.x);
            float v0span = uvs0.Max(u => u.y) - uvs0.Min(u => u.y);

            for (int depth = 1; depth <= 3; depth++)
            {
                var id = new TileId(face, x, y, depth);
                var builder = new PlanetTileMeshBuilder(cfg, null, Vector3.zero);
                var data = new TileData { id = id, resolution = cfg.baseResolution << depth };
                builder.BuildTileMesh(data);
                Assert.IsNotNull(data.mesh, $"Mesh expected at depth {depth}");
                var uvs = data.mesh.uv;
                float uspan = uvs.Max(u => u.x) - uvs.Min(u => u.x);
                float vspan = uvs.Max(u => u.y) - uvs.Min(u => u.y);

                float expectedRatio = 1 << depth; // depth0 span / depthN span == 3^depth
                // Protect against divide by zero
                Assert.Greater(uspan, 1e-8f, "U span must be positive");
                Assert.Greater(vspan, 1e-8f, "V span must be positive");

                float observedURatio = u0span / uspan;
                float observedVRatio = v0span / vspan;

                float relTol = 0.25f; // allow 25% relative tolerance
                Assert.AreEqual(expectedRatio, observedURatio, expectedRatio * relTol, $"U span ratio mismatch at depth {depth}: observed {observedURatio} expected {expectedRatio}");
                Assert.AreEqual(expectedRatio, observedVRatio, expectedRatio * relTol, $"V span ratio mismatch at depth {depth}: observed {observedVRatio} expected {expectedRatio}");
            }

            Object.DestroyImmediate(cfg);
        }
    }
}
