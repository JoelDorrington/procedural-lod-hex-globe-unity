using NUnit.Framework;
using UnityEngine;
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

            // Canonical corners
            var corners = IcosphereMapping.GetCorners(data.id, config.baseRadius, Vector3.zero);

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
                var expected = corners[k];
                // Compare normalized directions (global) to avoid absolute-position scale issues
                var dirMesh = worldFromMesh.normalized;
                var dirExpected = expected.normalized;
                float dot = Vector3.Dot(dirMesh, dirExpected);

                // Diagnostic log for CI/editor console
                Debug.Log($"[TileBuilderSpecificTileTests] Corner[{k}] idx={li} meshWorld={worldFromMesh} expected={expected} dot={dot} tile={data.id}");

                if (dot != 1f)
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

                Assert.AreEqual(1f, dot, 0f, $"Corner {k} direction mismatch: dot={dot}");
            }
        }
    }
}
