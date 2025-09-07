using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;
using UnityEngine.TestTools;
using System.Collections;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileColliderVisualAlignmentTests
    {
        [UnityTest]
        public IEnumerator Tile_ColliderAndVisualMesh_AreAligned()
        {
            var mgrGO = new GameObject("mgr_align_test");
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();

            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 10f;
            cfg.baseResolution = 8;
            cfg.heightScale = 1f;
            var cfgField = typeof(PlanetTileVisibilityManager).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            cfgField.SetValue(mgr, cfg);

            var planet = new GameObject("PlanetAlign");
            var ptField = typeof(PlanetTileVisibilityManager).GetField("planetTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ptField.SetValue(mgr, planet.transform);

            mgr.SetDepth(0);
            yield return null;

            var id = new TileId(0, 0, 0, 0);
            var spawned = mgr.TrySpawnTile(id, resolution: 8);
            Assert.IsNotNull(spawned);

            var tile = spawned.GetComponent<PlanetTerrainTile>();
            Assert.IsNotNull(tile.meshFilter);
            Assert.IsNotNull(tile.meshCollider);
            var vis = tile.meshFilter.sharedMesh;
            var col = tile.meshCollider.sharedMesh;
            Assert.IsNotNull(vis, "Visual mesh required");
            Assert.IsNotNull(col, "Collider mesh required");

            // Compare centroids
            Vector3 visCentroid = Vector3.zero; foreach (var v in vis.vertices) visCentroid += spawned.transform.TransformPoint(v); visCentroid /= vis.vertexCount;
            Vector3 colCentroid = Vector3.zero; foreach (var v in col.vertices) colCentroid += spawned.transform.TransformPoint(v); colCentroid /= col.vertexCount;

            float centroidDist = Vector3.Distance(visCentroid, colCentroid);
            Assert.Less(centroidDist, 0.01f, $"Visual/collider centroids differ (dist={centroidDist})");

            // Compare a few corresponding transformed vertices where indices match (if possible)
            int sampleCount = Mathf.Min(vis.vertexCount, col.vertexCount, 5);
            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 vVis = spawned.transform.TransformPoint(vis.vertices[i]);
                Vector3 vCol = spawned.transform.TransformPoint(col.vertices[i]);
                Assert.Less(Vector3.Distance(vVis, vCol), 0.01f, $"Vertex {i} mismatch between visual and collider mesh");
            }

            Object.DestroyImmediate(mgrGO);
            Object.DestroyImmediate(planet);
            ScriptableObject.DestroyImmediate(cfg);
        }
    }
}
