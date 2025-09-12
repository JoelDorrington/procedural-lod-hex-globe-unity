using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Verify depth transition points are placed exponentially toward the planet surface.
    /// The test parametrizes maxDistance = 2 * planetRadius and minDistance = planetRadius + 1.
    /// For each depth n (1..maxDepth) the transition point should be:
    /// T_n = min + (max - min) / (2^n)
    /// The manager's ComputeDepthFromCamera() method is invoked via reflection.
    /// </summary>
    public class DepthTransition_ExponentialSplitTests
    {
        private Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .FirstOrDefault(t => t.Name == name);
        }

        [Test]
        public void DepthTransitions_Are_ExponentialHalving_TowardSurface()
        {
            var mgrType = FindType("PlanetTileVisibilityManager");
            Assert.IsNotNull(mgrType, "PlanetTileVisibilityManager must exist.");

            var camType = FindType("CameraController");
            Assert.IsNotNull(camType, "CameraController must exist.");

            // Create manager instance
            var go = new GameObject("ptvm_test");
            var mgr = go.AddComponent(mgrType);

            try
            {
                // Create and assign a TerrainConfig with known baseRadius
                var configType = FindType("TerrainConfig");
                Assert.IsNotNull(configType, "TerrainConfig must exist.");
                var config = ScriptableObject.CreateInstance(configType) as ScriptableObject;
                // set baseRadius = 30f via reflection or direct property
                var brField = configType.GetField("baseRadius", BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(brField, "TerrainConfig.baseRadius field expected.");
                float planetRadius = 30f;
                brField.SetValue(config, planetRadius);

                // assign config to manager
                var cfgField = mgrType.GetField("config", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(cfgField, "PlanetTileVisibilityManager.config field expected.");
                cfgField.SetValue(mgr, config);

                // Create CameraController and assign to manager.GameCamera
                var camGo = new GameObject("camctrl");
                var camCtrl = camGo.AddComponent(camType);
                // set min/max/distance fields via reflection
                var minF = camType.GetField("minDistance", BindingFlags.Public | BindingFlags.Instance);
                var maxF = camType.GetField("maxDistance", BindingFlags.Public | BindingFlags.Instance);
                var distF = camType.GetField("distance", BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(minF);
                Assert.IsNotNull(maxF);
                Assert.IsNotNull(distF);

                float maxDist = planetRadius * 2f; // parameterized as double the planet radius
                float minDist = planetRadius + 1f;
                minF.SetValue(camCtrl, minDist);
                maxF.SetValue(camCtrl, maxDist);

                // assign GameCamera on manager
                var gameCamField = mgrType.GetField("GameCamera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(gameCamField, "GameCamera field expected on manager.");
                gameCamField.SetValue(mgr, camCtrl);

                // set manager.maxDepth to a test value (e.g., 4)
                var maxDepthField = mgrType.GetField("maxDepth", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                Assert.IsNotNull(maxDepthField, "maxDepth field expected.");
                int testMaxDepth = 4;
                maxDepthField.SetValue(mgr, testMaxDepth);

                // Obtain ComputeDepthFromCamera method
                var computeMethod = mgrType.GetMethod("ComputeDepthFromCamera", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(computeMethod, "ComputeDepthFromCamera must exist.");

                // For each depth boundary n = 1..testMaxDepth, check that a distance just above T_n yields depth n-1
                // and a distance just below T_n yields depth >= n.
                for (int n = 1; n <= testMaxDepth; n++)
                {
                    float Tn = minDist + (maxDist - minDist) / Mathf.Pow(2f, n);
                    float eps = 1e-3f;

                    // Distance slightly greater than Tn (farther away) should produce depth <= n-1
                    float d_far = Tn + eps;
                    distF.SetValue(camCtrl, d_far);
                    var depthFar = (int)computeMethod.Invoke(mgr, null);
                    Assert.LessOrEqual(depthFar, n - 1, $"At distance {d_far} (just > T{n}={Tn}) depth should be <= {n - 1} but was {depthFar}.");

                    // Distance slightly less than Tn (closer) should produce depth >= n
                    float d_near = Math.Max(minDist, Tn - eps);
                    distF.SetValue(camCtrl, d_near);
                    var depthNear = (int)computeMethod.Invoke(mgr, null);
                    Assert.GreaterOrEqual(depthNear, n, $"At distance {d_near} (just < T{n}={Tn}) depth should be >= {n} but was {depthNear}.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
