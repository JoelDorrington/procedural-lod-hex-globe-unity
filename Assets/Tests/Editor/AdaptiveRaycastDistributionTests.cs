using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    /// <summary>
    /// Unit tests for the adaptive raycast distribution feature.
    /// Tests verify that rays are concentrated within the planet's visible viewport bounds
    /// and that the distribution adapts based on camera distance and field of view.
    /// </summary>
    public class AdaptiveRaycastDistributionTests
    {
        [Test]
        public void SphereViewportRadius_ShouldBeZero_WhenCameraInsidePlanet()
        {
            // Arrange: camera inside planet
            float planetRadius = 100f;
            float cameraDistance = 50f; // inside planet
            float cameraFOV = 60f;
            
            // Act: calculate sphere viewport radius
            float sphereViewportRadius = CalculateSphereViewportRadius(cameraDistance, planetRadius, cameraFOV);
            
            // Assert: should be zero when camera is inside planet
            Assert.AreEqual(0f, sphereViewportRadius, 0.001f, "Sphere viewport radius should be 0 when camera is inside planet");
        }
        
        [Test]
        public void SphereViewportRadius_ShouldIncrease_WhenCameraMovesAway()
        {
            // Arrange: camera at different distances
            float planetRadius = 100f;
            float cameraFOV = 60f;
            float closeDistance = 120f;
            float farDistance = 500f;
            
            // Act: calculate viewport radius at different distances
            float closeRadius = CalculateSphereViewportRadius(closeDistance, planetRadius, cameraFOV);
            float farRadius = CalculateSphereViewportRadius(farDistance, planetRadius, cameraFOV);
            
            // Assert: viewport radius should be larger when farther away (planet appears smaller)
            Assert.Greater(closeRadius, farRadius, "Sphere should have larger viewport radius when camera is closer");
            Assert.Greater(closeRadius, 0f, "Close viewport radius should be positive");
            Assert.Greater(farRadius, 0f, "Far viewport radius should be positive");
        }
        
        [Test]
        public void SphereViewportRadius_ShouldRespectFieldOfView()
        {
            // Arrange: same distance, different FOVs
            float planetRadius = 100f;
            float cameraDistance = 200f;
            float narrowFOV = 30f;
            float wideFOV = 90f;
            
            // Act: calculate viewport radius with different FOVs
            float narrowRadius = CalculateSphereViewportRadius(cameraDistance, planetRadius, narrowFOV);
            float wideRadius = CalculateSphereViewportRadius(cameraDistance, planetRadius, wideFOV);
            
            // Assert: planet should appear larger in viewport with narrower FOV
            Assert.Greater(narrowRadius, wideRadius, "Planet should have larger viewport radius with narrower FOV");
        }
        
        [Test]
        public void SphereViewportRadius_ShouldBeClampedToOne()
        {
            // Arrange: very close camera that would result in viewport radius > 1
            float planetRadius = 100f;
            float veryCloseDistance = 101f; // just outside planet
            float wideFOV = 120f;
            
            // Act: calculate viewport radius
            float radius = CalculateSphereViewportRadius(veryCloseDistance, planetRadius, wideFOV);
            
            // Assert: should be clamped to 1.0
            Assert.LessOrEqual(radius, 1.0f, "Sphere viewport radius should never exceed 1.0");
        }
        
        [Test]
        public void AdaptiveRayDistribution_ShouldConcentrateRaysWithinSphere()
        {
            // Arrange: setup for ray distribution test
            int totalRays = 100;
            float sphereViewportRadius = 0.5f; // planet covers half the viewport
            float biasStrength = 1.0f; // full bias
            var rayPositions = new List<Vector2>();
            
            // Act: generate ray viewport positions using adaptive distribution
            for (int i = 0; i < totalRays; i++)
            {
                float u = (i % 10 + 0.5f) / 10f; // 10x10 grid
                float v = (i / 10 + 0.5f) / 10f;
                Vector2 adaptedPos = ApplyAdaptiveDistribution(u, v, sphereViewportRadius, biasStrength);
                rayPositions.Add(adaptedPos);
            }
            
            // Assert: most rays should be within the sphere bounds
            int raysWithinSphere = 0;
            foreach (var pos in rayPositions)
            {
                float centerDistance = Vector2.Distance(pos, Vector2.one * 0.5f); // distance from viewport center
                if (centerDistance <= sphereViewportRadius)
                {
                    raysWithinSphere++;
                }
            }
            
            float concentration = (float)raysWithinSphere / totalRays;
            Assert.Greater(concentration, 0.7f, "At least 70% of rays should be concentrated within sphere bounds");
        }
        
        [Test]
        public void AdaptiveRayDistribution_ShouldReduceBias_WhenBiasStrengthIsLow()
        {
            // Arrange: same setup with different bias strengths
            float u = 0.2f, v = 0.3f; // edge sample
            float sphereViewportRadius = 0.5f;
            float noBias = 0.0f;
            float fullBias = 1.0f;
            
            // Act: apply distribution with different bias strengths
            Vector2 noBiasPos = ApplyAdaptiveDistribution(u, v, sphereViewportRadius, noBias);
            Vector2 fullBiasPos = ApplyAdaptiveDistribution(u, v, sphereViewportRadius, fullBias);
            
            // Assert: no bias should preserve original position, full bias should move toward center
            Assert.AreEqual(u, noBiasPos.x, 0.001f, "No bias should preserve original U coordinate");
            Assert.AreEqual(v, noBiasPos.y, 0.001f, "No bias should preserve original V coordinate");
            
            float noBiasDistance = Vector2.Distance(noBiasPos, Vector2.one * 0.5f);
            float fullBiasDistance = Vector2.Distance(fullBiasPos, Vector2.one * 0.5f);
            Assert.Less(fullBiasDistance, noBiasDistance, "Full bias should move rays closer to viewport center");
        }
        
        // Helper methods that implement the adaptive distribution logic to be tested
        private float CalculateSphereViewportRadius(float cameraDistance, float planetRadius, float cameraFOV)
        {
            if (cameraDistance <= planetRadius || planetRadius <= 0f)
                return 0f;
            
            float angularRadius = Mathf.Asin(planetRadius / cameraDistance);
            float vFovHalf = cameraFOV * Mathf.Deg2Rad * 0.5f;
            float sphereViewportRadius = Mathf.Tan(angularRadius) / Mathf.Tan(vFovHalf);
            return Mathf.Clamp(sphereViewportRadius, 0f, 1.0f);
        }
        
        private Vector2 ApplyAdaptiveDistribution(float u, float v, float sphereViewportRadius, float biasStrength)
        {
            if (biasStrength <= 0f || sphereViewportRadius <= 0f)
                return new Vector2(u, v);
            
            // Convert to centered coordinates [-0.5, 0.5]
            float centeredU = u - 0.5f;
            float centeredV = v - 0.5f;
            float currentRadius = Mathf.Sqrt(centeredU * centeredU + centeredV * centeredV);
            
            if (currentRadius > 0f)
            {
                // Normalize the sample radius using max possible radius for centered square
                float maxSampleRadius = 0.5f * Mathf.Sqrt(2f);
                float normalizedRadius = Mathf.Clamp01(currentRadius / maxSampleRadius);
                
                // Apply bias exponent (this should be configurable, using 2.0 as default)
                float radialBiasExponent = 2.0f;
                float biasedNormalized = Mathf.Pow(normalizedRadius, radialBiasExponent);
                
                // Blend between original and biased radii
                float blendedNormalized = Mathf.Lerp(normalizedRadius, biasedNormalized, biasStrength);
                
                // Map to viewport space using sphere viewport radius
                float targetViewportRadius = blendedNormalized * sphereViewportRadius;
                
                // Scale the current vector
                float scale = targetViewportRadius / currentRadius;
                if (float.IsFinite(scale))
                {
                    centeredU *= scale;
                    centeredV *= scale;
                }
                
                u = centeredU + 0.5f;
                v = centeredV + 0.5f;
                
                // Clamp to valid viewport range
                u = Mathf.Clamp01(u);
                v = Mathf.Clamp01(v);
            }
            
            return new Vector2(u, v);
        }
    }
}
