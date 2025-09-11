using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Integration-style test that exercises the math-based visibility flow end-to-end
    /// at a light-weight level: SetDepth -> precompute -> math selector -> schedule/add mesh.
    /// This verifies the primary plumbing between manager, selector and cache.
    /// </summary>
    public class Integration_MathVisibility_FlowTests
    {
        private Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .FirstOrDefault(t => t.Name == name);
        }

        [Test]
        public void Integration_MathVisibility_EndToEnd()
        {
            var mgrType = FindType("PlanetTileVisibilityManager");
            Assert.IsNotNull(mgrType, "PlanetTileVisibilityManager type must exist for integration test.");

            var go = new GameObject("int_ptvm");
            var mgr = go.AddComponent(mgrType);

            try
            {
                // Set depth to 2 so precompute runs and selector has reasonable tiles
                var setDepth = mgrType.GetMethod("SetDepth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(setDepth, "SetDepth expected on manager.");
                setDepth.Invoke(mgr, new object[] { 2 });

                // Ensure precomputed registry populated for depth 2
                var regField = mgrType.GetField("tileRegistry", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(regField, "tileRegistry expected.");
                var dict = regField.GetValue(mgr) as IDictionary;
                Assert.IsNotNull(dict, "tileRegistry should be a dictionary-like object.");
                Assert.IsTrue(dict.Contains(2), "Precomputed registry should contain depth 2 after SetDepth.");

                // Call the math-based entrypoint
                var upd = mgrType.GetMethod("UpdateVisibilityMathBased", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(upd, "UpdateVisibilityMathBased should exist.");
                Assert.DoesNotThrow(() => upd.Invoke(mgr, null));

                // Use MathVisibilitySelector to obtain a sample center tile and its 1-ring
                var selectorType = FindType("MathVisibilitySelector");
                Assert.IsNotNull(selectorType, "MathVisibilitySelector must exist.");

                var tileFromDir = selectorType.GetMethod("TileFromDirection", BindingFlags.Public | BindingFlags.Static);
                var getKRing = selectorType.GetMethod("GetKRing", BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(tileFromDir, "TileFromDirection must exist.");
                Assert.IsNotNull(getKRing, "GetKRing must exist.");

                // Pick a reliable direction (face 0 barycentric center)
                var icosType = FindType("IcosphereMapping");
                Assert.IsNotNull(icosType, "IcosphereMapping must exist.");
                var baryToWorld = icosType.GetMethod("BarycentricToWorldDirection", BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(baryToWorld, "BarycentricToWorldDirection expected.");

                var dirObj = baryToWorld.Invoke(null, new object[] { 0, 0.33f, 0.33f });
                var dir = dirObj as Vector3? ?? Vector3.forward;

                var centerTile = tileFromDir.Invoke(null, new object[] { dir, 2 });
                Assert.IsNotNull(centerTile, "TileFromDirection must return a TileId-like value.");

                var ringObj = getKRing.Invoke(null, new object[] { centerTile, 1 }) as IEnumerable;
                Assert.IsNotNull(ringObj, "GetKRing should return an IEnumerable of TileId.");

                var tiles = new List<object>();
                foreach (var t in ringObj)
                {
                    tiles.Add(t);
                    if (tiles.Count >= 16) break; // keep the test small
                }
                Assert.IsTrue(tiles.Count > 0, "GetKRing should yield at least one tile.");

                // Acquire TileMeshCache and schedule/add a mesh for one tile
                var cacheType = FindType("TileMeshCache");
                Assert.IsNotNull(cacheType, "TileMeshCache must exist.");

                object cacheInstance = null;
                var prop = cacheType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (prop != null) cacheInstance = prop.GetValue(null);
                if (cacheInstance == null)
                {
                    var ctor = cacheType.GetConstructor(Type.EmptyTypes);
                    if (ctor != null) cacheInstance = Activator.CreateInstance(cacheType);
                }
                Assert.IsNotNull(cacheInstance, "Unable to obtain TileMeshCache instance.");

                // Find EnsureScheduled and Add/Get methods
                var ensure = cacheType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name.IndexOf("Ensure", StringComparison.OrdinalIgnoreCase) >= 0 && m.GetParameters().Length >= 1);
                Assert.IsNotNull(ensure, "Cache must expose an EnsureScheduled-like method.");

                var add = cacheType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .FirstOrDefault(m => m.GetParameters().Any(p => p.ParameterType == typeof(Mesh)));
                Assert.IsNotNull(add, "Cache must expose an Add-like method that accepts a Mesh.");

                var get = cacheType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .FirstOrDefault(m => m.ReturnType == typeof(Mesh) || m.Name.IndexOf("Get", StringComparison.OrdinalIgnoreCase) >= 0);
                Assert.IsNotNull(get, "Cache should expose a Get-like method.");

                // Schedule the first tile
                var sampleTile = tiles[0];
                var ensureParams = ensure.GetParameters();
                var ensureArgs = new object[ensureParams.Length];
                for (int i = 0; i < ensureParams.Length; i++)
                {
                    if (ensureParams[i].ParameterType.IsAssignableFrom(sampleTile.GetType())) ensureArgs[i] = sampleTile;
                    else if (ensureParams[i].ParameterType == typeof(int)) ensureArgs[i] = 1;
                    else ensureArgs[i] = Type.Missing;
                }

                // Invoke EnsureScheduled
                if (ensure.IsStatic) ensure.Invoke(null, ensureArgs); else ensure.Invoke(cacheInstance, ensureArgs);

                // Create tiny mesh and Add
                var testMesh = new Mesh();
                testMesh.vertices = new Vector3[] { Vector3.zero, Vector3.right, Vector3.up };
                testMesh.triangles = new int[] { 0, 1, 2 };

                var addParams = add.GetParameters();
                var addArgs = new object[addParams.Length];
                for (int i = 0; i < addParams.Length; i++)
                {
                    if (addParams[i].ParameterType.IsAssignableFrom(sampleTile.GetType())) addArgs[i] = sampleTile;
                    else if (addParams[i].ParameterType == typeof(Mesh)) addArgs[i] = testMesh;
                    else addArgs[i] = Activator.CreateInstance(addParams[i].ParameterType);
                }

                if (add.IsStatic) add.Invoke(null, addArgs); else add.Invoke(cacheInstance, addArgs);

                // Try Get for the tile
                var getParams = get.GetParameters();
                object getResult = null;
                if (getParams.Length == 0)
                {
                    // maybe Get() returns a collection; try calling parameterless
                    getResult = get.IsStatic ? get.Invoke(null, null) : get.Invoke(cacheInstance, null);
                }
                else
                {
                    // pass the tile
                    object[] gargs = new object[] { sampleTile };
                    getResult = get.IsStatic ? get.Invoke(null, gargs) : get.Invoke(cacheInstance, gargs);
                }

                // We expect either a Mesh or a collection that contains a Mesh; be permissive
                Assert.IsNotNull(getResult, "Cache Get should return a non-null result after Add.");

            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
