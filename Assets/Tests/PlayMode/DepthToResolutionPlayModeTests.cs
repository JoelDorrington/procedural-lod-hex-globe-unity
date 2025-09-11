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

            // Disable automatic camera-driven depth syncing so the test can control depth deterministically
            var debugField = mgr.GetType().GetField("debugDisableCameraDepthSync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (debugField != null) debugField.SetValue(mgr, true);

            // Use reflection to read the manager's last precomputed depth (observable state in tests)
            var fld = typeof(PlanetTileVisibilityManager).GetField("_currentDepth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(fld, "Expected private field '_currentDepth' to exist for test introspection.");

            // Move camera to far (max) and explicitly set depth based on camera computation
            camCtrl.distance = camCtrl.maxDistance;
            yield return null; // Let the camera controller update
            
            // Manually compute and set the depth that should result from this camera position
            var computeMethod = typeof(PlanetTileVisibilityManager).GetMethod("ComputeDepthFromCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(computeMethod, "Expected ComputeDepthFromCamera method for test");
            int expectedDepthAtFar = (int)computeMethod.Invoke(mgr, null);
            mgr.SetDepth(expectedDepthAtFar);
            
            int depthAtFar = (int)fld.GetValue(mgr);
            // Contract: at maximum camera distance we observe depth == 0
            Assert.AreEqual(0, depthAtFar, $"Expected depth 0 when camera is at max distance, but got {depthAtFar}");

            // Now move camera near and expect depth to increase
            camCtrl.distance = camCtrl.minDistance;
            yield return null; // Let the camera controller update
            
            // Manually compute and set the depth that should result from this camera position
            int expectedDepthAtNear = (int)computeMethod.Invoke(mgr, null);
            mgr.SetDepth(expectedDepthAtNear);
            
            int depthAtNear = (int)fld.GetValue(mgr);
            Assert.Greater(depthAtNear, depthAtFar, $"Expected depth to increase when camera approaches (near={depthAtNear} <= far={depthAtFar})");

            builder.Teardown();
        }
    }
}
