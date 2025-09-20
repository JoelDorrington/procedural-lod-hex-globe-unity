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
            PlanetTileMeshBuilder.ClearCache();
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
            // Find two actually adjacent tiles from the registry (share a corner)
            int depth = 1;
            var registry = new TerrainTileRegistry(depth, config.baseRadius, Vector3.zero);
            TileId chosenA = default; TileId chosenB = default;
            Vector3? matchedCorner = null;
            float cornerMatchEps = 1e-4f;

            var keys = new System.Collections.Generic.List<TileId>(registry.tiles.Keys);
            for (int a = 0; a < keys.Count && matchedCorner == null; a++)
            {
                for (int b = a + 1; b < keys.Count && matchedCorner == null; b++)
                {
                    var ea = registry.tiles[keys[a]];
                    var eb = registry.tiles[keys[b]];
                    foreach (var ca in ea.cornerWorldPositions)
                    {
                        foreach (var cb in eb.cornerWorldPositions)
                        {
                            if ((ca - cb).sqrMagnitude <= cornerMatchEps * cornerMatchEps)
                            {
                                chosenA = keys[a]; chosenB = keys[b]; matchedCorner = ca; break;
                            }
                        }
                        if (matchedCorner != null) break;
                    }
                }
            }
            Assert.IsTrue(matchedCorner.HasValue, "Could not find two adjacent tiles that share a corner in the registry");

            var dataA = new TileData { id = chosenA, resolution = config.baseResolution };
            var dataB = new TileData { id = chosenB, resolution = config.baseResolution };

            builder.BuildTileMesh(dataA);
            builder.BuildTileMesh(dataB);

            Assert.IsNotNull(dataA.mesh, "Tile A mesh should be generated");
            Assert.IsNotNull(dataB.mesh, "Tile B mesh should be generated");

            // Find the corner vertex in mesh A that lies at one of the tile corners in world-space
            // Convert local vertex back to world by adding data.center
            Vector3[] vertsA = dataA.mesh.vertices;
            Vector3[] vertsB = dataB.mesh.vertices;

            // Find the nearest mesh vertex (in world-space) to the registry corner for each tile,
            // then assert those two world positions are equal within tolerance. This is robust
            // when heights are non-zero (we assert alignment between tiles, not equality to the
            // base-radius registry position).
            float bestA = float.MaxValue; Vector3 bestApos = Vector3.zero; bool anyA = false;
            for (int idx = 0; idx < vertsA.Length; idx++)
            {
                var w = vertsA[idx] + dataA.center;
                float d = (w - matchedCorner.Value).sqrMagnitude;
                if (d < bestA) { bestA = d; bestApos = w; anyA = true; }
            }

            float bestB = float.MaxValue; Vector3 bestBpos = Vector3.zero; bool anyB = false;
            for (int idx = 0; idx < vertsB.Length; idx++)
            {
                var w = vertsB[idx] + dataB.center;
                float d = (w - matchedCorner.Value).sqrMagnitude;
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
