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

        [SetUp]
        public void SetUp()
        {
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
            // Choose two adjacent tiles sharing an edge: face 0, (0,0) and (1,0) at depth 1
            var tileAId = new TileId(0, 0, 0, 1);
            var tileBId = new TileId(0, 1, 0, 1);

            var dataA = new TileData { id = tileAId, resolution = config.baseResolution };
            var dataB = new TileData { id = tileBId, resolution = config.baseResolution };

            float rawMin = float.MaxValue, rawMax = float.MinValue;
            builder.BuildTileMesh(dataA, ref rawMin, ref rawMax);
            builder.BuildTileMesh(dataB, ref rawMin, ref rawMax);

            Assert.IsNotNull(dataA.mesh);
            Assert.IsNotNull(dataB.mesh);

            // For each vertex in mesh A, find if it lies on the edge area (u or v at grid seam)
            // Simpler: compute registry edge sample positions and for each sample locate nearest
            // vertex in both meshes and assert they match.
            var registry = new TerrainTileRegistry(1, config.baseRadius, Vector3.zero);
            registry.tiles.TryGetValue(tileAId, out var entryA);
            registry.tiles.TryGetValue(tileBId, out var entryB);

            // We'll sample the shared edge by walking the global integer segment between corner positions
            int resMinusOne = Mathf.Max(1, dataA.resolution - 1);
            int globalPerEdge = entryA.tilesPerEdge * resMinusOne;

            // Shared edge is along the boundary between tileA and tileB: iterate global coords where globalI == resMinusOne (tile boundary)
            for (int gJ = 0; gJ <= globalPerEdge; gJ++)
            {
                int globalI = resMinusOne; // seam x
                float u = globalI / (float)globalPerEdge;
                float v = gJ / (float)globalPerEdge;
                // Handle reflection into canonical triangle
                if (u + v > 1f)
                {
                    u = 1f - u; v = 1f - v;
                }

                Vector3 dir = IcosphereMapping.BarycentricToWorldDirection(entryA.face, u, v).normalized;
                // sample radius using a provider to get world position approximation
                var localProvider = (SimplePerlinHeightProvider)(config.heightProvider ?? new SimplePerlinHeightProvider());
                float sampleHeight = (config.heightScale) * localProvider.Sample(in dir, dataA.resolution);
                Vector3 sampleWorld = dir * (config.baseRadius + sampleHeight);

                // find nearest vertex in both meshes
                Vector3[] vertsA = dataA.mesh.vertices;
                Vector3[] vertsB = dataB.mesh.vertices;

                float bestA = float.MaxValue; Vector3 bestApos = Vector3.zero;
                foreach (var vtx in vertsA)
                {
                    var w = vtx + dataA.center;
                    float d = (w - sampleWorld).sqrMagnitude;
                    if (d < bestA) { bestA = d; bestApos = w; }
                }
                float bestB = float.MaxValue; Vector3 bestBpos = Vector3.zero;
                foreach (var vtx in vertsB)
                {
                    var w = vtx + dataB.center;
                    float d = (w - sampleWorld).sqrMagnitude;
                    if (d < bestB) { bestB = d; bestBpos = w; }
                }

                float eps = 1e-4f;
                // Diagnostic: print sample info and nearest vertices so we can inspect mismatches
                UnityEngine.Debug.Log($"[EdgeDiag] gJ={gJ} u={u:0.000} v={v:0.000} sampleWorld={sampleWorld} bestA={bestApos} dA={bestA:0.000000} bestB={bestBpos} dB={bestB:0.000000}");
                Assert.AreEqual(bestApos.x, bestBpos.x, eps, "Edge vertex X must match");
                Assert.AreEqual(bestApos.y, bestBpos.y, eps, "Edge vertex Y must match");
                Assert.AreEqual(bestApos.z, bestBpos.z, eps, "Edge vertex Z must match");
            }
        }
    }
}
