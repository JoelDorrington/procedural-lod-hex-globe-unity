using UnityEngine;
using System.Collections.Generic;

namespace HexGlobeProject.Graphics.DataStructures
{
    /// <summary>
    /// Generates an icosphere mesh with a specified radius and subdivision level.
    /// </summary>
    public class IcosphereGenerator
    {
        public static Mesh GenerateIcosphere(float radius = 1f, int subdivisions = 2)
        {

            Mesh mesh = new Mesh();

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            // Build base icosahedron
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;

            int AddVertex(Vector3 p)
            {
                vertices.Add(p.normalized * radius);
                return vertices.Count - 1;
            }

            // 1. Generate initial icosahedron vertices (12 vertices)
            AddVertex(new Vector3(-1f, t, 0f));
            AddVertex(new Vector3(1f, t, 0f));
            AddVertex(new Vector3(-1f, -t, 0f));
            AddVertex(new Vector3(1f, -t, 0f));
            AddVertex(new Vector3(0f, -1f, t));
            AddVertex(new Vector3(0f, 1f, t));
            AddVertex(new Vector3(0f, -1f, -t));
            AddVertex(new Vector3(0f, 1f, -t));
            AddVertex(new Vector3(t, 0f, -1f));
            AddVertex(new Vector3(t, 0f, 1f));
            AddVertex(new Vector3(-t, 0f, -1f));
            AddVertex(new Vector3(-t, 0f, 1f));

            // 2. Define initial 20 triangles based on these vertices
            List<int> faces = new List<int>
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };

            var middlePointCache = new Dictionary<long, int>();

            long Key(int a, int b)
            {
                int min = a < b ? a : b;
                int max = a < b ? b : a;
                return ((long)min << 32) | (uint)max;
            }

            int GetMiddlePoint(int a, int b)
            {
                long key = Key(a, b);
                if (middlePointCache.TryGetValue(key, out int idx))
                    return idx;

                Vector3 p = (vertices[a] + vertices[b]) * 0.5f;
                int i = vertices.Count;
                vertices.Add(p.normalized * radius);
                middlePointCache[key] = i;
                return i;
            }

            // 3. Subdivide the triangles recursively based on 'subdivisions'
            //    - For each triangle, calculate midpoints of edges
            //    - Normalize midpoints to 'radius'
            //    - Add new vertices and update triangles
            for (int i = 0; i < subdivisions; i++)
            {
                var newFaces = new List<int>(faces.Count * 4);
                for (int f = 0; f < faces.Count; f += 3)
                {
                    int a = faces[f];
                    int b = faces[f + 1];
                    int c = faces[f + 2];

                    int ab = GetMiddlePoint(a, b);
                    int bc = GetMiddlePoint(b, c);
                    int ca = GetMiddlePoint(c, a);

                    newFaces.Add(a); newFaces.Add(ab); newFaces.Add(ca);
                    newFaces.Add(b); newFaces.Add(bc); newFaces.Add(ab);
                    newFaces.Add(c); newFaces.Add(ca); newFaces.Add(bc);
                    newFaces.Add(ab); newFaces.Add(bc); newFaces.Add(ca);
                }
                faces = newFaces;
            }

            triangles.Clear();
            triangles.AddRange(faces);

            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            // 4. Assign data to the mesh
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            return mesh;
        }
        // Helper methods for calculating icosahedron vertices and subdivision logic
    }
}