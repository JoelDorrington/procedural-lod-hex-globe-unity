using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Core;

namespace HexGlobeProject.Tests.Editor
{
    public class AdjacentTileCornersTests
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
            // Use a simple provider to keep output deterministic
            var provider = new SimplePerlinHeightProvider();
            builder = new PlanetTileMeshBuilder(config, provider, Vector3.zero);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void SharedCornerVertices_AreIdenticalBetweenAdjacentTiles()
        {
            // Choose two adjacent tiles that share a corner. We'll pick face 0, tiles (0,0) and (1,0)
            // which share the corner at (u=1/tilesPerEdge, v=0) after mirroring rules. Use depth=1.
            var tileAId = new TileId(0, 0, 0, 1);
            var tileBId = new TileId(0, 1, 0, 1);

            var dataA = new TileData { id = tileAId, resolution = config.baseResolution };
            var dataB = new TileData { id = tileBId, resolution = config.baseResolution };

            float rawMin = float.MaxValue, rawMax = float.MinValue;
            builder.BuildTileMesh(dataA, ref rawMin, ref rawMax);
            builder.BuildTileMesh(dataB, ref rawMin, ref rawMax);

            Assert.IsNotNull(dataA.mesh, "Tile A mesh should be generated");
            Assert.IsNotNull(dataB.mesh, "Tile B mesh should be generated");

            // Find the corner vertex in mesh A that lies at one of the tile corners in world-space
            // Convert local vertex back to world by adding data.center
            Vector3[] vertsA = dataA.mesh.vertices;
            Vector3[] vertsB = dataB.mesh.vertices;

            // Extract candidate world positions for corners from registry for tileA
            var registry = new TerrainTileRegistry(1, config.baseRadius, Vector3.zero);
            registry.tiles.TryGetValue(tileAId, out var entryA);
            registry.tiles.TryGetValue(tileBId, out var entryB);

            Assert.IsNotNull(entryA.cornerWorldPositions, "Precomputed registry should provide corner positions");
            Assert.IsNotNull(entryB.cornerWorldPositions, "Precomputed registry should provide corner positions");

            // We'll check the shared corner between these tiles. Determine which corner is shared.
            // Locate the shared corner by checking which corner from tile A appears in tile B's corner list.
            Vector3 sharedA = entryA.cornerWorldPositions[1];
            Vector3? matchedInB = null;
            float eps = 1e-5f;
            foreach (var cb in entryB.cornerWorldPositions)
            {
                if ((cb - sharedA).sqrMagnitude <= eps * eps)
                {
                    matchedInB = cb;
                    break;
                }
            }
            Assert.IsTrue(matchedInB.HasValue, "Registry entries should share a corner position between adjacent tiles");

            // Compare world-space positions (redundant but clearer failure messages)
            Assert.AreEqual(sharedA.x, matchedInB.Value.x, eps, "Registry shared corner X should match");
            Assert.AreEqual(sharedA.y, matchedInB.Value.y, eps, "Registry shared corner Y should match");
            Assert.AreEqual(sharedA.z, matchedInB.Value.z, eps, "Registry shared corner Z should match");

            // Find the nearest mesh vertex (in world-space) to the registry corner for each tile,
            // then assert those two world positions are equal within tolerance. This is robust
            // when heights are non-zero (we assert alignment between tiles, not equality to the
            // base-radius registry position).
            float bestA = float.MaxValue; Vector3 bestApos = Vector3.zero; bool anyA = false;
            for (int idx = 0; idx < vertsA.Length; idx++)
            {
                var w = vertsA[idx] + dataA.center;
                float d = (w - sharedA).sqrMagnitude;
                if (d < bestA) { bestA = d; bestApos = w; anyA = true; }
            }

            float bestB = float.MaxValue; Vector3 bestBpos = Vector3.zero; bool anyB = false;
            for (int idx = 0; idx < vertsB.Length; idx++)
            {
                var w = vertsB[idx] + dataB.center;
                float d = (w - sharedA).sqrMagnitude;
                if (d < bestB) { bestB = d; bestBpos = w; anyB = true; }
            }

            Assert.IsTrue(anyA, "Tile A mesh had no vertices");
            Assert.IsTrue(anyB, "Tile B mesh had no vertices");

            // Now assert the two best-world-positions match each other within tolerance
            float matchEps = 1e-5f;
            Assert.AreEqual(bestApos.x, bestBpos.x, matchEps, "Nearest corner world X should match between tiles");
            Assert.AreEqual(bestApos.y, bestBpos.y, matchEps, "Nearest corner world Y should match between tiles");
            Assert.AreEqual(bestApos.z, bestBpos.z, matchEps, "Nearest corner world Z should match between tiles");
        }
    }
}
