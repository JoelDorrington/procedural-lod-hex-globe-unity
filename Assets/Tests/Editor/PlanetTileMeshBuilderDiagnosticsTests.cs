using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileMeshBuilderDiagnosticsTests
    {
        [Test]
        public void IcosphereMapping_BarycentricToWorldDirection_ProducesDistinctDirections()
        {
            // Sample a small set of barycentric coordinates across face 0
            var dirs = new List<Vector3>();
            var samples = new List<(float u, float v)>
            {
                (0.25f, 0.25f),
                (0.75f, 0.25f),
                (0.25f, 0.75f),
                (0.5f, 0.5f)
            };

            foreach (var s in samples)
            {
                var d = IcosphereMapping.BarycentricToWorldDirection(0, s.u, s.v).normalized;
                dirs.Add(d);
            }

            // Ensure not all directions are identical
            bool allSame = true;
            for (int i = 1; i < dirs.Count; i++)
            {
                if (Vector3.Distance(dirs[0], dirs[i]) > 1e-6f) { allSame = false; break; }
            }
            Assert.IsFalse(allSame, "IcosphereMapping.BarycentricToWorldDirection returned identical directions for different barycentric coordinates");
        }

        [Test]
        public void SimplePerlinHeightProvider_SamplesVaryAcrossDirections()
        {
            var provider = new SimplePerlinHeightProvider();
            provider.baseFrequency = 1f;
            provider.octaves = 3;
            provider.seed = 12345;

            var dirs = new List<Vector3>
            {
                new Vector3(0f,0f,1f),
                new Vector3(0.5f,0f,0.8660254f).normalized,
                new Vector3(0f,0.5f,0.8660254f).normalized,
                new Vector3(0.707f,0.707f,0f).normalized
            };

            float min = float.MaxValue, max = float.MinValue;
            foreach (var d in dirs)
            {
                float v = provider.Sample(in d, 4);
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }

            Assert.Greater(max, min, "SimplePerlinHeightProvider produced no variation across sampled directions");
        }

        [Test]
        public void TileVertexToBarycentricCoordinates_ProducesDistinctUVsForGrid()
        {
            var id = new TileId(0, 0, 0, 0);
            int res = 4;
            var uvs = new HashSet<(float, float)>();
            for (int j = 0; j < res; j++)
            {
                for (int i = 0; i < res; i++)
                {
                    IcosphereMapping.TileVertexToBarycentricCoordinates(id, i, j, res, out float u, out float v);
                    uvs.Add((u, v));
                }
            }
            Assert.Greater(uvs.Count, 1, "TileVertexToBarycentricCoordinates returned identical UVs for grid vertices");
        }
    }
}
