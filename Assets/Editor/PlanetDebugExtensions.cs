using System.Reflection;
using UnityEngine;
using HexGlobeProject.HexMap;

public static class PlanetDebugExtensions
{
    public static float GetBaseRadiusForDebug(this Planet p)
    {
        if (p == null) return 1f;

        // Try common field/property names via reflection
        var t = typeof(Planet);
        var fi = t.GetField("_baseRadius", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (fi != null && fi.FieldType == typeof(float)) return (float)fi.GetValue(p);
        var pi = t.GetProperty("BaseRadius", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (pi != null && pi.PropertyType == typeof(float)) return (float)pi.GetValue(p);

        // Fallback: try MeshFilter bounds on child
        var mf = p.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            var bounds = mf.sharedMesh.bounds;
            return Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        }

        // final fallback
        return 1f;
    }
}
