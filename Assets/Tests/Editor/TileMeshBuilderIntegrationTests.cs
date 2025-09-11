using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.Tests.PlayMode; // scene builder helper

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
            // Use the Playmode test scene builder to create a minimal planet scene
            var sceneBuilder = new PlaymodeTestSceneBuilder();
            sceneBuilder.Build();

            // Override the manager config with the intended test config
            var mgr = sceneBuilder.Manager;
            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 30f;
            cfg.baseResolution = 16;
            mgr.config = cfg;

            mgr.SetDepth(0);

            var tileId = new TileId(0, 0, 0, 0);
            var go = mgr.TrySpawnTile(tileId);

            var meshFilter = go.GetComponent<MeshFilter>();
            // The integration expectation: the spawned tile should already have the builder mesh assigned
            // (Currently this is not yet wired; the test is a failing guard until we integrate the builder.)
            Assert.IsNotNull(meshFilter.sharedMesh, "Builder did not produce a mesh (builder malfunction)");

            // Cleanup via builder helper
            sceneBuilder.Teardown();
        }
    }
}
