using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.HexMap;
using HexGlobeProject.Graphics.DataStructures;

namespace HexGlobeProject.Tests.Editor
{
    public class DualOverlayBufferTests
    {
        [Test]
        public void UploadSegments_BufferMatchesExtractedSegments_MeshSpace()
        {
            float baseRadius = 30f;
            var mesh = IcosphereGenerator.GenerateIcosphere(baseRadius, 1);

            Shader sh = Shader.Find("HexGlobe/PlanetTerrain");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh);

            var expected = DualMeshBuilder.ExtractSegments(mesh, baseRadius, true);
            Assert.IsNotNull(expected);
            Assert.IsTrue(expected.Length > 0, "Expected some extracted segments");

            int pairs = DualOverlayBuffer.UploadSegmentsToMaterial(mat, mesh, baseRadius, null);
            Assert.Greater(pairs, 0);

            var buf = DualOverlayBuffer.GetBuffer(mat);
            Assert.IsNotNull(buf, "ComputeBuffer should be created and retrievable");

            int pairCount = expected.Length / 2;
            var outData = new Vector4[pairCount * 2];
            buf.GetData(outData);
            Assert.AreEqual(pairCount * 2, outData.Length);

            for (int si = 0; si < pairCount; ++si)
            {
                int idx = si * 2;
                Vector3 a = expected[idx];
                Vector3 b = expected[idx + 1];
                Vector3 mid = (a + b) * 0.5f;
                Vector3 axis = b - a;
                float len = axis.magnitude;
                Vector3 dir = (len > 1e-6f) ? (axis / len) : Vector3.up;
                float halfLen = len * 0.5f;

                var mid4 = outData[idx];
                var dir4 = outData[idx + 1];

                Assert.AreEqual(mid.x, mid4.x, 1e-3f, $"mid.x mismatch at pair {si}");
                Assert.AreEqual(mid.y, mid4.y, 1e-3f, $"mid.y mismatch at pair {si}");
                Assert.AreEqual(mid.z, mid4.z, 1e-3f, $"mid.z mismatch at pair {si}");
                Assert.AreEqual(halfLen, mid4.w, 1e-3f, $"halfLen mismatch at pair {si}");

                Assert.AreEqual(dir.x, dir4.x, 1e-3f, $"dir.x mismatch at pair {si}");
                Assert.AreEqual(dir.y, dir4.y, 1e-3f, $"dir.y mismatch at pair {si}");
                Assert.AreEqual(dir.z, dir4.z, 1e-3f, $"dir.z mismatch at pair {si}");
                // radius is conservative (halfThickness + radialMargin) so we only assert it's > 0
                Assert.Greater(dir4.w, 0f, $"radius should be positive at pair {si}");
            }

            DualOverlayBuffer.ReleaseAll();
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void UploadSegments_TransformsApplied_WhenTransformProvided()
        {
            float baseRadius = 30f;
            var mesh = IcosphereGenerator.GenerateIcosphere(baseRadius, 1);

            Shader sh = Shader.Find("HexGlobe/PlanetTerrain");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh);

            var expected = DualMeshBuilder.ExtractSegments(mesh, baseRadius, true);
            Assert.IsNotNull(expected);
            Assert.IsTrue(expected.Length > 0, "Expected some extracted segments");

            var go = new GameObject("TestTransform");
            go.transform.position = new Vector3(10.0f, 5.0f, -3.0f);

            int pairs = DualOverlayBuffer.UploadSegmentsToMaterial(mat, mesh, baseRadius, go.transform);
            Assert.Greater(pairs, 0);

            var buf = DualOverlayBuffer.GetBuffer(mat);
            Assert.IsNotNull(buf, "ComputeBuffer should be created and retrievable");

            // Buffer holds capsule-packed pairs: [mid.xyz,halfLen], [dir.xyz,radius]
            int pairCount = expected.Length / 2;
            var outData = new Vector4[pairCount * 2];
            buf.GetData(outData);

            for (int si = 0; si < pairCount; ++si)
            {
                int idx = si * 2;
                var a = go.transform.TransformPoint(expected[idx]);
                var b = go.transform.TransformPoint(expected[idx + 1]);
                Vector3 mid = (a + b) * 0.5f;
                Vector3 axis = b - a;
                float len = axis.magnitude;
                Vector3 dir = (len > 1e-6f) ? (axis / len) : Vector3.up;
                float halfLen = len * 0.5f;

                var mid4 = outData[idx];
                var dir4 = outData[idx + 1];

                Assert.AreEqual(mid.x, mid4.x, 1e-3f, $"mid.x mismatch at pair {si}");
                Assert.AreEqual(mid.y, mid4.y, 1e-3f, $"mid.y mismatch at pair {si}");
                Assert.AreEqual(mid.z, mid4.z, 1e-3f, $"mid.z mismatch at pair {si}");
                Assert.AreEqual(halfLen, mid4.w, 1e-3f, $"halfLen mismatch at pair {si}");

                Assert.AreEqual(dir.x, dir4.x, 1e-3f, $"dir.x mismatch at pair {si}");
                Assert.AreEqual(dir.y, dir4.y, 1e-3f, $"dir.y mismatch at pair {si}");
                Assert.AreEqual(dir.z, dir4.z, 1e-3f, $"dir.z mismatch at pair {si}");
                Assert.Greater(dir4.w, 0f, $"radius should be positive at pair {si}");
            }

            DualOverlayBuffer.ReleaseAll();
            Object.DestroyImmediate(mat);
            Object.DestroyImmediate(go);
        }
    }
}