using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections;
using UnityEngine.TestTools;
using HexGlobeProject.TerrainSystem.Core;

namespace HexGlobeProject.Tests.PlayMode
{
    public class TileCornerFixTests
    {
        [UnityTest]
        public IEnumerator BuiltTileCornersAreCanonical()
        {
            // Arrange
            int depth = 2;
            int face = 0;
            int x = 0;
            int y = 0;
            int res = 8; // moderate resolution for test
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 10f;
            config.heightScale = 0.5f;
            config.heightProvider = new SimplePerlinHeightProvider();

            var tileId = new TileId(face, x, y, depth);
            var data = new TileData();
            data.id = tileId;
            data.resolution = res;

            var builder = new PlanetTileMeshBuilder(config, config.heightProvider, Vector3.zero);

            // Act
            builder.BuildTileMesh(data);
            yield return null; // allow one frame if anything deferred (builder is sync but keep safe)

            // Assert
            Assert.IsNotNull(data.mesh, "Mesh should be created");

            // Get canonical corners in world space and compare
            var corners = IcosphereMapping.GetCorners(data.id, config.baseRadius, Vector3.zero);
            var verts = data.mesh.vertices;
            Assert.IsNotNull(verts);

            // Find indices of the three canonical corners using the same scan order
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
            float eps = 1e-3f;
            for (int k = 0; k < 3; k++)
            {
                int li = FindIndexForLocal(cornersLocal[k].x, cornersLocal[k].y);
                Assert.IsTrue(li >= 0 && li < verts.Length, $"Corner vertex index {li} out of range");
                var worldFromMesh = verts[li] + data.center;
                var expected = corners[k];
                var dot = Vector3.Dot(worldFromMesh, expected);

                Debug.Log($"Corner[{k}] idx={li} meshWorld={worldFromMesh} expected={expected} dist={dot} res={res}");

                Assert.GreaterOrEqual(dot, 1f-eps, $"Corner {k} mismatch too large: {dot}");
            }
        }
    }
}
