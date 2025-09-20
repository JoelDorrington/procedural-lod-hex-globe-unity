using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    [TestFixture]
    public class TileIdEnumerationTests
    {
        [Test]
        public void GetAllTileIdsSortedByDistance_ShouldTerminateEnumeration_ForDepth0()
        {
            // Arrange: Create a test manager with precomputed registry
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<PlanetTileVisibilityManager>();
            
            // Set up basic config
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 1f;
            config.baseResolution = 16;
            manager.config = config;
            
            try
            {
                // Trigger precomputation for depth 0 (should create 20 tiles)
                manager.SetDepth(0);
                
                // Act: Get all TileIds using reflection to access private method
                var method = typeof(PlanetTileVisibilityManager).GetMethod("GetAllTileIdsSortedByDistance", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(method, "GetAllTileIdsSortedByDistance method should exist");
                
                var tileIds = method.Invoke(manager, new object[] { Vector3.zero, 0 }) as List<TileId>;
                
                // Assert: Enumeration should terminate and return finite list
                Assert.IsNotNull(tileIds, "Method should return a list");
                int expectedPerFace = HexGlobeProject.Tests.Editor.IcosphereTestHelpers.GetValidTileCountForDepth(0);
                int expectedTotal = 20 * expectedPerFace;
                Assert.AreEqual(expectedTotal, tileIds.Count, $"Depth 0 should generate exactly {expectedTotal} TileIds");
                
                // Verify enumeration terminates by converting to array (this would hang if infinite)
                var tileArray = tileIds.ToArray();
                Assert.AreEqual(expectedTotal, tileArray.Length, $"ToArray conversion should complete with {expectedTotal} items");
                
                // Verify all TileIds are unique (no duplicates that could cause processing loops)
                var uniqueTileIds = new HashSet<TileId>(tileIds);
                Assert.AreEqual(expectedTotal, uniqueTileIds.Count, "All TileIds should be unique (no duplicates)");
                
                // Verify all TileIds have the correct depth
                foreach (var tileId in tileIds)
                {
                    Assert.AreEqual(0, tileId.depth, "All TileIds should have depth 0");
                    Assert.GreaterOrEqual(tileId.face, 0, "Face should be non-negative");
                    Assert.LessOrEqual(tileId.face, 19, "Face should be within icosphere range (0-19)");
                }
            }
            finally
            {
                // Cleanup
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(go);
            }
        }
        
        [Test]
        public void GetAllTileIdsSortedByDistance_ShouldTerminateEnumeration_ForDepth1()
        {
            // Arrange: Create a test manager with precomputed registry
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<PlanetTileVisibilityManager>();
            
            // Set up basic config
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 1f;
            config.baseResolution = 16;
            manager.config = config;
            
            try
            {
                // Trigger precomputation for depth 1 (should create 20 * 2^2 = 80 tiles)
                manager.SetDepth(1);
                
                // Act: Get all TileIds using reflection to access private method
                var method = typeof(PlanetTileVisibilityManager).GetMethod("GetAllTileIdsSortedByDistance", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(method, "GetAllTileIdsSortedByDistance method should exist");
                
                var tileIds = method.Invoke(manager, new object[] { Vector3.zero, 1 }) as List<TileId>;
                
                // Assert: Enumeration should terminate and return finite list
                Assert.IsNotNull(tileIds, "Method should return a list");
                int tilesPerEdge1 = 1 << 1; // 2^1
                int expectedTotal1 = 20 * tilesPerEdge1 * tilesPerEdge1; // 20 * (2^1)^2 = 80
                Assert.AreEqual(expectedTotal1, tileIds.Count, $"Depth 1 should generate exactly {expectedTotal1} TileIds (20 faces * tilesPerEdge^2)");
                
                // Verify enumeration terminates by converting to array (this would hang if infinite)
                var tileArray = tileIds.ToArray();
                Assert.AreEqual(expectedTotal1, tileArray.Length, $"ToArray conversion should complete with {expectedTotal1} items");
                
                // Verify all TileIds are unique (no duplicates that could cause processing loops)
                var uniqueTileIds = new HashSet<TileId>(tileIds);
                Assert.AreEqual(expectedTotal1, uniqueTileIds.Count, "All TileIds should be unique (no duplicates)");
                
                // Verify all TileIds have the correct depth
                foreach (var tileId in tileIds)
                {
                    Assert.AreEqual(1, tileId.depth, "All TileIds should have depth 1");
                    Assert.GreaterOrEqual(tileId.face, 0, "Face should be non-negative");
                    Assert.LessOrEqual(tileId.face, 19, "Face should be within icosphere range (0-19)");
                    Assert.GreaterOrEqual(tileId.x, 0, "X coordinate should be non-negative");
                    Assert.LessOrEqual(tileId.x, 1, "X coordinate should be within depth 1 range (0-1)");
                    Assert.GreaterOrEqual(tileId.y, 0, "Y coordinate should be non-negative");
                    Assert.LessOrEqual(tileId.y, 1, "Y coordinate should be within depth 1 range (0-1)");
                }
            }
            finally
            {
                // Cleanup
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(go);
            }
        }
        
        [Test]
        public void TileId_Equality_ShouldWorkCorrectlyForEnumerationDeduplication()
        {
            // Arrange: Create identical TileIds
            var tileId1 = new TileId(0, 0, 0, 0);
            var tileId2 = new TileId(0, 0, 0, 0);
            var tileId3 = new TileId(1, 0, 0, 0); // Different face
            
            // Act & Assert: Test equality
            Assert.AreEqual(tileId1, tileId2, "Identical TileIds should be equal");
            Assert.AreNotEqual(tileId1, tileId3, "Different TileIds should not be equal");
            
            // Test HashSet deduplication (critical for enumeration termination)
            var tileSet = new HashSet<TileId> { tileId1, tileId2, tileId3 };
            Assert.AreEqual(2, tileSet.Count, "HashSet should deduplicate identical TileIds");
            
            // Test that adding the same TileId multiple times doesn't grow the set
            tileSet.Add(tileId1);
            tileSet.Add(tileId2);
            Assert.AreEqual(2, tileSet.Count, "Adding duplicate TileIds should not grow the set");
        }
    }
}
