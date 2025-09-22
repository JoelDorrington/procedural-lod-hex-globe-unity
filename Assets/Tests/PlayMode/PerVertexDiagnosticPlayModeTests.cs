using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Core;

namespace HexGlobeProject.Tests.PlayMode
{
    public class PerVertexDiagnosticPlayModeTests
    {
        [UnityTest]
        public IEnumerator LogPerVertexMapping_ForSpecificTile()
        {
            var managerGo = new GameObject("PTVM_Diag");
            var manager = managerGo.AddComponent<PlanetTileVisibilityManager>();
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 1f;
            cfg.baseResolution = 8;
            cfg.heightScale = 0.1f;
            manager.config = cfg;

            int depth = 1;
            var id = new TileId(0, 0, 1, depth);
            manager.SetDepth(depth);

            // Spawn target tile
            var go = manager.TrySpawnTile(id);
            yield return null;
            Assert.IsNotNull(go, "Target tile not spawned");
            var tile = go.GetComponent<PlanetTerrainTile>();
            Assert.IsNotNull(tile.tileData.mesh, "Mesh missing");

            var mesh = tile.tileData.mesh;
            int res = tile.tileData.resolution;
            Debug.Log(string.Format("Per-vertex diagnostic for tile {0}: mesh verts={1} res={2}", id, mesh.vertexCount, res));

            int idx = 0;
            foreach (var local in IcosphereMapping.TileVertexBarys(res))
            {
                if (idx >= mesh.vertexCount) break;

                var global = IcosphereMapping.BaryLocalToGlobal(id, local, res);
                var dir = IcosphereMapping.BaryToWorldDirection(id.face, global);
                var provider = cfg.heightProvider ?? new SimplePerlinHeightProvider();
                float raw = provider.Sample(in dir, res);
                float worldRad = cfg.baseRadius + raw * cfg.heightScale;
                var expectedWorld = dir * worldRad + managerGo.transform.position; // manager planet center fallback

                var actualWorld = tile.tileData.center + mesh.vertices[idx];
                var delta = (expectedWorld - actualWorld).magnitude;
                Debug.Log(string.Format("idx={0} local=({1},{2}) global=({3:F6},{4:F6},{5:F6}) expectedWorld={6} actualWorld={7} delta={8:F6}",
                    idx,
                    local.U, local.V,
                    global.U, global.V, global.W,
                    expectedWorld,
                    actualWorld,
                    delta));

                idx++;
            }

            // keep one frame for logs to flush
            yield return null;
        }
    }
}