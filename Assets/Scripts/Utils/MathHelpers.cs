using UnityEngine;

public static class MathHelpers
{
    public static float VectorMagnitude(Vector3 vector)
    {
        return Mathf.Sqrt(vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);
    }

    public static Vector3 NormalizeVector(Vector3 vector)
    {
        float magnitude = VectorMagnitude(vector);
        return magnitude > 0 ? vector / magnitude : Vector3.zero;
    }

    public static float AngleBetweenVectors(Vector3 from, Vector3 to)
    {
        float dotProduct = Vector3.Dot(NormalizeVector(from), NormalizeVector(to));
        return Mathf.Acos(dotProduct) * Mathf.Rad2Deg;
    }

    public static Vector3 SphericalToCartesian(float radius, float latitude, float longitude)
    {
        float latRad = latitude * Mathf.Deg2Rad;
        float lonRad = longitude * Mathf.Deg2Rad;

        float x = radius * Mathf.Cos(latRad) * Mathf.Cos(lonRad);
        float y = radius * Mathf.Sin(latRad);
        float z = radius * Mathf.Cos(latRad) * Mathf.Sin(lonRad);

        return new Vector3(x, y, z);
    }

    public static (float latitude, float longitude) CartesianToSpherical(Vector3 point)
    {
        float radius = VectorMagnitude(point);
        float latitude = Mathf.Asin(point.y / radius) * Mathf.Rad2Deg;
        float longitude = Mathf.Atan2(point.z, point.x) * Mathf.Rad2Deg;

        return (latitude, longitude);
    }
}