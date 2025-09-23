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
        private SimplePerlinHeightProvider provider;

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
            // Enable verbose per-vertex diagnostics to capture bary->global->dir->world traces
            PlanetTileMeshBuilder.VerboseEdgeSampleLogging = true;
            config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 30f;
            config.baseResolution = 8;
            config.heightScale = 1f;
            config.recalcNormals = false;
            provider = new SimplePerlinHeightProvider();
            builder = new PlanetTileMeshBuilder(config, provider, Vector3.zero);
        }

        [TearDown]
        public void TearDown()
        {
            PlanetTileMeshBuilder.VerboseEdgeSampleLogging = false;
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
            foreach (var c1 in cornersA)
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

            // Derive edge vertex indices from mesh UVs (barycentric coordinates). Vertex ordering
            // may vary but the per-vertex barycentric coords are stored in mesh.uv in the same
            // order as mesh.vertices.

            // Ensure we found two matching corners which define the shared edge.
            if (matchingCorners[0] == Vector3.zero || matchingCorners[1] == Vector3.zero)
            {
                Assert.Fail("Failed to find two shared corners between adjacent tiles; cannot verify edge continuity.");
            }

            // Validate each tile's edge vertices independently using robust lattice index mapping
            float worldTol = 1e-3f;

            // local helper: convert uv -> tile-local lattice indices (i,j) in [0,res-1]
            // Detect whether uv is global barycentric (face-wide) or tile-local normalized [0,1].
            (int i, int j) LocalLatticeIndexFromUV(Vector2 uv, TileId tid, int res)
            {
                var origin = IcosphereMapping.TileIndexToBaryOrigin(tid.depth, tid.x, tid.y);
                int tilesPerEdge = 1 << tid.depth;
                float tileSpan = 1f / tilesPerEdge;
                const float eps = 1e-5f;
                bool looksLocal = (uv.x >= -eps && uv.x <= 1f + eps && uv.y >= -eps && uv.y <= 1f + eps);
                float fi, fj;
                if (looksLocal)
                {
                    // uv are local normalized bary (i/(res-1), j/(res-1))
                    fi = uv.x * (res - 1);
                    fj = uv.y * (res - 1);
                }
                else
                {
                    // uv are global barycentric: convert to local normalized within this tile
                    float localU = (uv.x - origin.U) / tileSpan;
                    float localV = (uv.y - origin.V) / tileSpan;
                    fi = localU * (res - 1);
                    fj = localV * (res - 1);
                }
                int ii = Mathf.Clamp(Mathf.RoundToInt(fi), 0, res - 1);
                int jj = Mathf.Clamp(Mathf.RoundToInt(fj), 0, res - 1);
                return (ii, jj);
            }

            // Determine which edge (0: u==0, 1: v==0, 2: u+v==1) of tileA is the shared edge
            Vector3[] tileACorners = IcosphereMapping.GetCorners(dataA.id, config.baseRadius);
            int aCornerIndex0 = -1, aCornerIndex1 = -1;
            for (int k = 0; k < 3; k++)
            {
                if (Vector3.Distance(tileACorners[k], matchingCorners[0]) < 1e-3f) aCornerIndex0 = k;
                if (Vector3.Distance(tileACorners[k], matchingCorners[1]) < 1e-3f) aCornerIndex1 = k;
            }
            Assert.IsTrue(aCornerIndex0 >= 0 && aCornerIndex1 >= 0, "Failed to map shared corners to tile A corners");
            int aEdgeKind = -1;
            // corners: 0=(0,0), 1=(1,0), 2=(0,1)
            if ((aCornerIndex0 == 0 && aCornerIndex1 == 1) || (aCornerIndex0 == 1 && aCornerIndex1 == 0)) aEdgeKind = 1; // v==0
            else if ((aCornerIndex0 == 0 && aCornerIndex1 == 2) || (aCornerIndex0 == 2 && aCornerIndex1 == 0)) aEdgeKind = 0; // u==0
            else aEdgeKind = 2; // u+v==1

            int aEdgeCount = 0;
            for (int vi = 0; vi < dataA.mesh.vertexCount; vi++)
            {
                Vector2 uv = dataA.mesh.uv[vi];
                var (ii, jj) = LocalLatticeIndexFromUV(uv, dataA.id, dataA.resolution);
                bool onEdge;
                if (aEdgeKind == 0) onEdge = (ii == 0);
                else if (aEdgeKind == 1) onEdge = (jj == 0);
                else onEdge = (ii + jj == dataA.resolution - 1);
                if (!onEdge) continue;
                aEdgeCount++;
                var dir = IcosphereMapping.BaryToWorldDirection(dataA.id.face, new Barycentric(uv.x, uv.y)).normalized;
                float raw = provider.Sample(in dir, dataA.resolution);
                float rawScaled = raw * config.heightScale;
                Vector3 expectedWorld = dir * (config.baseRadius + rawScaled);
                Vector3 actualWorld = dataA.center + dataA.mesh.vertices[vi];
                float d = Vector3.Distance(expectedWorld, actualWorld);
                if (d > worldTol) Debug.LogError($"Tile A edge vertex mismatch idx={vi} lattice=({ii},{jj}) expected={expectedWorld} actual={actualWorld} d={d}");
                Assert.LessOrEqual(d, worldTol, $"Tile A edge vertex world position mismatch at idx={vi} lattice=({ii},{jj}): d={d}");
            }
            Assert.AreEqual(config.baseResolution, aEdgeCount, $"Tile A edge vertex count mismatch: expected {config.baseResolution}, got {aEdgeCount}");

            // Determine shared edge for tileB
            Vector3[] tileBCorners = IcosphereMapping.GetCorners(dataB.id, config.baseRadius);
            int bCornerIndex0 = -1, bCornerIndex1 = -1;
            for (int k = 0; k < 3; k++)
            {
                if (Vector3.Distance(tileBCorners[k], matchingCorners[0]) < 1e-3f) bCornerIndex0 = k;
                if (Vector3.Distance(tileBCorners[k], matchingCorners[1]) < 1e-3f) bCornerIndex1 = k;
            }
            Assert.IsTrue(bCornerIndex0 >= 0 && bCornerIndex1 >= 0, "Failed to map shared corners to tile B corners");
            int bEdgeKind = -1;
            if ((bCornerIndex0 == 0 && bCornerIndex1 == 1) || (bCornerIndex0 == 1 && bCornerIndex1 == 0)) bEdgeKind = 1; // v==0
            else if ((bCornerIndex0 == 0 && bCornerIndex1 == 2) || (bCornerIndex0 == 2 && bCornerIndex1 == 0)) bEdgeKind = 0; // u==0
            else bEdgeKind = 2; // u+v==1

            int bEdgeCount = 0;
            for (int vi = 0; vi < dataB.mesh.vertexCount; vi++)
            {
                Vector2 uv = dataB.mesh.uv[vi];
                var (ii, jj) = LocalLatticeIndexFromUV(uv, dataB.id, dataB.resolution);
                bool onEdge;
                if (bEdgeKind == 0) onEdge = (ii == 0);
                else if (bEdgeKind == 1) onEdge = (jj == 0);
                else onEdge = (ii + jj == dataB.resolution - 1);
                if (!onEdge) continue;
                bEdgeCount++;
                var dir = IcosphereMapping.BaryToWorldDirection(dataB.id.face, new Barycentric(uv.x, uv.y)).normalized;
                float raw = provider.Sample(in dir, dataB.resolution);
                float rawScaled = raw * config.heightScale;
                Vector3 expectedWorld = dir * (config.baseRadius + rawScaled);
                Vector3 actualWorld = dataB.center + dataB.mesh.vertices[vi];
                float d = Vector3.Distance(expectedWorld, actualWorld);
                if (d > worldTol) Debug.LogError($"Tile B edge vertex mismatch idx={vi} lattice=({ii},{jj}) expected={expectedWorld} actual={actualWorld} d={d}");
                Assert.LessOrEqual(d, worldTol, $"Tile B edge vertex world position mismatch at idx={vi} lattice=({ii},{jj}): d={d}");
            }
            Assert.AreEqual(config.baseResolution, bEdgeCount, $"Tile B edge vertex count mismatch: expected {config.baseResolution}, got {bEdgeCount}");

        }
    }
}
