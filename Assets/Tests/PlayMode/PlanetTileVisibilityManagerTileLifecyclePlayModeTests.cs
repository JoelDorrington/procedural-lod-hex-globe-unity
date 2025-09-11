using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexGlobeProject.Tests.PlayMode
{
    public class PlanetTileVisibilityManagerTileLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator Tiles_Are_Disabled_On_Depth_Transition_And_Reenabled_On_Return()
        {
            var builder = new PlaymodeTestSceneBuilder();
            builder.Build();

            var mgr = builder.Manager;

            // Disable automatic camera-driven depth syncing so the test can control depth deterministically
            var debugField = mgr.GetType().GetField("debugDisableCameraDepthSync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (debugField != null) debugField.SetValue(mgr, true);
            
            // Now check if the registry was populated
            var regField = typeof(HexGlobeProject.TerrainSystem.LOD.PlanetTileVisibilityManager).GetField("s_precomputedRegistry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (regField != null)
            {
                var registry = regField.GetValue(null) as System.Collections.IDictionary;
                if (registry != null && registry.Contains(0))
                {
                    var list = registry[0] as System.Collections.IList;
                    if (list != null)
                    {
                        Debug.Log($"Precomputed registry for depth 0 contains {list.Count} entries");
                    }
                    else
                    {
                        Debug.Log("Precomputed registry for depth 0 exists but list is null");
                    }
                }
                else
                {
                    Debug.Log("Precomputed registry for depth 0 is missing or empty");
                }
            }
            
            // Allow more time for the prioritized spawn worker and coroutines to produce tiles
            yield return new WaitForSecondsRealtime(1.0f);

            var active0 = mgr.GetActiveTiles();
            Assert.AreEqual(12, active0.Count, "Depth 0 should spawn 12 tiles eventually");

            // Record GameObject instances for depth 0
            var instances = new System.Collections.Generic.Dictionary<string, GameObject>();
            foreach (var t in active0)
            {
                instances[t.tileData.id.ToString()] = t.gameObject;
            }

            // Transition to depth 1 and immediately verify GameObject deactivation.
            mgr.SetDepth(1);

            // Strict check: GameObjects must be deactivated (SetActive(false)) immediately
            // as part of the depth transition. Do not wait frames because reactivation
            // logic (heuristic) may turn visuals back on later.
            foreach (var kv in instances)
            {
                var go = kv.Value;
                Assert.IsNotNull(go, "Recorded tile GameObject should still exist.");
                Assert.IsFalse(go.activeInHierarchy, "Depth-0 tile GameObject should be deactivated immediately after depth transition.");
            }

            // Transition back to depth 0
            mgr.SetDepth(0);
            // Allow more time for re-enabling/reuse to occur after depth change
            yield return new WaitForSecondsRealtime(1.0f);

            // Ensure the same GameObject instances were re-enabled (not recreated)
            var activeReturn = mgr.GetActiveTiles();
            Assert.AreEqual(12, activeReturn.Count, "Returning to depth 0 should restore 12 tiles.");

            foreach (var t in activeReturn)
            {
                var idStr = t.tileData.id.ToString();
                Assert.IsTrue(instances.ContainsKey(idStr), "Returned tile id should match an original depth-0 tile id.");
                Assert.AreSame(instances[idStr], t.gameObject, "Tile GameObject should be the same instance (reused) when returning to depth 0.");
                Assert.IsTrue(t.gameObject.activeInHierarchy, "Reused tile GameObject should be active in hierarchy after return.");
            }

            builder.Teardown();
        }
    }
}
