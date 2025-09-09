using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.PlayMode
{
    public class DepthMappingPlayModeTests
    {
        [UnityTest]
        public IEnumerator Depth_Increases_WhenCameraApproaches()
        {
            var builder = new PlaymodeTestSceneBuilder();
            builder.Build();

            // Let one frame run to initialize
            yield return null;

            var mgr = builder.Manager;
            var camCtrl = builder.CameraController;

            // Use reflection to read the manager's last precomputed depth (observable state in tests)
            var fld = typeof(PlanetTileVisibilityManager).GetField("_lastPrecomputedDepth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(fld, "Expected private field '_lastPrecomputedDepth' to exist for test introspection.");

            // Move camera to far (max) and allow updates
            camCtrl.distance = camCtrl.maxDistance;
            for (int i = 0; i < 8; i++) yield return null;

            int depthAtFar = (int)fld.GetValue(mgr);
            // Contract: at maximum camera distance we observe depth == 0
            Assert.AreEqual(0, depthAtFar, $"Expected depth 0 when camera is at max distance, but got {depthAtFar}");

            // Now move camera near and expect depth to increase
            camCtrl.distance = camCtrl.minDistance;
            for (int i = 0; i < 8; i++) yield return null;

            int depthAtNear = (int)fld.GetValue(mgr);
            Assert.Greater(depthAtNear, depthAtFar, $"Expected depth to increase when camera approaches (near={depthAtNear} <= far={depthAtFar})");

            builder.Teardown();
        }
    }
}
