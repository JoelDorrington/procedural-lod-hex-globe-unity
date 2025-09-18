using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HexGlobeProject.HexMap;

namespace HexGlobeProject.Tests.Editor
{
    public class DualOverlayBufferBoundsTests
    {
        [Test]
        public void UploadsSegmentBoundsBuffer_WhenOverlayEnabled()
        {
            // Create a temporary GameObject with a Planet component
            var go = new GameObject("_TestPlanet");
            var planet = go.AddComponent<Planet>();

            // generate a default planet mesh
            planet.GeneratePlanet();

            // Enable overlay (this should cause the DualOverlayBuffer to upload buffers)
            var setOverlayMethod = typeof(Planet).GetMethod("SetOverlayOnMaterials", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(setOverlayMethod, "Could not find SetOverlayOnMaterials method on Planet via reflection");
            setOverlayMethod.Invoke(planet, new object[] { true });

            // Iterate materials on the planet's renderer(s) and assert buffers exist
            var rends = go.GetComponentsInChildren<Renderer>(true);
            int found = 0;
            foreach (var r in rends)
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    var segBuf = DualOverlayBuffer.GetBuffer(mat);
                    var boundsBuf = DualOverlayBuffer.GetBoundsBuffer(mat);
                    if (segBuf != null || boundsBuf != null)
                    {
                        found++;
                        Assert.IsNotNull(segBuf, "Segment buffer should not be null when overlay enabled");
                        Assert.IsNotNull(boundsBuf, "Bounds buffer should not be null when overlay enabled");
                        Assert.Greater(mat.GetInt("_DualSegmentCount"), 0, "_DualSegmentCount should be > 0");
                    }
                }
            }

            // cleanup
            DualOverlayBuffer.ReleaseAll();
            Object.DestroyImmediate(go);

            Assert.Greater(found, 0, "Test scene should contain at least one material that received buffers");
        }
    }
}