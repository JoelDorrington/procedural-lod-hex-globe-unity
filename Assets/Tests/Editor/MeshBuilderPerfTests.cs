using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem;

namespace HexGlobeProject.Tests.Editor
{
    public class MeshBuilderPerfTests
    {
        [Test]
        public void BuildTileMesh_Perf_SimpleLowRes()
        {
            var config = ScriptableObject.CreateInstance<TerrainConfig>();
            config.baseRadius = 1f;
            config.baseResolution = 8;

            var builder = new PlanetTileMeshBuilder(config, null, Vector3.zero);
            var id = new TileId(0, 0, 0, 2);
            var data = new TileData { id = id, resolution = Mathf.Max(8, config.baseResolution << id.depth) };

            int runs = 10;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < runs; i++)
            {
                builder.BuildTileMesh(data);
                // Clear mesh to avoid cache short-circuiting in this perf test
                data.mesh = null;
            }
            sw.Stop();
            var avgMs = sw.Elapsed.TotalMilliseconds / runs;
            Debug.Log($"BuildTileMesh average (low-res) = {avgMs:F2} ms over {runs} runs");

            Object.DestroyImmediate(config);
            Assert.Pass("Perf measurement recorded to log");
        }
    }
}
