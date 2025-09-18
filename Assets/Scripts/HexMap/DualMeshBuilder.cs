using System.Collections.Generic;
using UnityEngine;

namespace HexGlobeProject.HexMap
{
    /// <summary>
    /// Utility to extract the dual graph (centroid per face) and produce line segments
    /// connecting adjacent face centroids. The output is a flat array of Vector3 pairs
    /// [s0,e0,s1,e1,...] in world-space projected to the provided baseRadius.
    /// </summary>
    public static class DualMeshBuilder
    {
    public static Vector3[] ExtractSegments(Mesh baseMesh, float baseRadius, bool skipProjection = false, Transform spaceTransform = null)
        {
            if (baseMesh == null) return new Vector3[0];
            var tris = baseMesh.triangles;
            var verts = baseMesh.vertices;
            int faceCount = tris.Length / 3;

            var dualVertsRaw = new Vector3[faceCount];
            for (int f = 0; f < faceCount; f++)
            {
                int i0 = tris[3 * f + 0];
                int i1 = tris[3 * f + 1];
                int i2 = tris[3 * f + 2];
                Vector3 a = verts[i0];
                Vector3 b = verts[i1];
                Vector3 c = verts[i2];
                Vector3 p = (a + b + c) / 3f;
                if (spaceTransform != null)
                    p = spaceTransform.TransformPoint(p);
                dualVertsRaw[f] = p;
            }

            long EdgeKey(int a, int b)
            {
                int min = a < b ? a : b;
                int max = a ^ b ^ min;
                return ((long)min << 32) | (uint)max;
            }

            var edgeToFace = new Dictionary<long, int>(faceCount * 3);
            var lineIndices = new List<int>(faceCount * 3);
            for (int f = 0; f < faceCount; f++)
            {
                int i0 = tris[3 * f + 0];
                int i1 = tris[3 * f + 1];
                int i2 = tris[3 * f + 2];
                void AddEdge(int u, int v, int face)
                {
                    long key = EdgeKey(u, v);
                    if (edgeToFace.TryGetValue(key, out int other))
                    {
                        lineIndices.Add(other);
                        lineIndices.Add(face);
                        edgeToFace.Remove(key);
                    }
                    else
                    {
                        edgeToFace[key] = face;
                    }
                }
                AddEdge(i0, i1, f);
                AddEdge(i1, i2, f);
                AddEdge(i2, i0, f);
            }

            // Prepare segment array: each pair of indices becomes two Vector3s (start,end)
            int segCount = lineIndices.Count / 2;
            var segments = new Vector3[segCount * 2];
            for (int i = 0, s = 0; i < lineIndices.Count; i += 2, s += 2)
            {
                var v0 = dualVertsRaw[lineIndices[i]];
                var v1 = dualVertsRaw[lineIndices[i + 1]];
                Vector3 p0, p1;
                if (skipProjection)
                {
                    p0 = v0;
                    p1 = v1;
                }
                else
                {
                    p0 = v0.sqrMagnitude > 1e-12f ? v0.normalized * baseRadius : Vector3.up * baseRadius;
                    p1 = v1.sqrMagnitude > 1e-12f ? v1.normalized * baseRadius : Vector3.up * baseRadius;
                }
                segments[s + 0] = p0;
                segments[s + 1] = p1;
            }

            return segments;
        }
    }
}
