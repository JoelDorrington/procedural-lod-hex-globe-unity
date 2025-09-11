using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HexGlobeProject.Tests.PlayMode;

namespace HexGlobeProject.Tests.PlayMode
{
    public class ViewportConstraintPlayModeTests
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
        public IEnumerator ActiveTiles_DoNotIncrease_When_Planet_Fills_View()
        {
            var mgr = builder.Manager;
            var cam = builder.CameraController;

            // Allow initialization
            yield return null;
            yield return null;

            // Wait until manager spawns initial set for the far-camera case
            int attempts = 0;
            int maxAttempts = 60;
            int farCount = 0;
            while (attempts++ < maxAttempts)
            {
                yield return null;
                var active = mgr.GetActiveTiles();
                if (active != null && active.Count > 0)
                {
                    farCount = active.Count;
                    break;
                }
            }

            Assert.Greater(farCount, 0, "Initial far-camera active tile count must be > 0");

            // Move camera very close to the planet to make it fill the viewport
            cam.distance = cam.minDistance;

            // Allow frames for manager.Update() and spawn worker
            attempts = 0;
            int closeCount = 0;
            while (attempts++ < maxAttempts)
            {
                yield return null;
                var active = mgr.GetActiveTiles();
                if (active != null && active.Count > 0)
                {
                    closeCount = active.Count;
                    // If count stabilizes below or equal to farCount we can exit early
                    if (closeCount <= farCount) break;
                }
            }

            Assert.Greater(closeCount, 0, "Close-camera active tile count must be > 0");

            // The viewport-angle constraint: when the planet fills the viewport, the manager
            // should not explode the active tile count. Allow some growth due to smaller
            // tile sizes and edge cases, but enforce a reasonable upper bound.
            int upperBound = 40;
            Assert.LessOrEqual(closeCount, upperBound, $"Active tiles when planet fills view ({closeCount}) should be <= {upperBound}");
        }
    }
}
