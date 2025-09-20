using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using HexGlobeProject.TerrainSystem.Core;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileMeshBuilderDiagnosticsTests
    {
        [Test]
        public void IcosphereMapping_BaryToWorldDirection_ProducesDistinctDirections()
        {
            // Sample a small set of Bary coordinates across face 0
            var dirs = new List<Vector3>();
            var samples = new List<Barycentric>
            {
                new (0.25f, 0.25f),
                new (0.75f, 0.25f),
                new (0.25f, 0.75f),
                new (0.5f, 0.5f)
            };

            foreach (var s in samples)
            {
                var d = IcosphereMapping.BaryToWorldDirection(0, s).normalized;
                dirs.Add(d);
            }

            // Ensure not all directions are identical
            bool allSame = true;
            for (int i = 1; i < dirs.Count; i++)
            {
                if (Vector3.Distance(dirs[0], dirs[i]) > 1e-6f) { allSame = false; break; }
            }
            Assert.IsFalse(allSame, "IcosphereMapping.BaryToWorldDirection returned identical directions for different Bary coordinates");
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
    }
}
