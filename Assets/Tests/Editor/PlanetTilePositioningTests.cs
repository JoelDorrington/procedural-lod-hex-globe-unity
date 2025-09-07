using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;
using UnityEngine.TestTools;
using System.Collections;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTilePositioningTests
    {
        [UnityTest]
        public IEnumerator SpawnedTile_PositionMatchesPrecomputedCenter()
        {
            // Arrange: manager with runtime config and planet root at origin
            var mgrGO = new GameObject("mgr_pos_test");
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();

            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 10f;
            cfg.baseResolution = 8;
            cfg.heightScale = 1f;
            var cfgField = typeof(PlanetTileVisibilityManager).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            cfgField.SetValue(mgr, cfg);

            var planet = new GameObject("PlanetRoot_PosTest");
            planet.transform.position = Vector3.zero;
            var ptField = typeof(PlanetTileVisibilityManager).GetField("planetTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ptField.SetValue(mgr, planet.transform);

            // Act: precompute depth 0 and allow a frame
            mgr.SetDepth(0);
            yield return null;

            var id = new TileId(0, 0, 0, 0);
            var spawned = mgr.TrySpawnTile(id, resolution: 8);
            Assert.IsNotNull(spawned, "Spawned GameObject should not be null");

            // Retrieve the precomputed entry for comparison
            bool found = PlanetTileVisibilityManager.GetPrecomputedIndex(id, out int idx, out var entry);
            Assert.IsTrue(found, "Expected precomputed registry to contain the tile entry");

            // Assert 1: GameObject transform matches the canonical center
            var expectedCenter = entry.centerWorld;
            var spawnPos = spawned.transform.position;
            float posDist = Vector3.Distance(spawnPos, expectedCenter);
            Assert.Less(posDist, 0.01f, $"Spawn transform.position should match precomputed centerWorld (dist={posDist})");

            // Assert 2: mesh world-space centroid should also match the canonical center
            var tileComp = spawned.GetComponent<PlanetTerrainTile>();
            Assert.IsNotNull(tileComp, "Spawned GameObject should have a PlanetTerrainTile component");
            Assert.IsNotNull(tileComp.meshFilter, "PlanetTerrainTile.meshFilter should exist");
            var mesh = tileComp.meshFilter.sharedMesh;
            Assert.IsNotNull(mesh, "Spawned tile should have a sharedMesh assigned");

            var verts = mesh.vertices;
            Assert.Greater(verts.Length, 0, "Mesh should contain vertices");

            Vector3 worldCentroid = Vector3.zero;
            for (int i = 0; i < verts.Length; i++) worldCentroid += spawned.transform.TransformPoint(verts[i]);
            worldCentroid /= verts.Length;

            float meshCentroidDist = Vector3.Distance(worldCentroid, expectedCenter);
            Assert.Less(meshCentroidDist, 0.01f, $"Mesh world-centroid should match precomputed centerWorld (dist={meshCentroidDist})");

            // Cleanup
            Object.DestroyImmediate(mgrGO);
            Object.DestroyImmediate(planet);
            ScriptableObject.DestroyImmediate(cfg);
        }
    }
}
