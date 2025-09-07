using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem;

namespace HexGlobeProject.Tests.Editor
{
    public class TileRayMappingTests
    {
        [Test]
        public void RayToTile_MappingShouldReturnPrecomputedEntry()
        {
            // Arrange: create manager and minimal scene objects
            var managerGO = new GameObject("TestVisibilityManager");
            var mgr = managerGO.AddComponent<PlanetTileVisibilityManager>();

            var planetGO = new GameObject("TestPlanet");
            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 0f, -200f);
            cam.transform.LookAt(Vector3.zero);

            // Configure manager via SerializedObject where needed
            var so = new SerializedObject(mgr);
            var planetProp = so.FindProperty("planetTransform");
            if (planetProp != null) planetProp.objectReferenceValue = planetGO.transform;
            var radiusProp = so.FindProperty("config");
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 50f;
            mgr.config = cfg;
            mgr.GameCamera = camGO.AddComponent<CameraController>();
            so.ApplyModifiedProperties();

            // Precompute entries for depth 1
            var precompute = typeof(PlanetTileVisibilityManager).GetMethod("PrecomputeTileNormalsForDepth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(precompute, "PrecomputeTileNormalsForDepth not found");
            precompute.Invoke(mgr, new object[] { 1 });

            // Build a few representative rays (viewport center and offsets)
            var camPos = cam.transform.position;
            var targets = new Vector2[] { new Vector2(0.5f, 0.5f), new Vector2(0.25f, 0.5f), new Vector2(0.75f, 0.5f), new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.75f) };

            foreach (var tv in targets)
            {
                // Convert viewport point to world ray similar to manager's GetRayForSample
                Ray ray = cam.ViewportPointToRay(new Vector3(tv.x, tv.y, 0f));

                // Compute mathematical sphere intersection used by fallback path
                Vector3 hitPoint = PlanetTerrainTile.GetSphereHitPoint(ray, Vector3.zero, cfg.baseRadius, 1.01f);
                Vector3 dir = (hitPoint - Vector3.zero).normalized;

                // Expect GetClosestPrecomputedTile to return a non-default entry
                bool ok = PlanetTileVisibilityManager.GetPrecomputedEntry(dir, 1, out var entry);
                Assert.IsTrue(ok, $"Precomputed entry lookup failed for dir={dir}");

                // Also validate TileCoordinateMapping path produces indices within range
                TileCoordinateMapping.WorldDirectionToTileCoordinates(dir, 1, out int face, out int tx, out int ty, out float lx, out float ly);
                int tilesPerEdge = 1 << 1;
                Assert.IsTrue(tx >= 0 && tx < tilesPerEdge, $"tileX out of range: {tx}");
                Assert.IsTrue(ty >= 0 && ty < tilesPerEdge, $"tileY out of range: {ty}");
            }

            // Cleanup
            Object.DestroyImmediate(managerGO);
            Object.DestroyImmediate(planetGO);
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(cfg);
        }
    }
}
