using NUnit.Framework;
using UnityEngine;
using System;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Core;

namespace HexGlobeProject.Tests.Editor
{
    public class TileBuilderSpecificTileTests
    {
        [Test]
        public void BuildMesh_SpecificTile14_3_1_depth2_CornersCanonical()
        {
            // Arrange: target tile (depth=2, face=14, x=3, y=1)
            int depth = 2;
            int face = 14;
            int x = 3;
            int y = 1;
            int res = 8;

            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 10f;
            config.heightScale = 0.5f;
            config.heightProvider = new SimplePerlinHeightProvider();

            var tileId = new TileId(face, x, y, depth);
            var data = new TileData();
            data.id = tileId;
            data.resolution = res;

            var builder = new PlanetTileMeshBuilder(config, config.heightProvider, Vector3.zero);

            // Act: build mesh synchronously
            builder.BuildTileMesh(data);

            // Assert basics
            Assert.IsNotNull(data.mesh, "Mesh should be created for target tile");
            var verts = data.mesh.vertices;
            Assert.IsNotNull(verts, "Mesh vertices should not be null");

            // Compute canonical bary origin and tile span so the test can apply
            // the same Barycentric reflection/clamping behavior as the builder.
            var origin = IcosphereMapping.TileIndexToBaryOrigin(data.id.depth, data.id.x, data.id.y);
            int tilesPerEdge = 1 << data.id.depth;
            float tileWidthWeight = 1f / tilesPerEdge;

            // Helper to find vertex index for a tile-local lattice coordinate
            int FindIndexForLocal(int localX, int localY)
            {
                int idx = 0; int found = -1;
                for (int jj = 0; jj < res; jj++)
                {
                    int maxI = res - 1 - jj;
                    for (int ii = 0; ii <= maxI; ii++)
                    {
                        if (ii == localX && jj == localY) { found = idx; break; }
                        idx++;
                    }
                    if (found >= 0) break;
                }
                return found;
            }

            var cornersLocal = new (int x, int y)[] { (0, 0), (res - 1, 0), (0, res - 1) };
            

            for (int k = 0; k < 3; k++)
            {
                int li = FindIndexForLocal(cornersLocal[k].x, cornersLocal[k].y);
                Assert.IsTrue(li >= 0 && li < verts.Length, $"Corner vertex index {li} out of range for corner {k}");
                var worldFromMesh = verts[li] + data.center;

                // Compute the expected bary (pre-reflect) for this canonical corner
                float u = origin.U;
                float v = origin.V;
                if (k == 1) u = origin.U + tileWidthWeight;
                if (k == 2) v = origin.V + tileWidthWeight;
                var expectedBary = new Barycentric(u, v); // may reflect or clamp
                // Mirror the builder's special-case reflection for far-edge corners so
                // the test computes the same effective bary as the mesh builder.
                int tilesPerFace = 1 << data.id.depth;
                bool isCorner = expectedBary.U == 0f || expectedBary.V == 0f || Mathf.Approximately(expectedBary.W, 0f);
                if (data.id.x + data.id.y == tilesPerFace && isCorner && !expectedBary.IsReflected)
                {
                    expectedBary = new Barycentric(1f - expectedBary.U, 1f - expectedBary.V);
                }
                var expectedDir = IcosphereMapping.BaryToWorldDirection(data.id.face, expectedBary);
                var expected = expectedDir * config.baseRadius + Vector3.zero;
                // Compare world positions directly (mesh vertex + center vs canonical world point)
                var meshWorld = worldFromMesh;
                var canonicalWorld = expectedDir * config.baseRadius + Vector3.zero;
                float dist = Vector3.Distance(meshWorld, canonicalWorld);
                const float kEpsilon = 0.5f;

                // Diagnostic log for CI/editor console
                Debug.Log($"[TileBuilderSpecificTileTests] Corner[{k}] idx={li} meshWorld={meshWorld} expected={canonicalWorld} dist={dist} tile={data.id}");
                // Extra low-level diagnostics: print float bit patterns and stored bary/uv values
                var distBytes = BitConverter.GetBytes(dist);
                uint distBits = BitConverter.ToUInt32(distBytes, 0);
                Debug.Log($"[TileBuilderSpecificTileTests] dist bits=0x{distBits:X8}");

                // Print the stored bary (UV) at the found index and the recomputed world from that bary
                Vector2[] uvs2 = data.mesh.uv;
                if (uvs2 != null && uvs2.Length > li)
                {
                    var foundUv = uvs2[li];
                    Debug.Log($"[TileBuilderSpecificTileTests] stored UV@{li}={foundUv}");
                    var dirFromUv = IcosphereMapping.BaryToWorldDirection(data.id.face, new Barycentric(foundUv.x, foundUv.y));
                    var provider = config.heightProvider ?? new SimplePerlinHeightProvider();
                    float sample = provider.Sample(in dirFromUv, res) * config.heightScale;
                    var recomFromUv = dirFromUv * (config.baseRadius + sample) + Vector3.zero;
                    Debug.Log($"[TileBuilderSpecificTileTests] recomputedFromUV dir={dirFromUv} sample={sample} recomWorld={recomFromUv}");
                }

                // Compute canonical corner using same helper the builder uses and compare
                var canonicalFromMapping = IcosphereMapping.GetCorners(data.id, config.baseRadius, Vector3.zero)[k];
                Debug.Log($"[TileBuilderSpecificTileTests] canonicalCornerFromMapping[{k}]={canonicalFromMapping} canonicalDir={canonicalFromMapping.normalized}");
                float dotWithCanonical = Vector3.Dot(meshWorld.normalized, canonicalFromMapping.normalized);
                Debug.Log($"[TileBuilderSpecificTileTests] dotBetweenMeshAndCanonical={dotWithCanonical}");

                if (dist > kEpsilon)
                {
                    // Find nearest vertex to expected to see if a different index holds the correct position
                    int nearestIdx = -1; float nearestDist = float.MaxValue;
                    for (int vi = 0; vi < verts.Length; vi++)
                    {
                        float d = Vector3.Distance(verts[vi] + data.center, expected);
                        if (d < nearestDist) { nearestDist = d; nearestIdx = vi; }
                    }
                    Debug.Log($"[TileBuilderSpecificTileTests] Nearest vertex to expected: idx={nearestIdx} dist={nearestDist}");

                    // Dump UV (bary) at the found index and at the nearest
                    Vector2[] uvs = data.mesh.uv;
                    if (uvs != null && uvs.Length > 0)
                    {
                        Vector2 uvFound = (li >= 0 && li < uvs.Length) ? uvs[li] : Vector2.positiveInfinity;
                        Vector2 uvNearest = (nearestIdx >= 0 && nearestIdx < uvs.Length) ? uvs[nearestIdx] : Vector2.positiveInfinity;
                        Debug.Log($"[TileBuilderSpecificTileTests] UV found@{li}={uvFound} UV nearest@{nearestIdx}={uvNearest}");
                    }

                    // List triangles that reference the found index
                    var tris = data.mesh.triangles;
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        if (tris[t] == li || tris[t+1] == li || tris[t+2] == li)
                        {
                            Vector3 a = verts[tris[t]] + data.center;
                            Vector3 b = verts[tris[t+1]] + data.center;
                            Vector3 c = verts[tris[t+2]] + data.center;
                            float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                            Debug.Log($"[TileBuilderSpecificTileTests] Tri referencing idx={li}: triIdx={t/3} indices=({tris[t]},{tris[t+1]},{tris[t+2]}) worldA={a} worldB={b} worldC={c} area={area}");
                        }
                    }

                    // Suggest next steps in log to speed up debugging
                    Debug.Log("[TileBuilderSpecificTileTests] Diagnostic: if nearestIdx has small dist then index mapping may be wrong; if not, sampling or bary mapping differs.");
                }

                Assert.AreEqual(0f, dist, kEpsilon, $"Corner {k} world position mismatch: dist={dist}");
            }
        }
    }
}
