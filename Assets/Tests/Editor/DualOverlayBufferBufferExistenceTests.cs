using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.HexMap;

namespace HexGlobeProject.Tests.Editor
{
    public class DualOverlayBufferBufferExistenceTests
    {
        [Test]
        public void BufferExistsAfterUpload()
        {
            var go = new GameObject("PlanetTestBuf");
            var planet = go.AddComponent<Planet>();
            planet.GeneratePlanet();

            var mr = go.GetComponent<MeshRenderer>();
            Assert.IsNotNull(mr);

            var setOverlay = typeof(Planet).GetMethod("SetOverlayOnMaterials", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(setOverlay);
            setOverlay.Invoke(planet, new object[] { true });

            bool any = false;
            foreach (var m in mr.sharedMaterials)
            {
                if (m == null) continue;
                var buf = DualOverlayBuffer.GetBuffer(m);
                if (buf != null && buf.count > 0) { any = true; break; }
            }

            // Cleanup buffers created during the test to avoid leaking GPU resources in the Editor
            HexGlobeProject.HexMap.DualOverlayBuffer.ReleaseAll();
            GameObject.DestroyImmediate(go);
            Assert.IsTrue(any, "No ComputeBuffer was created for any material");
        }
    }
}
