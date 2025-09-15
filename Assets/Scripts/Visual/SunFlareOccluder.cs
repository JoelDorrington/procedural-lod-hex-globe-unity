using System;
using UnityEngine;

namespace HexGlobeProject.Visual
{
    /// <summary>
    /// Fades a LensFlare's brightness when the target light is occluded by a spherical planet.
    /// Uses a simple ray-sphere intersection test from the active camera to determine occlusion.
    /// </summary>
    [DisallowMultipleComponent]
    public class SunFlareOccluder : MonoBehaviour
    {
        public Light targetLight;
        public LensFlare lensFlare;
        public Transform planetTransform;
        public float planetRadius = 30f;
        [Tooltip("How quickly the flare brightness interpolates")]
        public float fadeSpeed = 99999f;

        // Desired maximum brightness when not occluded. If using LensFlare component, we'll read/restore from it.
        private float maxBrightness = 1f;
        private float currentBrightness = 0f;

        void Start()
        {
            if (lensFlare == null)
            {
                lensFlare = GetComponent<LensFlare>();
            }
            if (targetLight == null)
            {
                targetLight = GetComponent<Light>();
            }
            if (lensFlare != null)
            {
                maxBrightness = lensFlare.brightness;
                currentBrightness = maxBrightness;
            }
        }

        void Update()
        {
            try
            {
                var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
                if (cam == null || targetLight == null || lensFlare == null || planetTransform == null)
                {
                    // nothing to do
                    return;
                }

                // Use explicit world positions from transforms
                Vector3 camPos = cam.transform.position;
                Vector3 planetCenter = planetTransform.position;
                // Use the light GameObject's world position as the visual source of the flare
                Vector3 lightPos = targetLight.transform.position;
                Vector3 dir = lightPos - camPos;
                float distToLight = dir.magnitude;
                if (distToLight <= 1e-6f)
                {
                    // degenerate: treat as visible
                    TargetBrightness(maxBrightness, fadeSpeed);
                    return;
                }
                Vector3 dirNorm = dir / distToLight;
                // Ray-sphere intersection (quadratic): solve for t in |o + t*d - c|^2 = r^2
                // where o = camPos, d = dirNorm, c = planetCenter, r = worldRadius
                // Use Transform.lossyScale to account for world scaling of the planet radius
                float scaleFactor = 1f;
                try
                {
                    scaleFactor = Mathf.Max(Mathf.Abs(planetTransform.lossyScale.x), Mathf.Abs(planetTransform.lossyScale.y), Mathf.Abs(planetTransform.lossyScale.z));
                }
                catch { scaleFactor = 1f; }
                float worldRadius = planetRadius * scaleFactor;

                Vector3 oc = camPos - planetCenter; // vector from sphere center to ray origin
                float a = Vector3.Dot(dirNorm, dirNorm); // should be 1 since dirNorm is normalized
                float b = 2f * Vector3.Dot(dirNorm, oc);
                float c = Vector3.Dot(oc, oc) - worldRadius * worldRadius;
                bool occluded = false;
                float tCandidate = float.NaN;

                float discriminant = b * b - 4f * a * c;
                if (discriminant >= 0f)
                {
                    float sqrtD = Mathf.Sqrt(discriminant);
                    // two roots (near, far)
                    float t0 = (-b - sqrtD) / (2f * a);
                    float t1 = (-b + sqrtD) / (2f * a);
                    // we consider intersection if either root is within (0, distToLight)
                    // prefer the nearest positive root
                    float tNear = Mathf.Min(t0, t1);
                    float tFar = Mathf.Max(t0, t1);
                    float tHit = float.NaN;
                    if (tNear > 0f) tHit = tNear; else if (tFar > 0f) tHit = tFar;
                    if (!float.IsNaN(tHit) && tHit < distToLight) { occluded = true; tCandidate = tHit; }
                }

                // Only update brightness when state changes to reduce noise; but always lerp towards the desired target
                float target = occluded ? 0f : maxBrightness;
                TargetBrightness(target, fadeSpeed);
            }
            catch { }
        }

        private void TargetBrightness(float target, float speed)
        {
            currentBrightness = Mathf.MoveTowards(currentBrightness, target, speed * Time.deltaTime);
            try
            {
                if (lensFlare != null) lensFlare.brightness = currentBrightness;
            }
            catch { }
        }
    }
}
