using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// TDD-friendly interface tests for the TileMeshCache and scheduler.
    /// These assert the minimal public contract that higher-level systems rely on.
    /// Tests use reflection so they remain stable while the implementation evolves.
    /// </summary>
    public class TileMeshCache_InterfaceTests
    {
        private Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .FirstOrDefault(t => t.Name == name);
        }

        [Test]
        public void TileMeshCache_ProvidesSingletonOrConstructibleInstance()
        {
            var cacheType = FindType("TileMeshCache");
            Assert.IsNotNull(cacheType, "TileMeshCache type must exist (implement the cache singleton).");

            // Try common instance access patterns: public static Instance property or GetInstance() or public ctor
            object instance = null;

            var prop = cacheType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (prop != null) instance = prop.GetValue(null);

            if (instance == null)
            {
                var mi = cacheType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static);
                if (mi != null) instance = mi.Invoke(null, null);
            }

            if (instance == null)
            {
                // Fall back to a public parameterless constructor
                var ctor = cacheType.GetConstructor(Type.EmptyTypes);
                if (ctor != null) instance = Activator.CreateInstance(cacheType);
            }

            Assert.IsNotNull(instance, "TileMeshCache must expose a usable instance (Instance property, GetInstance(), or public ctor).");
        }

        [Test]
        public void TileMeshCache_EnsureScheduled_Add_Get_Workflow()
        {
            var cacheType = FindType("TileMeshCache");
            Assert.IsNotNull(cacheType, "TileMeshCache type must exist.");

            // Acquire instance (same logic as previous test)
            object instance = null;
            var prop = cacheType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (prop != null) instance = prop.GetValue(null);
            if (instance == null)
            {
                var mi = cacheType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static);
                if (mi != null) instance = mi.Invoke(null, null);
            }
            if (instance == null)
            {
                var ctor = cacheType.GetConstructor(Type.EmptyTypes);
                if (ctor != null) instance = Activator.CreateInstance(cacheType);
            }
            Assert.IsNotNull(instance, "Unable to obtain TileMeshCache instance.");

            // Create a sample TileId (common struct present in project)
            var tileIdType = FindType("TileId");
            Assert.IsNotNull(tileIdType, "TileId type should exist.");
            // Try to construct with (face,x,y,depth) constructor if available
            object tileId = null;
            var ctor4 = tileIdType.GetConstructor(new Type[] { typeof(int), typeof(int), typeof(int), typeof(int) });
            if (ctor4 != null) tileId = ctor4.Invoke(new object[] { 0, 0, 0, 1 });
            else tileId = Activator.CreateInstance(tileIdType);
            Assert.IsNotNull(tileId, "Failed to create a TileId instance for testing.");

            // Find EnsureScheduled method (name may vary but we look for a method that accepts TileId)
            MethodInfo ensureMethod = cacheType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(m => m.Name.IndexOf("Ensure", StringComparison.OrdinalIgnoreCase) >= 0
                    && m.GetParameters().Any(p => p.ParameterType == tileIdType));
            Assert.IsNotNull(ensureMethod, "TileMeshCache must expose an EnsureScheduled-like method that accepts a TileId.");

            // Invoke EnsureScheduled (use 1 as a sample priority if method takes extra args)
            object handle = null;
            var ensureParams = ensureMethod.GetParameters();
            var ensureArgs = new object[ensureParams.Length];
            for (int i = 0; i < ensureParams.Length; i++)
            {
                if (ensureParams[i].ParameterType == tileIdType) ensureArgs[i] = tileId;
                else if (ensureParams[i].ParameterType == typeof(int)) ensureArgs[i] = 1;
                else ensureArgs[i] = Type.Missing;
            }

            handle = ensureMethod.IsStatic ? ensureMethod.Invoke(null, ensureArgs) : ensureMethod.Invoke(instance, ensureArgs);
            // We only assert that invocation completes; returned handle may be null depending on implementation
            Assert.DoesNotThrow(() => { /* invocation above */ });

            // Find a Get or TryGet method that returns a Mesh given TileId
            var meshType = FindType("Mesh") ?? typeof(UnityEngine.Mesh).GetType(); // Mesh type present
            MethodInfo getMethod = cacheType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(m => (m.Name == "Get" || m.Name.IndexOf("Get", StringComparison.OrdinalIgnoreCase) >= 0)
                    && m.GetParameters().Any(p => p.ParameterType == tileIdType));

            // If no Get method, skip mesh availability assertions but fail the shape contract
            Assert.IsNotNull(getMethod, "TileMeshCache should provide a Get(TileId) or similar method to retrieve cached Meshes.");

            // Before Add, Get may return null
            var before = getMethod.IsStatic ? getMethod.Invoke(null, new object[] { tileId }) : getMethod.Invoke(instance, new object[] { tileId });

            // Find Add method that accepts TileId and Mesh (or similar)
            MethodInfo addMethod = cacheType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(m => m.GetParameters().Any(p => p.ParameterType == tileIdType) && m.GetParameters().Any(p => p.ParameterType == typeof(Mesh)));

            if (addMethod == null)
            {
                // Try methods that take (object key, Mesh)
                addMethod = cacheType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .FirstOrDefault(m => m.GetParameters().Any(p => p.ParameterType == typeof(object)) && m.GetParameters().Any(p => p.ParameterType == typeof(Mesh)));
            }

            Assert.IsNotNull(addMethod, "TileMeshCache must provide an Add-like method to insert built Mesh objects.");

            // Construct a tiny test mesh
            var testMesh = new Mesh();
            testMesh.vertices = new Vector3[] { Vector3.zero, Vector3.right, Vector3.up };
            testMesh.triangles = new int[] { 0, 1, 2 };

            // Invoke Add
            var addParams = addMethod.GetParameters();
            var addArgs = new object[addParams.Length];
            for (int i = 0; i < addParams.Length; i++)
            {
                if (addParams[i].ParameterType == tileIdType) addArgs[i] = tileId;
                else if (addParams[i].ParameterType == typeof(Mesh)) addArgs[i] = testMesh;
                else addArgs[i] = Activator.CreateInstance(addParams[i].ParameterType);
            }

            addMethod.Invoke(addMethod.IsStatic ? null : instance, addArgs);

            // After adding, Get should return a Mesh (or non-null)
            var after = getMethod.IsStatic ? getMethod.Invoke(null, new object[] { tileId }) : getMethod.Invoke(instance, new object[] { tileId });
            Assert.IsNotNull(after, "After adding a mesh, TileMeshCache.Get(TileId) should return a Mesh instance.");
            Assert.IsInstanceOf<Mesh>(after, "Get must return a UnityEngine.Mesh instance.");
        }
    }
}