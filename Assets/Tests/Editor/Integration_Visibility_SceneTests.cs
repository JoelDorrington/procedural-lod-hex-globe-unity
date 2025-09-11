using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HexGlobeProject.Tests.Editor
{
    public class Integration_Visibility_SceneTests
    {
        private Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .FirstOrDefault(t => t.Name == name);
        }

        [Test]
        public void Manager_MathVisibility_SpawnsVisibleTiles()
        {
            var mgrType = FindType("PlanetTileVisibilityManager");
            Assert.IsNotNull(mgrType, "PlanetTileVisibilityManager must exist.");

            var go = new GameObject("int_vis_mgr");
            var mgr = go.AddComponent(mgrType);

            GameObject camGo = null;
            try
            {
                // Create a planet root so tiles parent under it
                var planet = new GameObject("PlanetRoot");
                var planetField = mgrType.GetField("planetTransform", BindingFlags.NonPublic | BindingFlags.Instance);
                if (planetField != null) planetField.SetValue(mgr, planet.transform);

                // Attach CameraController if available; otherwise attach Camera and try best-effort
                var ccType = FindType("CameraController");
                if (ccType != null)
                {
                    camGo = new GameObject("int_cam");
                    var camComp = camGo.AddComponent(ccType);
                    // Try to set a distance that results in reasonable depth (close enough to see tiles)
                    var distField = ccType.GetField("distance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (distField != null) distField.SetValue(camComp, 10f);

                    // Assign into manager.GameCamera
                    var gameCamField = mgrType.GetField("GameCamera", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (gameCamField != null && gameCamField.FieldType.IsAssignableFrom(ccType))
                    {
                        gameCamField.SetValue(mgr, camComp);
                    }
                }
                else
                {
                    camGo = new GameObject("int_cam_simple");
                    var cam = camGo.AddComponent<Camera>();
                    // Position camera away from planet center
                    camGo.transform.position = Vector3.forward * 10f;
                    // If manager has GameCamera of CameraController type, we cannot assign; rely on calling UpdateVisibilityMathBased directly
                }

                // Set depth to 1 to produce multiple tiles
                var setDepth = mgrType.GetMethod("SetDepth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(setDepth, "SetDepth expected on manager.");
                setDepth.Invoke(mgr, new object[] { 1 });

                // Call UpdateVisibilityMathBased
                var upd = mgrType.GetMethod("UpdateVisibilityMathBased", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(upd, "UpdateVisibilityMathBased expected.");
                upd.Invoke(mgr, null);

                // Give a small moment for any synchronous spawns (tests run synchronously)
                // Inspect active tiles
                var getActive = mgrType.GetMethod("GetActiveTiles", BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(getActive, "GetActiveTiles expected for inspection.");
                var active = getActive.Invoke(mgr, null) as IList;
                Assert.IsNotNull(active, "GetActiveTiles should return a list.");
                Assert.IsTrue(active.Count > 0, "Manager should have spawned at least one active tile after math visibility pass.");

                // Inspect first tile for mesh and active state
                var first = active[0];
                var tileType = first.GetType();
                var meshFilterProp = tileType.GetProperty("meshFilter", BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(meshFilterProp, "PlanetTerrainTile.meshFilter property expected.");
                var meshFilter = meshFilterProp.GetValue(first) as MeshFilter;
                Assert.IsNotNull(meshFilter, "Spawned tile must have a MeshFilter.");
                Assert.IsNotNull(meshFilter.sharedMesh, "Spawned tile must have a built Mesh assigned to MeshFilter.sharedMesh.");
                var goObj = tileType.GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance)?.GetValue(first) as GameObject;
                if (goObj == null)
                {
                    goObj = (first as Component)?.gameObject;
                }
                Assert.IsNotNull(goObj, "Tile GameObject should be retrievable.");
                Assert.IsTrue(goObj.activeInHierarchy, "Tile GameObject should be active in hierarchy.");
            }
            finally
            {
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
