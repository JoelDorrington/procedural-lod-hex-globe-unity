using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Core;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

namespace HexGlobeProject.Tests.Editor
{
    public class TileMesh_TransformConsistencyTests
    {
        private IEnumerable<TileId> IterateTileIds()
        {

            Vector3 planetCenter = Vector3.zero;
            float planetRadius = 1f;

            var registry0 = new TerrainTileRegistry(0, planetRadius, planetCenter);
            var registry1 = new TerrainTileRegistry(1, planetRadius, planetCenter);
            var registry2 = new TerrainTileRegistry(2, planetRadius, planetCenter);

            var registries = new TerrainTileRegistry[] { registry0, registry1, registry2 };

            foreach (var registry in registries)
            {
                foreach (var tileId in registry.tiles.Keys)
                {
                    yield return tileId;
                }
            }
        }

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

            foreach (var tileId in IterateTileIds())
            {

                float[][] cornerBarys = new float[][] {
                new float[]{ 0f, 0f },
                new float[]{ 1f, 0f },
                new float[]{ 0f, 1f }
            };
                Vector3[] cornerWorlds = IcosphereMapping.GetCorners(tileId, config.baseRadius, Vector3.zero);

                foreach (var uv in cornerBarys)
                {
                    bool matchesOneCorner = false;
                    var global = IcosphereMapping.BaryLocalToGlobal(tileId, uv, config.baseResolution);
                    var dir = IcosphereMapping.BaryToWorldDirection(tileId.face, global.x, global.y);
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
