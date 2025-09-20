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
            // Simplified diagnostic test: focus on the single problematic global index found in logs
            // earlier: globalI=7, gJ=1 when using res=8 and depth=1.
            int depth = 0;
            var tileAId = new TileId(depth, 0, 0, 1);
            var tileBId = new TileId(depth, 1, 0, 1);

            var dataA = new TileData { id = tileAId, resolution = config.baseResolution };
            var dataB = new TileData { id = tileBId, resolution = config.baseResolution };

            builder.BuildTileMesh(dataA);
            builder.BuildTileMesh(dataB);

            Assert.IsNotNull(dataA.mesh);
            Assert.IsNotNull(dataB.mesh);

            var registry = new TerrainTileRegistry(1, config.baseRadius, Vector3.zero);
            registry.tiles.TryGetValue(tileAId, out var entryA);
            registry.tiles.TryGetValue(tileBId, out var entryB);

            int tilesPerEdge = 1 << depth;
            int resMinusOne = Mathf.Max(1, dataA.resolution - 1);
            int globalPerEdge = tilesPerEdge * resMinusOne;

            int targetGlobalI = resMinusOne; // 7
            int targetGJ = 1;
            float u = targetGlobalI / (float)globalPerEdge;
            float v = targetGJ / (float)globalPerEdge;
            if (u + v > 1f) { u = 1f - v; v = 1f - u; }

            Vector3 dir = IcosphereMapping.BaryToWorldDirection(entryA.face, new(u,v)).normalized;
            var localProvider = (SimplePerlinHeightProvider)(config.heightProvider ?? new SimplePerlinHeightProvider());
            float sampleHeight = config.heightScale * localProvider.Sample(in dir, dataA.resolution);
            Vector3 sampleWorld = dir * (config.baseRadius + sampleHeight);

            // Find nearest mesh vertex in each tile
            Vector3 bestApos = Vector3.zero; float bestAd = float.MaxValue;
            foreach (var vtx in dataA.mesh.vertices)
            {
                var w = vtx + dataA.center;
                float d = (w - sampleWorld).sqrMagnitude;
                if (d < bestAd) { bestAd = d; bestApos = w; }
            }
            Vector3 bestBpos = Vector3.zero; float bestBd = float.MaxValue;
            foreach (var vtx in dataB.mesh.vertices)
            {
                var w = vtx + dataB.center;
                float d = (w - sampleWorld).sqrMagnitude;
                if (d < bestBd) { bestBd = d; bestBpos = w; }
            }

            int subdivisionsPerTileEdge = Mathf.Max(1, dataA.resolution - 1);

            // For tileA (x=0), compute local index iA = globalI - tileA.x*subdivisionsPerTileEdge
            int localIA = targetGlobalI - tileAId.x * subdivisionsPerTileEdge;
            int localJA = targetGJ - tileAId.y * subdivisionsPerTileEdge;

            // For tileB (x=1)
            int localIB = targetGlobalI - tileBId.x * subdivisionsPerTileEdge;
            int localJB = targetGJ - tileBId.y * subdivisionsPerTileEdge;

            // Determine whether these local indices are inside the canonical triangle (i+j <= res-1)
            bool tileA_hasLocal = localIA >= 0 && localJA >= 0 && localIA + localJA <= dataA.resolution - 1;
            bool tileB_hasLocal = localIB >= 0 && localJB >= 0 && localIB + localJB <= dataB.resolution - 1;

            // Assert expectations we observed in logs: tileB should have the local index; tileA likely doesn't
            Assert.IsTrue(tileB_hasLocal, "Tile B should contain the local index corresponding to the global seam sample");
            Assert.IsFalse(tileA_hasLocal, "Tile A should NOT contain that local index (this explains the mismatch)");

            // Next causal requirement: if a tile 'owns' the local index then the mesh
            // must contain the EXACT sampled world vertex at that lattice coordinate.
            // Conversely, if it does not own that local index the mesh must not contain it.
            float exactTolSqr = 1e-8f; // ~1e-4 units tolerance squared
            Vector3 exactSampleWorld = dir * (config.baseRadius + (config.heightScale * localProvider.Sample(in dir, dataA.resolution)));

            bool foundInA = false;
            foreach (var vtx in dataA.mesh.vertices)
            {
                if ((vtx + dataA.center - exactSampleWorld).sqrMagnitude <= exactTolSqr) { foundInA = true; break; }
            }
            bool foundInB = false;
            foreach (var vtx in dataB.mesh.vertices)
            {
                if ((vtx + dataB.center - exactSampleWorld).sqrMagnitude <= exactTolSqr) { foundInB = true; break; }
            }

            // Next causal check: builder must use the registry's centerWorld as data.center
            Assert.LessOrEqual((entryA.centerWorld - dataA.center).sqrMagnitude, 1e-6f, "Tile A center must equal registry centerWorld");
            Assert.LessOrEqual((entryB.centerWorld - dataB.center).sqrMagnitude, 1e-6f, "Tile B center must equal registry centerWorld");

            // If the mesh contains the exact sampled vertex, ensure converting that
            // local vertex back to world-space by adding data.center yields the exact sample
            Vector3 matchedLocalA = Vector3.zero; bool matchedLocalASet = false;
            Vector3 matchedLocalB = Vector3.zero; bool matchedLocalBSet = false;
            foreach (var vtx in dataA.mesh.vertices)
            {
                if ((vtx + dataA.center - exactSampleWorld).sqrMagnitude <= 1e-6f) { matchedLocalA = vtx; matchedLocalASet = true; break; }
            }
            foreach (var vtx in dataB.mesh.vertices)
            {
                if ((vtx + dataB.center - exactSampleWorld).sqrMagnitude <= 1e-6f) { matchedLocalB = vtx; matchedLocalBSet = true; break; }
            }

            if (tileA_hasLocal)
            {
                Assert.IsTrue(foundInA, "Tile A declares the local index but did not produce the exact sampled vertex in its mesh");
                Assert.IsTrue(matchedLocalASet, "Tile A should have a mesh vertex that converts to the exact sampled world position");
                Assert.LessOrEqual(((matchedLocalA + dataA.center) - exactSampleWorld).sqrMagnitude, 1e-6f, "Local->world conversion mismatch for Tile A matched vertex");
            }
            else
            {
                Assert.IsFalse(foundInA, "Tile A does not own the local index but unexpectedly contains the exact sampled vertex");
                Assert.IsFalse(matchedLocalASet, "Tile A unexpectedly contains a vertex equal to the sampled world position");
            }

            if (tileB_hasLocal)
            {
                Assert.IsTrue(foundInB, "Tile B declares the local index but did not produce the exact sampled vertex in its mesh");
                Assert.IsTrue(matchedLocalBSet, "Tile B should have a mesh vertex that converts to the exact sampled world position");
                Assert.LessOrEqual(((matchedLocalB + dataB.center) - exactSampleWorld).sqrMagnitude, 1e-6f, "Local->world conversion mismatch for Tile B matched vertex");
            }
            else
            {
                Assert.IsFalse(foundInB, "Tile B does not own the local index but unexpectedly contains the exact sampled vertex");
                Assert.IsFalse(matchedLocalBSet, "Tile B unexpectedly contains a vertex equal to the sampled world position");
            }
        }
    }
}
