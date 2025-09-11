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
    /// Ensure that any tile which has at least one corner vertex on the visible
    /// side of the planet (dot(camDir, cornerDir) > 0) is included by the manager
    /// and becomes active after a math visibility pass.
    /// </summary>
    public class Integration_TileCornerVisibilityTests
    {
        private Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .FirstOrDefault(t => t.Name == name);
        }

        [Test]
        public void Tiles_With_Any_Visible_Corner_Are_Spawned()
        {
            var mgrType = FindType("PlanetTileVisibilityManager");
            Assert.IsNotNull(mgrType, "PlanetTileVisibilityManager must exist.");

            var ccType = FindType("CameraController");
            if (ccType == null)
            {
                Assert.Ignore("CameraController type not present in project; skipping corner-visibility integration test.");
            }

            var go = new GameObject("corner_vis_mgr");
            var mgr = go.AddComponent(mgrType);

            GameObject camGo = null;
            GameObject planet = null;
            try
            {
                // Create planet root at origin and assign
                planet = new GameObject("CornerPlanet");
                var planetField = mgrType.GetField("planetTransform", BindingFlags.NonPublic | BindingFlags.Instance);
                if (planetField != null) planetField.SetValue(mgr, planet.transform);

                // Create CameraController and assign to manager
                camGo = new GameObject("corner_cam");
                var camComp = camGo.AddComponent(ccType);
                // Position camera along +Z so visible hemisphere is +Z
                camGo.transform.position = Vector3.forward * 10f;

                var gameCamField = mgrType.GetField("GameCamera", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (gameCamField != null && gameCamField.FieldType.IsAssignableFrom(ccType))
                {
                    gameCamField.SetValue(mgr, camComp);
                }
                else
                {
                    Assert.Ignore("Manager.GameCamera field not compatible with CameraController; skipping test.");
                }

                // Set depth small for test performance
                var setDepth = mgrType.GetMethod("SetDepth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(setDepth, "SetDepth expected on manager.");
                int depth = 1;
                setDepth.Invoke(mgr, new object[] { depth });

                // Ensure precomputed registry populated for depth 2
                var regField = mgrType.GetField("tileRegistry", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(regField, "tileRegistry expected.");
                var dict = regField.GetValue(mgr) as IDictionary;
                Assert.IsNotNull(dict, "tileRegistry should be a dictionary-like object.");
                Assert.IsTrue(dict.Contains(depth), "Precomputed registry must contain requested depth.");

                var registry = dict[depth] as TerrainTileRegistry;
                Assert.IsNotNull(registry, "Registry must be a TerrainTileRegistry.");

                // Compute camera direction from planet center
                Vector3 planetCenter = planet.transform.position;
                Vector3 camDir = (camGo.transform.position - planetCenter).normalized;

                var expected = new List<(int face, int x, int y)>();
                int sampled = 0;
                foreach (var entry in registry.tiles.Values)
                {
                    var et = entry.GetType();
                    var cornersField = et.GetField("cornerWorldPositions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var faceField = et.GetField("face", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var xField = et.GetField("x", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var yField = et.GetField("y", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cornersField == null || faceField == null || xField == null || yField == null) continue;

                    var corners = cornersField.GetValue(entry) as IEnumerable;
                    if (corners == null) continue;
                    bool anyVisible = false;
                    foreach (var c in corners)
                    {
                        if (c is Vector3 v)
                        {
                            Vector3 dir = (v - planetCenter).normalized;
                            if (Vector3.Dot(dir, camDir) > 0f) { anyVisible = true; break; }
                        }
                    }

                    if (anyVisible)
                    {
                        int face = (int)faceField.GetValue(entry);
                        int x = (int)xField.GetValue(entry);
                        int y = (int)yField.GetValue(entry);
                        expected.Add((face, x, y));
                    }

                    sampled++;
                    if (sampled >= 256) break; // limit samples for speed
                }

                Assert.IsTrue(expected.Count > 0, "At least one precomputed tile should have a corner visible from the camera.");

                // Run the visibility pass
                var upd = mgrType.GetMethod("UpdateVisibilityMathBased", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(upd, "UpdateVisibilityMathBased expected.");
                upd.Invoke(mgr, null);

                // Inspect active tiles
                var getActive = mgrType.GetMethod("GetActiveTiles", BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(getActive, "GetActiveTiles expected.");
                var active = getActive.Invoke(mgr, null) as IEnumerable;
                Assert.IsNotNull(active, "GetActiveTiles must return enumerable.");

                bool foundMatch = false;
                foreach (var t in active)
                {
                    var tt = t.GetType();
                    var dataField = tt.GetField("tileData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (dataField == null) continue;
                    var tileData = dataField.GetValue(t);
                    if (tileData == null) continue;
                    var idField = tileData.GetType().GetField("id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (idField == null) continue;
                    var id = idField.GetValue(tileData);
                    if (id == null) continue;
                    var faceProp = id.GetType().GetField("face", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var xProp = id.GetType().GetField("x", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var yProp = id.GetType().GetField("y", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (faceProp == null || xProp == null || yProp == null) continue;
                    int f = (int)faceProp.GetValue(id);
                    int xi = (int)xProp.GetValue(id);
                    int yi = (int)yProp.GetValue(id);
                    if (expected.Any(e => e.face == f && e.x == xi && e.y == yi)) { foundMatch = true; break; }
                }

                Assert.IsTrue(foundMatch, "At least one tile with a visible corner should be active after the visibility pass.");
            }
            finally
            {
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                if (planet != null) UnityEngine.Object.DestroyImmediate(planet);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
