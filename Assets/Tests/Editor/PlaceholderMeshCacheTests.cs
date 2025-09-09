using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem;

namespace HexGlobeProject.Tests.Editor
{
    public class PlaceholderMeshCacheTests
    {
        [Test]
        public void Manager_Provides_OnePlaceholderMeshPerDepth_And_ReusesIt()
        {
            var mgrGO = new GameObject("MgrCacheTest");
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();

            var planetGO = new GameObject("PlanetCachePlanet");
            var so = new SerializedObject(mgr);
            var planetProp = so.FindProperty("planetTransform");
            if (planetProp != null) planetProp.objectReferenceValue = planetGO.transform;
            so.ApplyModifiedProperties();

            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 10f;
            cfg.baseResolution = 8;
            cfg.heightScale = 0f;
            mgr.config = cfg;

            // Expect the manager to expose a public readonly list/array named PlaceholderMeshes
            var prop = typeof(PlanetTileVisibilityManager).GetProperty("PlaceholderMeshes", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(prop, "PlanetTileVisibilityManager must expose a public property 'PlaceholderMeshes' to access per-depth placeholder meshes");

            // Trigger precompute / initialization across depths 0..maxDepth
            var precompute = typeof(PlanetTileVisibilityManager).GetMethod("PrecomputeTileNormalsForDepth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(precompute, "PrecomputeTileNormalsForDepth not found");

            // Read manager's private maxDepth via reflection for test alignment
            var maxDepthField = typeof(PlanetTileVisibilityManager).GetField("maxDepth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int maxDepth = maxDepthField != null ? (int)maxDepthField.GetValue(mgr) : 2;

            for (int d = 0; d <= maxDepth; d++) precompute.Invoke(mgr, new object[] { d });

            // Read PlaceholderMeshes property
            var meshesObj = prop.GetValue(mgr);
            Assert.IsNotNull(meshesObj, "PlaceholderMeshes property returned null; expected a collection of Mesh per depth");

            Mesh[] meshes = meshesObj as Mesh[] ?? (meshesObj as System.Collections.Generic.IReadOnlyList<Mesh>) as Mesh[];
            // Try common shapes: array or IReadOnlyList<Mesh>
            if (meshes == null)
            {
                var asList = meshesObj as System.Collections.IEnumerable;
                var tmp = new System.Collections.Generic.List<Mesh>();
                if (asList != null)
                {
                    foreach (var o in asList) tmp.Add(o as Mesh);
                    meshes = tmp.ToArray();
                }
            }

            Assert.IsNotNull(meshes, "Could not coerce PlaceholderMeshes to Mesh[]");
            Assert.AreEqual(maxDepth + 1, meshes.Length, $"Expected PlaceholderMeshes length == maxDepth+1 ({maxDepth + 1})");

            // Spawn two tiles at same depth and ensure they reference the same placeholder mesh for that depth
            int testDepth = Mathf.Min(1, maxDepth);
            var idA = new TileId(0, 0, 0, testDepth);
            var idB = new TileId(0, 1, 0, testDepth);
            var goA = mgr.TrySpawnTile(idA, cfg.baseResolution);
            var goB = mgr.TrySpawnTile(idB, cfg.baseResolution);
            Assert.IsNotNull(goA); Assert.IsNotNull(goB);
            var tileA = goA.GetComponent<PlanetTerrainTile>();
            var tileB = goB.GetComponent<PlanetTerrainTile>();
            Assert.IsNotNull(tileA); Assert.IsNotNull(tileB);

            Assert.IsNotNull(tileA.meshFilter.sharedMesh, "Tile A should have a placeholder mesh assigned");
            Assert.IsNotNull(tileB.meshFilter.sharedMesh, "Tile B should have a placeholder mesh assigned");

            Assert.AreSame(meshes[testDepth], tileA.meshFilter.sharedMesh, "Tile A must reference the manager's placeholder mesh for its depth");
            Assert.AreSame(meshes[testDepth], tileB.meshFilter.sharedMesh, "Tile B must reference the manager's placeholder mesh for its depth");

            Object.DestroyImmediate(goA); Object.DestroyImmediate(goB);
            Object.DestroyImmediate(mgrGO); Object.DestroyImmediate(planetGO);
        }
    }
}
