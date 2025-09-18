using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.HexMap;

namespace HexGlobeProject.Tests.Editor
{
    public class DualOverlayBufferCoordinateSpaceTests
    {
        [Test]
        public void UploadedSegments_AreInWorldSpace()
        {
            // Create planet at a non-zero world position to detect coordinate-space bugs
            var go = new GameObject("_TestPlanet_Coord");
            go.transform.position = new Vector3(12.345f, 3.21f, -7.89f);
            var planet = go.AddComponent<Planet>();

            // generate the planet mesh
            planet.GeneratePlanet();

            // Enable overlay via reflection
            var setOverlayMethod = typeof(Planet).GetMethod("SetOverlayOnMaterials", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(setOverlayMethod, "Could not find SetOverlayOnMaterials method on Planet via reflection");
            setOverlayMethod.Invoke(planet, new object[] { true });

            // Find the planet's MeshFilter and CPU segments
            var mf = go.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf, "MeshFilter expected on Planet");
            // Compute baseRadius from mesh vertex magnitudes (mesh was generated with radius baked into vertices)
            var verts = mf.sharedMesh.vertices;
            float baseRadius = 0f;
            for (int i = 0; i < verts.Length; ++i) baseRadius = Mathf.Max(baseRadius, verts[i].magnitude);
            var cpuSegments = DualMeshBuilder.ExtractSegments(mf.sharedMesh, baseRadius, true);
            Assert.IsNotNull(cpuSegments);
            Assert.IsTrue(cpuSegments.Length > 0, "Expected CPU segments to be generated");

            // For each renderer/material check compute buffer contents
            var rends = go.GetComponentsInChildren<Renderer>(true);
            bool anyChecked = false;
            foreach (var r in rends)
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    var buf = DualOverlayBuffer.GetBuffer(mat);
                    if (buf == null) continue;

                    // Read buffer contents (capsule-packed: [mid.xyz,halfLen], [dir.xyz,radius])
                    var data = new UnityEngine.Vector4[buf.count];
                    buf.GetData(data);

                    // Build expected capsule pairs from world-space endpoints
                    int pairCount = cpuSegments.Length / 2;
                    var expectedMidDir = new UnityEngine.Vector4[pairCount * 2];
                    for (int si = 0; si < pairCount; ++si)
                    {
                        int idx = si * 2;
                        var a = go.transform.TransformPoint(cpuSegments[idx]);
                        var b = go.transform.TransformPoint(cpuSegments[idx + 1]);
                        Vector3 mid = (a + b) * 0.5f;
                        Vector3 axis = b - a;
                        float len = axis.magnitude;
                        Vector3 dir = len > 1e-6f ? (axis / len) : Vector3.up;
                        float halfLen = len * 0.5f;
                        expectedMidDir[idx] = new Vector4(mid.x, mid.y, mid.z, halfLen);
                        expectedMidDir[idx + 1] = new Vector4(dir.x, dir.y, dir.z, 0.0f); // radius checked separately
                    }

                    // Compare a small sample of pairs
                    int maxPairs = Mathf.Min(5, pairCount);
                    float eps = 1e-3f;
                    for (int si = 0; si < maxPairs; ++si)
                    {
                        int idx = si * 2;
                        var em = expectedMidDir[idx];
                        var ed = expectedMidDir[idx + 1];
                        var dm = data[idx];
                        var dd = data[idx + 1];
                        Assert.AreEqual(em.x, dm.x, eps, $"mid.x differs at pair {si}");
                        Assert.AreEqual(em.y, dm.y, eps, $"mid.y differs at pair {si}");
                        Assert.AreEqual(em.z, dm.z, eps, $"mid.z differs at pair {si}");
                        Assert.AreEqual(em.w, dm.w, eps, $"halfLen differs at pair {si}");
                        Assert.AreEqual(ed.x, dd.x, eps, $"dir.x differs at pair {si}");
                        Assert.AreEqual(ed.y, dd.y, eps, $"dir.y differs at pair {si}");
                        Assert.AreEqual(ed.z, dd.z, eps, $"dir.z differs at pair {si}");
                        Assert.Greater(dd.w, 0.0f, $"radius should be > 0 at pair {si}");
                    }

                    anyChecked = true;
                }
            }

            // cleanup
            DualOverlayBuffer.ReleaseAll();
            Object.DestroyImmediate(go);

            Assert.IsTrue(anyChecked, "No material with a dual overlay compute buffer was found to check");
        }
    }
}
