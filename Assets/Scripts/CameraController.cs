using System;
using UnityEngine;

/// <summary>
/// CameraController provides WASD and mouse wheel control so that the camera orbits around a target (the sphere), always facing it.
/// The latitudinal movement is clamped to prevent the camera from coming closer than 10° to the north or south poles,
/// and the zoom is limited with min/max restrictions.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Target and Orbit Settings")]
    // The point around which the camera orbits (typically the center of the sphere)
    public Transform target;
    
    // Spherical coordinates (in degrees for latitude and longitude)
    // Latitude is clamped between -80 and 80 degrees (to keep 10° away from poles)
    public float latitude = 0f;         
    public float longitude = 0f;        

    [Header("Distance Settings")]
    // Current distance from target
    public float distance = 30f;
    private float _proportionalDistance = 0f;
    public float minDistance = 5f;
    public float maxDistance = 80f;
    [Tooltip("Min zoom speed factor (scaled by current distance)")] public float zoomSpeedMin = .5f;
    [Tooltip("Max zoom speed factor (scaled by current distance)")] public float zoomSpeedMax = 20f;
    [Tooltip("Smoothing time for zoom interpolation")] public float zoomSmoothTime = 0.15f;
    private float _targetDistance;
    private float _zoomVelocity;
    // Track last observed external assignment to distance so we can detect
    // when other code sets the public field directly and propagate it to
    // the internal target distance used by smoothing logic.
    private float _lastObservedDistance;

    [Header("Rotation Settings")]
    public float rotationSpeed = 50f; // Degrees per second
    [Tooltip("Roll degrees per second when pressing Q/E")] public float rollSpeed = 60f;
    [Tooltip("Current roll angle (bank) in degrees")] public float roll = 0f;
    [Tooltip("Initial roll angle for reset")] public float initialRoll = 0f;
    [Tooltip("Angular movement speed around sphere for WASD (deg/sec)")] public float moveAngularSpeed = 50f;
    private const float RollResetDuration = 0.4f;
    private bool _rollResetActive;
    private float _rollResetTime;
    private float _rollResetStart;

    [Header("Framing / Auto-Fit")]
    [Tooltip("Optional: approximate radius of target object for auto framing (0 = disabled)")] public float targetRadius = 0f;
    [Tooltip("Press this key to reframe camera to show targetRadius within view")] public KeyCode reframeKey = KeyCode.F;

    public float ProportionalDistance => _proportionalDistance;

    void Start()
    {
        _targetDistance = distance;
    _lastObservedDistance = distance;
    }

    void Update()
    {
        // Current direction from lat/long
        float latRad0 = latitude * Mathf.Deg2Rad;
        float lonRad0 = longitude * Mathf.Deg2Rad;
        Vector3 center = target ? target.position : Vector3.zero;
        Vector3 dir = new Vector3(Mathf.Cos(latRad0) * Mathf.Cos(lonRad0), Mathf.Sin(latRad0), Mathf.Cos(latRad0) * Mathf.Sin(lonRad0)); // normalized

        // WASD relative movement along tangent directions of current camera orientation.
        float angleStep = moveAngularSpeed * Time.deltaTime;
        bool moved = false;
        if (Input.GetKey(KeyCode.W)) { dir = Quaternion.AngleAxis(angleStep, transform.right) * dir; moved = true; }
        if (Input.GetKey(KeyCode.S)) { dir = Quaternion.AngleAxis(-angleStep, transform.right) * dir; moved = true; }
        if (Input.GetKey(KeyCode.A)) { dir = Quaternion.AngleAxis(angleStep, transform.up) * dir; moved = true; }
        if (Input.GetKey(KeyCode.D)) { dir = Quaternion.AngleAxis(-angleStep, transform.up) * dir; moved = true; }

        if (moved)
        {
            dir.Normalize();
            // Derive new lat/long then clamp latitude, recompute dir if clamped.
            latitude = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            longitude = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
            latitude = Mathf.Clamp(latitude, -80f, 80f);
            // Recompute dir if clamped changed latitude
            latRad0 = latitude * Mathf.Deg2Rad;
            lonRad0 = longitude * Mathf.Deg2Rad;
            dir = new Vector3(Mathf.Cos(latRad0) * Mathf.Cos(lonRad0), Mathf.Sin(latRad0), Mathf.Cos(latRad0) * Mathf.Sin(lonRad0));
        }

    // Roll with Q/E (bank camera around its forward axis AFTER positioning & LookAt)
        bool rollKey = false;
        if (Input.GetKey(KeyCode.Q)) { roll -= rollSpeed * Time.deltaTime; rollKey = true; }
        if (Input.GetKey(KeyCode.E)) { roll += rollSpeed * Time.deltaTime; rollKey = true; }
        if (rollKey && _rollResetActive) _rollResetActive = false; // cancel smooth reset if user intervenes
        if (Input.GetKeyDown(KeyCode.R))
        {
            _rollResetActive = true;
            _rollResetTime = 0f;
            _rollResetStart = roll;
        }
        if (_rollResetActive)
        {
            _rollResetTime += Time.deltaTime;
            float t = Mathf.Clamp01(_rollResetTime / RollResetDuration);
            // Smoothstep
            t = t * t * (3f - 2f * t);
            roll = Mathf.Lerp(_rollResetStart, initialRoll, t);
            if (t >= 1f) _rollResetActive = false;
        }

        // Mouse scroll zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        _proportionalDistance = Mathf.InverseLerp(minDistance, maxDistance, distance);
        // If external code assigned to `distance` directly since last frame,
        // treat that as the new requested target distance so external tests
        // and scripts see immediate effect.
        if (!Mathf.Approximately(distance, _lastObservedDistance))
        {
            _targetDistance = distance;
        }
        // scroll is usually between -0.3 and 0.3
        float absScroll = Mathf.Abs(scroll);
        if (absScroll > 0.0001f)
        {
            float distanceScale = Mathf.Lerp(zoomSpeedMin, zoomSpeedMax, _proportionalDistance);
            float scrollStep = scroll * distanceScale;
            _targetDistance -= scrollStep;
            _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
        }

        // Optional reframe
        if (targetRadius > 0f && Input.GetKeyDown(reframeKey) && target != null)
        {
            // Position so that target radius fits vertically in view frustum
            float fovRad = Camera.main != null ? Camera.main.fieldOfView * Mathf.Deg2Rad : 60f * Mathf.Deg2Rad;
            float fitDistance = targetRadius / Mathf.Sin(fovRad * 0.5f);
            _targetDistance = Mathf.Clamp(fitDistance, minDistance, maxDistance);
        }

        // Smooth zoom
        distance = Mathf.SmoothDamp(distance, _targetDistance, ref _zoomVelocity, zoomSmoothTime);

    // Update last observed distance for external-assignment detection
    _lastObservedDistance = distance;

        // Convert spherical coordinates to Cartesian coordinates
        // Use possibly updated (clamped) direction for final position
        Vector3 finalDir = new Vector3(Mathf.Cos(latitude * Mathf.Deg2Rad) * Mathf.Cos(longitude * Mathf.Deg2Rad),
                                       Mathf.Sin(latitude * Mathf.Deg2Rad),
                                       Mathf.Cos(latitude * Mathf.Deg2Rad) * Mathf.Sin(longitude * Mathf.Deg2Rad));
        Vector3 posOffset = finalDir * distance;

        // Update camera position and ensure it looks at the target
        if(target != null)
        {
            transform.position = target.position + posOffset;
            // LookAt first to align view to target maintaining latitude/longitude derived distance to poles
            transform.LookAt(target.position, Vector3.up);
            if (Mathf.Abs(roll) > 0.001f)
            {
                // Apply roll about the forward axis (preserves position & poles distance)
                transform.Rotate(Vector3.forward, roll, Space.Self);
            }
        }
        else
        {
            Debug.LogWarning("CameraController: Target not assigned.");
        }
    }
}
