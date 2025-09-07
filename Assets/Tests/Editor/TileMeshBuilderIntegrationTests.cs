using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Integration test wiring the PlanetTileMeshBuilder into the spawn flow.
    /// This test is intentionally added before the integration is implemented so
    /// it will fail and act as a reminder / guard for wiring the builder into TrySpawnTile.
    /// </summary>
    public class TileMeshBuilderIntegrationTests
    {
        [Test]
        public void TrySpawnTile_ShouldUseMeshBuilderToAssignVisualMesh()
        {
            var mgrGO = new GameObject("TestVisibilityManager");
            var mgr = mgrGO.AddComponent<PlanetTileVisibilityManager>();

            var planetGO = new GameObject("TestPlanet");
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 30f;
            cfg.baseResolution = 16;
            mgr.config = cfg;

            // Ensure manager has a planet transform and precomputed entries
            var so = new SerializedObject(mgr);
            var planetProp = so.FindProperty("planetTransform");
            if (planetProp != null) planetProp.objectReferenceValue = planetGO.transform;
            so.ApplyModifiedProperties();

            var precompute = typeof(PlanetTileVisibilityManager).GetMethod("PrecomputeTileNormalsForDepth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(precompute, "PrecomputeTileNormalsForDepth not found");
            precompute.Invoke(mgr, new object[] { 1 });

            // Spawn the tile via manager (current implementation does not use builder)
            var tileId = new TileId(0, 0, 0, 1);
            var go = mgr.TrySpawnTile(tileId, 16);
            Assert.IsNotNull(go, "TrySpawnTile returned null");

            var tile = go.GetComponent<PlanetTerrainTile>();
            Assert.IsNotNull(tile, "PlanetTerrainTile missing on spawned GO");

            // Build an authoritative mesh using PlanetTileMeshBuilder for the same TileId
            var builder = new PlanetTileMeshBuilder(cfg);
            var data = new TileData();
            data.id = tileId;
            data.resolution = 16;

            float rawMin = float.MaxValue, rawMax = float.MinValue;
            builder.BuildTileMesh(data, ref rawMin, ref rawMax);

            // The integration expectation: the spawned tile should already have the builder mesh assigned
            // (Currently this is not yet wired; the test is a failing guard until we integrate the builder.)
            Assert.IsNotNull(data.mesh, "Builder did not produce a mesh (builder malfunction)");

            // Expectation: the tile's meshFilter.sharedMesh should be the same mesh produced by the builder
            // This will fail until TrySpawnTile actually calls the builder and assigns data.mesh to the tile.
            Assert.AreSame(data.mesh, tile.meshFilter.sharedMesh, "TrySpawnTile must use PlanetTileMeshBuilder to assign visual mesh (integration not wired yet)");

            // Cleanup
            Object.DestroyImmediate(mgrGO);
            Object.DestroyImmediate(planetGO);
        }
    }
}
