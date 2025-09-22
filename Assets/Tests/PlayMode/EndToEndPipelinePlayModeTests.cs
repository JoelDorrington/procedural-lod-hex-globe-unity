using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using HexGlobeProject.Tests.PlayMode;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.PlayMode
{
    public class EndToEndPipelinePlayModeTests
    {
        [UnityTest]
        public IEnumerator BuildScene_And_VerifyTiles_AreSpawned_WithValidMeshes()
        {
            var builder = new PlaymodeTestSceneBuilder();
            builder.Build();

            var mgr = builder.Manager;

            // Disable camera-driven depth sync so test can set depth deterministically
            var debugField = mgr.GetType().GetField("debugDisableCameraDepthSync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (debugField != null) debugField.SetValue(mgr, true);

            // Initialize manager for depth 0 so we can validate the base-tile vertex distribution
            try { mgr.SetDepth(0); } catch { }
            // Allow prioritized spawn worker time to create tiles
            yield return new WaitForSecondsRealtime(1.0f);

            var active = mgr.GetActiveTiles();
            Assert.Greater(active.Count, 0, "Expected at least one active tile after spawn");

            // For each active depth-0 tile verify its UVs match a triangular barycentric lattice
            float uvTol = 1e-4f;
            foreach (var t in active)
            {
                Assert.IsNotNull(t.gameObject, "Tile GameObject should not be null");
                Assert.IsNotNull(t.tileData, "TileData should be assigned");
                Assert.IsNotNull(t.tileData.mesh, "Tile mesh should be generated");

                var mesh = t.tileData.mesh;
                // Ensure normals exist and count matches
                Assert.AreEqual(mesh.vertexCount, mesh.normals.Length, "Mesh should have equal count of normals and vertices");

                int res = t.tileData.resolution;
                int expectedCount = res * (res + 1) / 2;
                Assert.AreEqual(expectedCount, mesh.uv.Length, $"Tile {t.tileData.id} UV count should match triangular lattice count (res={res})");

                // Build expected bary UVs for this tile resolution
                float step = 1f / (res - 1);
                var expected = new System.Collections.Generic.List<Vector2>(expectedCount);
                for (int i = 0; i < res; i++)
                {
                    for (int j = 0; j < res - i; j++)
                    {
                        expected.Add(new Vector2(i * step, j * step));
                    }
                }

                var matched = new bool[expected.Count];
                for (int k = 0; k < mesh.uv.Length; k++)
                {
                    var uv = mesh.uv[k];
                    bool found = false;
                    for (int e = 0; e < expected.Count; e++)
                    {
                        if (!matched[e] && Vector2.Distance(uv, expected[e]) <= uvTol)
                        {
                            matched[e] = true;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        Assert.Fail($"Tile {t.tileData.id} contains unexpected UV coordinate {uv} (res={res})");
                    }
                }

                for (int e = 0; e < matched.Length; e++)
                {
                    Assert.IsTrue(matched[e], $"Tile {t.tileData.id} is missing expected UV at {expected[e]} (res={res})");
                }
            }

            builder.Teardown();
        }
    }
}
