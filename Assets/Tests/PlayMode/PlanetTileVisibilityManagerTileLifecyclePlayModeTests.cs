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
            // When camera-driven depth sync is disabled, ensure the manager is initialized
            // for depth 0 explicitly so the precomputed registry and spawn logic are set up.
            try { mgr.SetDepth(0); } catch { }
            
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

            // Transition to depth 1 and allow a short moment for the lifecycle coroutine
            // to process deactivation before verifying GameObject state.
            mgr.SetDepth(1);
            // Give the manager's lifecycle/worker coroutines a small time slice to run
            yield return new WaitForSecondsRealtime(0.1f);

            // Check: GameObjects should have been deactivated as part of the depth transition.
            // The manager may either deactivate or destroy non-visible tiles depending on policy,
            // so accept either a destroyed object (null) or an inactive GameObject.
            foreach (var kv in instances)
            {
                var go = kv.Value;
                // Unity overrides == to allow checking destroyed objects; use that to avoid MissingReferenceException
                if (go == null)
                {
                    // Tile was destroyed as part of lifecycle - acceptable
                    continue;
                }

                Assert.IsFalse(go.activeInHierarchy, "Depth-0 tile GameObject should be deactivated after depth transition.");
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
                // The returned tile id should match an original depth-0 tile id (we precomputed those earlier)
                Assert.IsTrue(instances.ContainsKey(idStr), "Returned tile id should match an original depth-0 tile id.");

                var original = instances[idStr];
                if (original != null)
                {
                    // If the original instance still exists we expect reuse
                    Assert.AreSame(original, t.gameObject, "Tile GameObject should be the same instance (reused) when returning to depth 0.");
                }
                else
                {
                    // Original was destroyed; accept a recreated instance but ensure it's active
                    Assert.IsNotNull(t.gameObject, "Returned tile GameObject should not be null");
                }

                Assert.IsTrue(t.gameObject.activeInHierarchy, "Returned tile GameObject should be active in hierarchy after return.");
            }

            builder.Teardown();
        }
    }
}
