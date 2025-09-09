using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using HexGlobeProject.Tests.PlayMode;

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

            // Start at depth 0
            mgr.SetDepth(0);
            // Wait for spawn
            for (int i = 0; i < 4; i++) yield return null;

            var active0 = mgr.GetActiveTiles();
            Assert.AreEqual(20, active0.Count, "Depth 0 should spawn 20 tiles initially.");

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
            // logic (heuristic/raycast) may turn visuals back on later.
            foreach (var kv in instances)
            {
                var go = kv.Value;
                Assert.IsNotNull(go, "Recorded tile GameObject should still exist.");
                Assert.IsFalse(go.activeInHierarchy, "Depth-0 tile GameObject should be deactivated immediately after depth transition.");
            }

            // Transition back to depth 0
            mgr.SetDepth(0);
            for (int i = 0; i < 6; i++) yield return null;

            // Ensure the same GameObject instances were re-enabled (not recreated)
            var activeReturn = mgr.GetActiveTiles();
            Assert.AreEqual(20, activeReturn.Count, "Returning to depth 0 should restore 20 tiles.");

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
