using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem;

namespace HexGlobeProject.Tests.Editor
{
    public class Perlin3DHeightProviderTests
    {
        private Perlin3DHeightProvider provider;

        [SetUp]
        public void SetUp()
        {
            provider = new Perlin3DHeightProvider
            {
                baseFrequency = 1f,
                octaves = 3,
                lacunarity = 2f,
                gain = 0.5f,
                amplitude = 1f,
                seed = 42
            };
        }

        [Test]
        public void Sample_ShouldBeDeterministic()
        {
            var dir = new Vector3(0.5f, 0.7f, 0.2f).normalized;
            float a = provider.Sample(dir, 32);
            float b = provider.Sample(dir, 32);
            float c = provider.Sample(dir, 32);

            Assert.AreEqual(a, b, 0.0001f);
            Assert.AreEqual(a, c, 0.0001f);
        }

        [Test]
        public void Sample_ResolutionShouldNotAffectTopology()
        {
            var dir = new Vector3(0.3f, 0.6f, 0.7f).normalized;
            float r4 = provider.Sample(dir, 4);
            float r16 = provider.Sample(dir, 16);
            float r64 = provider.Sample(dir, 64);
            float r256 = provider.Sample(dir, 256);

            float tol = 0.0001f;
            Assert.AreEqual(r4, r16, tol);
            Assert.AreEqual(r4, r64, tol);
            Assert.AreEqual(r4, r256, tol);
        }
    }
}
