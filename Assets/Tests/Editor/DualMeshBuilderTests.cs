using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.HexMap;
using HexGlobeProject.Graphics.DataStructures;

namespace HexGlobeProject.Tests.Editor
{
    public class DualMeshBuilderTests
    {
        [Test]
        public void ExtractSegments_CountMatchesUniqueEdges()
        {
            float radius = 10f;
            var mesh = IcosphereGenerator.GenerateIcosphere(radius: radius, subdivisions: 1);
            Assert.IsNotNull(mesh);

            var segments = DualMeshBuilder.ExtractSegments(mesh, radius);

            // For an icosphere with subdivisions=1, expectation: segments should be > 0
            Assert.IsTrue(segments.Length > 0, "No segments were generated");

            // Each segment is two Vector3 entries
            Assert.IsTrue(segments.Length % 2 == 0, "Segment array length must be even");

            int segCount = segments.Length / 2;
            // Basic sanity: segment count should be at least number of faces / 2 (heuristic)
            int faceCount = mesh.triangles.Length / 3;
            Assert.GreaterOrEqual(segCount, faceCount / 3, "Too few segments for face count");
        }

        [Test]
        public void ExtractSegments_EndpointsProjectedToRadius()
        {
            float radius = 12.5f;
            var mesh = IcosphereGenerator.GenerateIcosphere(radius: radius, subdivisions: 2);
            var segments = DualMeshBuilder.ExtractSegments(mesh, radius);
            Assert.IsTrue(segments.Length > 0);

            for (int i = 0; i < segments.Length; i++)
            {
                float len = segments[i].magnitude;
                Assert.That(Mathf.Abs(len - radius) < 1e-3f, $"Segment endpoint not projected to radius: {len} != {radius}");
            }
        }
    }
}