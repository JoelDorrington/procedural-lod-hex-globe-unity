using NUnit.Framework;
using System.Collections;
using UnityEngine.TestTools;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.PlayMode
{
    public class PlanetTileVisibilityManagerDepthPlayModeTests
    {
        [UnityTest]
        public IEnumerator CameraDistance_Changes_UpdateManagerDepth()
        {
            var builder = new PlaymodeTestSceneBuilder();
            builder.Build();

            // Let one frame run to initialize
            yield return null;

            var mgr = builder.Manager;
            var camCtrl = builder.CameraController;

            // Read initial precomputed depth
            var fld = typeof(PlanetTileVisibilityManager).GetField("_lastPrecomputedDepth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(fld, "Expected private field '_lastPrecomputedDepth' to exist for test introspection.");

            int initial = (int)fld.GetValue(mgr);

            // Move camera far away -> expect deeper (larger) depth value after update
            camCtrl.distance = camCtrl.maxDistance; // far

            // Wait several frames for Update/heuristic to run and settle
            for (int i = 0; i < 6; i++) yield return null;

            int later = (int)fld.GetValue(mgr);

            // Require a strict increase; test should fail if manager didn't update
            Assert.IsTrue(later > initial, $"Manager did not increase depth after zooming out (initial={initial}, later={later}).");

            // Now zoom in and expect depth to decrease
            camCtrl.distance = camCtrl.minDistance;
            for (int i = 0; i < 6; i++) yield return null;

            int final = (int)fld.GetValue(mgr);
            // Require a strict decrease when zooming in
            Assert.IsTrue(final < later, $"Manager did not decrease depth after zooming in (previous={later}, final={final}).");

            builder.Teardown();
        }
    }
}
