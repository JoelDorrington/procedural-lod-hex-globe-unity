using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HexGlobeProject.Tests.PlayMode;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.PlayMode
{
    public class DepthTransitionSpawnPlayModeTests
    {
        private PlaymodeTestSceneBuilder builder;

        [SetUp]
        public void SetUp()
        {
            builder = new PlaymodeTestSceneBuilder();
            builder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            if (builder != null) { builder.Teardown(); builder = null; }
        }

        [UnityTest]
        public IEnumerator SetDepth_Transition_ShouldSpawnTiles()
        {
            var mgr = builder.Manager;

            // Ensure Awake/Start have run
            yield return null;
            yield return null;

            // Start at depth 0
            mgr.SetDepth(0);
            yield return null;

            // Animate (scroll) the camera distance toward the planet over several frames
            var cam = builder.CameraController;
            float start = cam.distance;
            float target = cam.minDistance; // move close to force higher depth
            int scrollFrames = 20;
            for (int i = 0; i < scrollFrames; i++)
            {
                float t = (i + 1f) / scrollFrames;
                cam.distance = Mathf.Lerp(start, target, t);
                yield return null; // allow manager.Update() to run each frame
            }

            // Wait a few extra frames for spawn worker to process queued spawns
            for (int i = 0; i < 5; i++) yield return null;

            var finalActive = mgr.GetActiveTiles();
            Assert.IsNotNull(finalActive);
            Assert.IsTrue(finalActive.Count > 0, "After scrolling the camera and allowing the manager to recompute depth, at least one tile should be active for the new depth.");
        }
    }
}
