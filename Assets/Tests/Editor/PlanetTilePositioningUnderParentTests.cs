using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;
using UnityEngine.TestTools;
using System.Collections;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTilePositioningUnderParentTests
    {
        [UnityTest]
        public IEnumerator SpawnedTile_MatchesPrecomputedCenter_WhenUnderNonIdentityParent()
        {
            // Create a scene parent with rotation and non-uniform scale to mimic Play-mode hierarchy
            var sceneParent = new GameObject("SceneParent");
            sceneParent.transform.position = new Vector3(1.0f, 2.0f, -3.0f);
            sceneParent.transform.rotation = Quaternion.Euler(10f, 33f, 5f);
            sceneParent.transform.localScale = new Vector3(1.5f, 2.0f, 0.8f);

            // Manager under the scene parent (non-identity ancestors)
            var mgrGO = new GameObject("mgr_under_parent");
            mgrGO.transform.SetParent(sceneParent.transform, worldPositionStays: true);
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();

            // Planet also under the scene parent, positioned non-zero relative to parent
            var planet = new GameObject("PlanetUnderParent");
            planet.transform.SetParent(sceneParent.transform, worldPositionStays: true);
            planet.transform.localPosition = new Vector3(0.3f, 0.4f, 0.5f);

            // Assign runtime config
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 10f;
            cfg.baseResolution = 8;
            cfg.heightScale = 1f;
            var cfgField = typeof(PlanetTileVisibilityManager).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            cfgField.SetValue(mgr, cfg);

            var ptField = typeof(PlanetTileVisibilityManager).GetField("planetTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ptField.SetValue(mgr, planet.transform);

            // Act: precompute and spawn
            mgr.SetDepth(0);
            yield return null;

            var id = new TileId(0, 0, 0, 0);
            var spawned = mgr.TrySpawnTile(id, resolution: 8);
            Assert.IsNotNull(spawned, "Expected spawned tile even under non-identity parent");

            bool found = PlanetTileVisibilityManager.GetPrecomputedIndex(id, out int idx, out var entry);
            Assert.IsTrue(found, "Precomputed registry expected to contain entry");

            // Compute mesh world centroid
            var tileComp = spawned.GetComponent<PlanetTerrainTile>();
            Assert.IsNotNull(tileComp.meshFilter, "Expected meshFilter on spawned tile");
            var mesh = tileComp.meshFilter.sharedMesh;
            Assert.IsNotNull(mesh, "Expected mesh on spawned tile");

            Vector3 worldCentroid = Vector3.zero;
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++) worldCentroid += spawned.transform.TransformPoint(verts[i]);
            worldCentroid /= verts.Length;

            // The precomputed entry centerWorld is in world-space; ensure centroid matches it
            float dist = Vector3.Distance(worldCentroid, entry.centerWorld);
            Assert.Less(dist, 0.01f, $"Mesh world-centroid should equal precomputed centerWorld even with parent transforms (dist={dist})");

            // Cleanup
            Object.DestroyImmediate(sceneParent);
            Object.DestroyImmediate(mgrGO);
            Object.DestroyImmediate(planet);
            ScriptableObject.DestroyImmediate(cfg);
        }
    }
}
