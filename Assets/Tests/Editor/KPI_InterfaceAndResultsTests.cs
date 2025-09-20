using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HexGlobeProject.TerrainSystem.LOD;
using NUnit.Framework;
using UnityEngine;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Lightweight KPI tests that exercise the public interfaces and observable results
    /// of the math-only visibility work. These tests avoid implementation details and
    /// assert the critical contracts used by higher-level systems.
    /// </summary>
    public class KPI_InterfaceAndResultsTests
    {
        private Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .FirstOrDefault(t => t.Name == name);
        }

        [Test]
        public void MathVisibilitySelector_TileFromDirection_ReturnsValidTile()
        {
            var selectorType = FindType("MathVisibilitySelector");
            Assert.IsNotNull(selectorType, "MathVisibilitySelector type must be present for math-based visibility.");

            var tileFromDir = selectorType.GetMethod("TileFromDirection", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(tileFromDir, "TileFromDirection must be a public static method.");

            int depth = 2;
            // Use a reliable canonical direction (face 0 Bary center)
            var dir = FindType("IcosphereMapping").GetMethod("BaryToWorldDirection", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { 0, 0.5f, 0.5f }) as Vector3? ?? Vector3.forward;

            var tile = tileFromDir.Invoke(null, new object[] { dir, depth });
            Assert.IsNotNull(tile, "TileFromDirection returned null.");

            var tileType = tile.GetType();
            var depthField = tileType.GetField("depth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(depthField, "TileId must expose a depth field/property.");
            var depthVal = (int)depthField.GetValue(tile);
            Assert.AreEqual(depth, depthVal, "Returned TileId.depth must match requested depth.");
        }

        [Test]
        public void PlanetTileVisibilityManager_SetDepth_PopulatesPrecomputedRegistry_WithCornersAndNormals()
        {
            var mgrType = FindType("PlanetTileVisibilityManager");
            Assert.IsNotNull(mgrType, "PlanetTileVisibilityManager must exist.");

            var go = new GameObject("kpi_ptvm");
            var mgr = go.AddComponent(mgrType);

            try
            {
                // Call SetDepth(1)
                var setDepth = mgrType.GetMethod("SetDepth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(setDepth, "SetDepth method expected on manager.");
                setDepth.Invoke(mgr, new object[] { 1 });

                // Access private static registry
                // Ensure precomputed registry populated for depth 1
                var regField = mgrType.GetField("tileRegistry", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(regField, "tileRegistry expected.");
                var dict = regField.GetValue(mgr) as IDictionary;
                Assert.IsNotNull(dict, "tileRegistry should be a dictionary-like object.");
                Assert.IsTrue(dict.Contains(1), "Registry must contain an entry for depth 1 after SetDepth.");

                var list = dict[1] as TerrainTileRegistry;
                Assert.IsNotNull(list, "Registry entry must be enumerable of precomputed entries.");

                int sampled = 0;
                foreach (var entry in list.tiles.Values)
                {
                    sampled++;
                    var et = entry.GetType();
                    var normalField = et.GetField("normal", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var cornersField = et.GetField("cornerWorldPositions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert.IsNotNull(normalField, "Precomputed entry must have a normal field.");
                    Assert.IsNotNull(cornersField, "Precomputed entry must have cornerWorldPositions.");

                    var n = (Vector3)normalField.GetValue(entry);
                    Assert.IsTrue(Mathf.Approximately(n.magnitude, 1f) || n.magnitude > 0.9f, "Precomputed normals should be unit-length (or close).");

                    var corners = cornersField.GetValue(entry) as IEnumerable;
                    Assert.IsNotNull(corners, "cornerWorldPositions should be present and iterable.");
                    int ccount = 0;
                    foreach (var c in corners) { ccount++; Assert.IsInstanceOf<Vector3>(c); }
                    Assert.IsTrue(ccount >= 1, "Each precomputed entry should expose at least one corner world position.");

                    if (sampled >= 8) break; // sample a small number for speed
                }
                Assert.Greater(sampled, 0, "Registry must contain at least one precomputed entry for depth 1.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlanetTileVisibilityManager_UpdateVisibilityMathBased_Runs_WithCameraAndDoesNotThrow()
        {
            var mgrType = FindType("PlanetTileVisibilityManager");
            Assert.IsNotNull(mgrType, "PlanetTileVisibilityManager must exist.");

            var go = new GameObject("kpi_ptvm_run");
            var mgr = go.AddComponent(mgrType);

            GameObject camGo = null;
            try
            {
                // Attach a real CameraController if available; otherwise attach a Camera component
                var ccType = FindType("CameraController");
                if (ccType != null)
                {
                    camGo = new GameObject("kpi_cam");
                    var camComp = camGo.AddComponent(ccType);
                    // Try to set basic distance fields if present
                    var distField = ccType.GetField("distance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var minField = ccType.GetField("minDistance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var maxField = ccType.GetField("maxDistance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (distField != null) distField.SetValue(camComp, 200f);
                    if (minField != null) minField.SetValue(camComp, 10f);
                    if (maxField != null) maxField.SetValue(camComp, 1000f);

                    // Assign to manager.GameCamera if such a field/property exists
                    var gameCamField = mgrType.GetField("GameCamera", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (gameCamField != null && gameCamField.FieldType.IsAssignableFrom(ccType))
                        gameCamField.SetValue(mgr, camComp);
                    else
                    {
                        var prop = mgrType.GetProperty("GameCamera", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (prop != null && prop.PropertyType.IsAssignableFrom(ccType)) prop.SetValue(mgr, camComp);
                    }
                }
                else
                {
                    camGo = new GameObject("kpi_cam_simple");
                    camGo.AddComponent<Camera>();
                    // attempt to set a CameraController-like field may not exist; skip assignment
                }

                // Ensure depth and precomputed registry exist so math path can exercise both branches
                var setDepth = mgrType.GetMethod("SetDepth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (setDepth != null) setDepth.Invoke(mgr, new object[] { 1 });

                // Call private UpdateVisibilityMathBased via reflection
                var upd = mgrType.GetMethod("UpdateVisibilityMathBased", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(upd, "UpdateVisibilityMathBased should exist as the math-based entrypoint.");

                // Invocation should not throw
                Assert.DoesNotThrow(() => upd.Invoke(mgr, null));

                // Try to observe a positive effect: if manager exposes GetActiveTiles(), ensure it returns a collection (optional)
                var getActive = mgrType.GetMethod("GetActiveTiles", BindingFlags.Public | BindingFlags.Instance);
                if (getActive != null)
                {
                    var active = getActive.Invoke(mgr, null) as IEnumerable;
                    Assert.IsNotNull(active, "GetActiveTiles should return an enumerable when present.");
                }
            }
            finally
            {
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}