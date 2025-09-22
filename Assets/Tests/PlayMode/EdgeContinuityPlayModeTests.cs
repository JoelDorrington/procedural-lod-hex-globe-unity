using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.PlayMode
{
    public class EdgeContinuityPlayModeTests
    {
        [UnityTest]
        public IEnumerator SpawnTiles_AdjacentEdgesMatchWorldPositions()
        {
            var managerGo = new GameObject("PTVM_EdgeTest");
            var manager = managerGo.AddComponent<PlanetTileVisibilityManager>();
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 1f;
            cfg.baseResolution = 8;
            manager.config = cfg;

            // Ensure depth=1
            int depth = 1;
            manager.SetDepth(depth);

            // Spawn all tiles at depth
            int tilesPerEdge = 1 << depth;
            var spawned = new Dictionary<TileId, PlanetTerrainTile>();
            for (int f = 0; f < 20; f++)
            {
                for (int x = 0; x < tilesPerEdge; x++)
                {
                    for (int y = 0; y < tilesPerEdge; y++)
                    {
                        var id = new TileId(f, x, y, depth);
                        var go = manager.TrySpawnTile(id);
                        yield return null;
                        if (go != null)
                        {
                            var comp = go.GetComponent<PlanetTerrainTile>();
                            spawned[id] = comp;
                        }
                    }
                }
            }

            // Allow a frame for mesh assignment
            yield return null;

            const float tol = 1e-4f;
            var failures = new List<string>();

            foreach (var kv in spawned)
            {
                var id = kv.Key;
                var tile = kv.Value;
                if (tile == null || tile.tileData == null || tile.tileData.mesh == null) continue;
                // Check neighbor to the +x direction when present
                var neighborId = new TileId(id.face, id.x + 1, id.y, id.depth);
                if (spawned.ContainsKey(neighborId))
                {
                    var aMesh = tile.tileData.mesh;
                    var bMesh = spawned[neighborId].tileData.mesh;
                    // For simplicity compare a small sample: convert all a verts and b verts and find nearest matches
                    var aWorld = GetWorldVerts(aMesh, tile.tileData.center);
                    var bWorld = GetWorldVerts(bMesh, spawned[neighborId].tileData.center);
                    // For each vertex in A that lies near the shared border (U close to 1), find matching vertex in B
                    for (int i = 0; i < aMesh.vertexCount; i++)
                    {
                        var uv = aMesh.uv[i];
                        if (uv.x > 0.999f * 0.66f) // heuristic for right-edge in bary UV (since tileSpan < 1)
                        {
                            var aPos = aWorld[i];
                            float best = float.MaxValue;
                            for (int j = 0; j < bWorld.Length; j++)
                            {
                                var d = (aPos - bWorld[j]).magnitude;
                                if (d < best) best = d;
                            }
                            if (best > tol)
                            {
                                failures.Add($"Edge mismatch between {id} and {neighborId}: closestDist={best}");
                            }
                        }
                    }
                }
            }

            if (failures.Count > 0)
            {
                string msg = "Edge continuity failures:\n" + string.Join("\n", failures.ToArray());
                Assert.Fail(msg);
            }

            // Cleanup
            Object.Destroy(managerGo);
            Object.Destroy(cfg);
        }

        private Vector3[] GetWorldVerts(Mesh mesh, Vector3 center)
        {
            var verts = mesh.vertices;
            var outv = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++) outv[i] = center + verts[i];
            return outv;
        }
    }
}
