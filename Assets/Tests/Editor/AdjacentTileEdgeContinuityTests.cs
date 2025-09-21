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
            foreach (int aIndex in res8EdgeIndices)
            {
                Vector3 aVert = dataA.mesh.vertices[aIndex];
                foreach (int bIndex in res8EdgeIndices)
                {
                    Vector3 bVert = dataB.mesh.vertices[bIndex];
                    if (aVert.normalized == bVert.normalized)
                    {
                        matchedVerts++;
                        Debug.Log($"Matching vert: {aVert.normalized} == {bVert.normalized}");
                    }
                    else
                    {
                        Debug.Log($"No match vert: {aVert.normalized} != {bVert.normalized}");
                    }
                }
            }

            Assert.AreEqual(matchedVerts, config.baseResolution, $"Expected {config.baseResolution} matching edge vertices, found {matchedVerts}");
        }
    }
}
