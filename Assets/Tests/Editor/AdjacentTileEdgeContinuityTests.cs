using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Core;

namespace HexGlobeProject.Tests.Editor
{
    public class AdjacentTileEdgeContinuityTests
    {
        private TerrainConfig config;
        private PlanetTileMeshBuilder builder;

        private static int[,] res8vertLattice = {
            {0,1,2,3,4,5,6,7},
            {8,9,10,11,12,13,14,-1},
            {15,16,17,18,19,20,-1,-1},
            {21,22,23,24,25,-1,-1,-1},
            {26,27,28,29,-1,-1,-1,-1},
            {30,31,32,-1,-1,-1,-1,-1},
            {33,34,-1,-1,-1,-1,-1,-1},
            {35,-1,-1,-1,-1,-1,-1,-1}
        };

        private static int[] res8EdgeIndices = {
            0, 1, 2, 3, 4, 5, 6,
            7, 14, 20, 25, 29, 32, 34, 35,
            8, 15, 21, 26, 30, 33,
        };

        [SetUp]
        public void SetUp()
        {
            PlanetTileMeshBuilder.ClearCache();
            config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 30f;
            config.baseResolution = 8;
            config.heightScale = 1f;
            config.recalcNormals = false;
            var provider = new SimplePerlinHeightProvider();
            builder = new PlanetTileMeshBuilder(config, provider, Vector3.zero);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void SharedEdgeVertices_AreIdenticalBetweenAdjacentTiles()
        {
            int depth = 0;
            var tileAId = new TileId(0, 0, 0, depth);
            var tileBId = new TileId(1, 0, 0, depth);

            var dataA = new TileData { id = tileAId, resolution = config.baseResolution };
            var dataB = new TileData { id = tileBId, resolution = config.baseResolution };

            builder.BuildTileMesh(dataA);
            builder.BuildTileMesh(dataB);

            Assert.IsNotNull(dataA.mesh);
            Assert.IsNotNull(dataB.mesh);

            var cornersA = IcosphereMapping.GetCorners(tileAId, config.baseRadius);
            var cornersB = IcosphereMapping.GetCorners(tileBId, config.baseRadius);
            var matchingCorners = new Vector3[2];
            foreach(var c1 in cornersA)
            {
                foreach (var c2 in cornersB)
                {
                    if (Vector3.Distance(c1, c2) < 0.1f)
                    {
                        Debug.Log($"Matching corner found: {c1} == {c2}");
                        if (matchingCorners[0] == Vector3.zero)
                        {
                            matchingCorners[0] = c1;
                        }
                        else
                        {
                            matchingCorners[1] = c1;
                        }
                    }
                    else
                    {
                        Debug.Log($"No match: {c1} != {c2}");
                    }
                }
            }

            int matchedVerts = 0;
            // Derive edge vertex indices from mesh UVs (barycentric coordinates). Vertex ordering
            // may vary but the per-vertex barycentric coords are stored in mesh.uv in the same
            // order as mesh.vertices.
            var aUVs = new Vector2[dataA.mesh.vertexCount];
            var bUVs = new Vector2[dataB.mesh.vertexCount];
            dataA.mesh.GetUVs(0, new System.Collections.Generic.List<Vector2>(aUVs));
            dataB.mesh.GetUVs(0, new System.Collections.Generic.List<Vector2>(bUVs));

            var aEdgeIndices = new System.Collections.Generic.List<int>();
            var bEdgeIndices = new System.Collections.Generic.List<int>();
            float eps = 1e-4f;
            for (int i = 0; i < dataA.mesh.vertexCount; i++)
            {
                var uv = dataA.mesh.uv[i];
                // On an edge when u==0 or v==0 or u+v==1 (mirrored)
                if (Mathf.Abs(uv.x) < eps || Mathf.Abs(uv.y) < eps || Mathf.Abs(uv.x + uv.y - 1f) < eps)
                    aEdgeIndices.Add(i);
            }
            for (int i = 0; i < dataB.mesh.vertexCount; i++)
            {
                var uv = dataB.mesh.uv[i];
                if (Mathf.Abs(uv.x) < eps || Mathf.Abs(uv.y) < eps || Mathf.Abs(uv.x + uv.y - 1f) < eps)
                    bEdgeIndices.Add(i);
            }

            // Compare world-space sampled positions for these edge vertices and avoid double-counting.
            var matchedA = new System.Collections.Generic.HashSet<int>();
            float worldTol = 1e-3f;
            foreach (int aIndex in aEdgeIndices)
            {
                Vector3 aVertLocal = dataA.mesh.vertices[aIndex];
                Vector3 aWorldPos = dataA.center + aVertLocal;
                foreach (int bIndex in bEdgeIndices)
                {
                    Vector3 bVertLocal = dataB.mesh.vertices[bIndex];
                    Vector3 bWorldPos = dataB.center + bVertLocal;
                    if (Vector3.Distance(aWorldPos, bWorldPos) < worldTol)
                    {
                        if (!matchedA.Contains(aIndex))
                        {
                            matchedA.Add(aIndex);
                            matchedVerts++;
                        }
                        break; // A's vertex matched, move to next A
                    }
                    else
                    {
                        // Debug: print barycentric UV and world direction for mismatch cases to diagnose seam generation
                        var aUV = dataA.mesh.uv[aIndex];
                        var bUV = dataB.mesh.uv[bIndex];
                        var aDir = IcosphereMapping.BaryToWorldDirection(dataA.id.face, new Barycentric(aUV.x, aUV.y));
                        var bDir = IcosphereMapping.BaryToWorldDirection(dataB.id.face, new Barycentric(bUV.x, bUV.y));
                        Debug.Log($"Edge mismatch: A idx={aIndex} uv={aUV} dir={aDir} world={aWorldPos} | B idx={bIndex} uv={bUV} dir={bDir} world={bWorldPos}");
                    }
                }
            }

            Assert.AreEqual(config.baseResolution, matchedVerts, $"Expected {config.baseResolution} matching edge vertices, found {matchedVerts}");
        }
    }
}
