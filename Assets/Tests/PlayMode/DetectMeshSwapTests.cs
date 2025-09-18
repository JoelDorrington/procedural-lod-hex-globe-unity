using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.PlayMode
{
    public class DetectMeshSwapTests
    {
        [UnityTest]
        public IEnumerator ActiveTiles_DoNotLoseOrSwapMesh_AfterSpawn()
        {
            var managerGO = new GameObject("PTVM_Detect_Test");
            var manager = managerGO.AddComponent<PlanetTileVisibilityManager>();

            var planetGO = new GameObject("Planet");
            planetGO.transform.position = Vector3.zero;
            manager.GetType().GetField("planetTransform", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, planetGO.transform);

            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 1f;
            config.baseResolution = 8;
            manager.config = config;

            // Lower spawn budget to force staggered builds but not so low that tests take forever
            var maxSpawnsField = typeof(PlanetTileVisibilityManager).GetField("maxSpawnsPerFrame", BindingFlags.NonPublic | BindingFlags.Instance);
            if (maxSpawnsField != null) maxSpawnsField.SetValue(manager, 2);

            var camGO = new GameObject("TestCam");
            var camController = camGO.AddComponent<CameraController>();
            camController.target = planetGO.transform;
            camController.distance = 30f;
            manager.GameCamera = camController;

            manager.SetDepth(3);

            // Wait 0.2s for initial spawns
            yield return new WaitForSecondsRealtime(0.2f);

            var active = manager.GetActiveTiles();
            Assert.IsNotNull(active, "Active tile list should be available");
            Assert.IsNotEmpty(active, "Expected some active tiles after initial spawn pass");

            // Record initial mesh references
            var initialMeshes = new Dictionary<PlanetTerrainTile, Mesh>();
            foreach (var t in active)
            {
                Mesh m = null;
                if (t.meshFilter != null) m = t.meshFilter.sharedMesh;
                initialMeshes[t] = m;
            }

            // Monitor over 2 seconds for any tile losing or swapping mesh
            float watchTime = 2f;
            float elapsed = 0f;
            float sampleInterval = 0.1f;
            while (elapsed < watchTime)
            {
                yield return new WaitForSecondsRealtime(sampleInterval);
                elapsed += sampleInterval;

                var nowActive = manager.GetActiveTiles();
                foreach (var t in nowActive)
                {
                    Mesh before = initialMeshes.ContainsKey(t) ? initialMeshes[t] : null;
                    Mesh now = t.meshFilter != null ? t.meshFilter.sharedMesh : null;
                    if (before == null && now == null) continue; // both null - ignore
                    if (before == null && now != null) {
                        // initial had no mesh but now has one - that's okay
                        initialMeshes[t] = now; continue;
                    }
                    if (before != now)
                    {
                        Assert.Fail($"Tile {t.gameObject.name} changed mesh during watch window: before={before?.name ?? "<null>"} after={now?.name ?? "<null>"}");
                    }
                }
            }

            // Cleanup
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(managerGO);
            Object.DestroyImmediate(planetGO);
            Object.DestroyImmediate(camGO);

            Assert.Pass("No mesh swaps or nulling observed during watch window");
        }
    }
}
