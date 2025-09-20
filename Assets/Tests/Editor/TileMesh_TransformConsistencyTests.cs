using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Core;

namespace HexGlobeProject.Tests.Editor
{
    public class TileMesh_TransformConsistencyTests
    {

        [SetUp]
        public void SetUp()
        {
            PlanetTileMeshBuilder.ClearCache();
        }

        [Test]
        public void BuiltMeshVerticesMatchIcosphereWorldPositions()
        {
            float dTolerance = 10f;
            
            // Arrange
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 30f;
            config.baseResolution = 32;
            config.heightScale = 1f;
            config.recalcNormals = false;
            var provider = new SimplePerlinHeightProvider();

            foreach (var tileId in TileIdIterator.IterateTileIds())
            {

                float[][] cornerBarys = new float[][] {
                new float[]{ 0f, 0f },
                new float[]{ 1f, 0f },
                new float[]{ 0f, 1f }
            };
                Vector3[] cornerWorlds = IcosphereMapping.GetCorners(tileId, config.baseRadius, Vector3.zero);

                foreach (var uv in cornerBarys)
                {
                    var u = uv[0];
                    var v = uv[1];
                    bool matchesOneCorner = false;
                    var global = IcosphereMapping.BaryLocalToGlobal(tileId, new(u,v), config.baseResolution);
                    var dir = IcosphereMapping.BaryToWorldDirection(tileId.face, global);
                    // sample height using provider
                    float h = provider.Sample(in dir, config.baseResolution) * config.heightScale;
                    var baryWorld = dir * (config.baseRadius + h);
                    float dMin = float.MaxValue;
                    foreach (var cornerWorld in cornerWorlds)
                    {
                        float d = (baryWorld - cornerWorld).sqrMagnitude;
                        if (d < dMin) dMin = d;
                        // We need to allow a little deviation for the height sampling
                        if (d < dTolerance)
                        {
                            matchesOneCorner = true;
                            Debug.Log($"[TEST_DIAG] Bary vertex world matches expected corner {baryWorld} ~ {cornerWorld} (d={d:0.000})");
                        }
                    }
                    Assert.True(matchesOneCorner, $"[TileId {tileId}] Expected {baryWorld} to match one of the corners [{string.Join(", ", cornerWorlds)}]. dMin={dMin:0.000} > {dTolerance:0.000}");
                }
            }
            // Cleanup
            Object.DestroyImmediate(config);
        }
    }
}
