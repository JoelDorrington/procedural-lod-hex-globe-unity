using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace HexGlobeProject.Tests.Editor
{
    public class PlanetTileColliderWindingTests
    {
        // Helper: applies the same winding-fix logic used in PlanetTileVisibilityManager
        private static void EnsureOutwardWinding(List<int> tris, List<Vector3> verts)
        {
            if (tris.Count < 3) return;
            int aIdx = tris[0];
            int bIdx = tris[1];
            int cIdx = tris[2];
            Vector3 aPos = verts[aIdx];
            Vector3 bPos = verts[bIdx];
            Vector3 cPos = verts[cIdx];
            Vector3 triNormal = Vector3.Cross(bPos - aPos, cPos - aPos).normalized;
            Vector3 avgDir = (aPos.normalized + bPos.normalized + cPos.normalized).normalized;
            if (Vector3.Dot(triNormal, avgDir) < 0f)
            {
                for (int t = 0; t < tris.Count; t += 3)
                {
                    int tmp = tris[t + 1];
                    tris[t + 1] = tris[t + 2];
                    tris[t + 2] = tmp;
                }
            }
        }

        private static float FirstTriangleDot(List<int> tris, List<Vector3> verts)
        {
            int aIdx = tris[0];
            int bIdx = tris[1];
            int cIdx = tris[2];
            Vector3 aPos = verts[aIdx];
            Vector3 bPos = verts[bIdx];
            Vector3 cPos = verts[cIdx];
            Vector3 triNormal = Vector3.Cross(bPos - aPos, cPos - aPos).normalized;
            Vector3 avgDir = (aPos.normalized + bPos.normalized + cPos.normalized).normalized;
            return Vector3.Dot(triNormal, avgDir);
        }

        [Test]
        public void SingleTriangle_InwardGetsFlipped()
        {
            float r = 10f;
            // Choose three points on a face (roughly forming a triangle on sphere)
            var a = new Vector3(1, 0.2f, 0.1f).normalized * r;
            var b = new Vector3(0.3f, 1, 0.2f).normalized * r;
            var c = new Vector3(0.2f, 0.1f, 1).normalized * r;

            var verts = new List<Vector3> { a, b, c };
            // Build tris such that the winding may point inward; try both orders.
            var tris = new List<int> { 0, 2, 1 }; // intentionally reversed ordering

            float before = FirstTriangleDot(tris, verts);
            Assert.Less(before, 0f, "Precondition: constructed triangle should have inward-pointing normal (dot < 0)");

            EnsureOutwardWinding(tris, verts);
            float after = FirstTriangleDot(tris, verts);
            Assert.GreaterOrEqual(after, 0f, "Triangle winding should be flipped so dot >= 0");
        }

        [Test]
        public void SingleTriangle_AlreadyOutwardRemains()
        {
            float r = 10f;
            var a = new Vector3(1, 0.2f, 0.1f).normalized * r;
            var b = new Vector3(0.3f, 1, 0.2f).normalized * r;
            var c = new Vector3(0.2f, 0.1f, 1).normalized * r;

            var verts = new List<Vector3> { a, b, c };
            var tris = new List<int> { 0, 1, 2 }; // correct ordering

            float before = FirstTriangleDot(tris, verts);
            Assert.GreaterOrEqual(before, 0f, "Precondition: triangle should already be outward-pointing");

            EnsureOutwardWinding(tris, verts);
            float after = FirstTriangleDot(tris, verts);
            Assert.GreaterOrEqual(after, 0f, "Triangle should remain outward after ensure routine");
        }

        [Test]
        public void SubdividedTriangleGrid_WindingFixed()
        {
            // Build a small subdivided triangular grid (subdivisions=3) and intentionally
            // produce triangles in a winding that may point inward. After the fix,
            // the first triangle should point outward.
            int subdivisions = 3;
            float r = 50f;
            // base triangle barycentric corners -- unused in this test (kept for clarity)

            var verts = new List<Vector3>();
            var indexMap = new int[subdivisions + 1, subdivisions + 1];
            for (int rr = 0; rr <= subdivisions; rr++) for (int cc = 0; cc <= subdivisions; cc++) indexMap[rr, cc] = -1;
            int vertIdx = 0;
            for (int row = 0; row <= subdivisions; row++)
            {
                for (int col = 0; col <= (subdivisions - row); col++)
                {
                    float s = col / (float)subdivisions;
                    float t = row / (float)subdivisions;
                    float b1 = s; float b2 = t; float b0 = 1f - b1 - b2;
                    // Simple mapping to 3D directions using barycentric to world approx: just use barycentric coords as position
                    Vector3 dir = new Vector3(b1, b2, b0).normalized;
                    verts.Add(dir * r);
                    indexMap[row, col] = vertIdx++;
                }
            }

            var tris = new List<int>();
            for (int row = 0; row < subdivisions; row++)
            {
                for (int col = 0; col <= (subdivisions - row - 1); col++)
                {
                    int a0 = indexMap[row, col];
                    int a1 = indexMap[row + 1, col];
                    int a2 = indexMap[row, col + 1];
                    // Intentionally add in the order that could be inward for some configurations
                    tris.Add(a0); tris.Add(a2); tris.Add(a1);
                    if (col < (subdivisions - row - 1))
                    {
                        int a3 = indexMap[row + 1, col + 1];
                        tris.Add(a2); tris.Add(a3); tris.Add(a1);
                    }
                }
            }

            float before = FirstTriangleDot(tris, verts);
            // If before is already >= 0 it's fine; otherwise we expect the fix to flip it

            EnsureOutwardWinding(tris, verts);
            float after = FirstTriangleDot(tris, verts);
            Assert.GreaterOrEqual(after, 0f, "After ensure routine the first triangle must point outward");
        }

        [Test]
        public void RandomizedTriangles_RobustFlip()
        {
            var rng = new System.Random(12345);
            int trials = 500;
            float r = 20f;
            for (int t = 0; t < trials; t++)
            {
                // Generate three random directions on the sphere
                Vector3 a = RandomUnit(rng) * r;
                Vector3 b = RandomUnit(rng) * r;
                Vector3 c = RandomUnit(rng) * r;
                var verts = new List<Vector3> { a, b, c };

                // Randomly choose winding
                var tris = new List<int> { 0, 1, 2 };
                if (rng.NextDouble() < 0.5) tris = new List<int> { 0, 2, 1 };

                // Occasionally create near-degenerate by nudging two verts close
                if (t % 50 == 0)
                {
                    b = Vector3.Lerp(a, b, 0.001f).normalized * r;
                    verts[1] = b;
                }

                EnsureOutwardWinding(tris, verts);
                float dot = FirstTriangleDot(tris, verts);
                Assert.GreaterOrEqual(dot, -1e-5f, $"Random trial {t} failed to ensure outward winding (dot={dot})");
            }
        }

        [Test]
        public void FlipIsIdempotent()
        {
            float r = 10f;
            var a = new Vector3(1, 0.2f, 0.1f).normalized * r;
            var b = new Vector3(0.3f, 1, 0.2f).normalized * r;
            var c = new Vector3(0.2f, 0.1f, 1).normalized * r;

            var verts = new List<Vector3> { a, b, c };
            var tris = new List<int> { 0, 2, 1 }; // reversed

            EnsureOutwardWinding(tris, verts);
            var firstPass = new List<int>(tris);
            EnsureOutwardWinding(tris, verts);
            CollectionAssert.AreEqual(firstPass, tris, "EnsureOutwardWinding should be idempotent (second pass no-op)");
        }

        // Helper to generate a random unit vector using RNG
        private static Vector3 RandomUnit(System.Random rng)
        {
            // uniform sampling on sphere
            double z = rng.NextDouble() * 2.0 - 1.0;
            double t = rng.NextDouble() * 2.0 * System.Math.PI;
            double r = System.Math.Sqrt(1.0 - z * z);
            float x = (float)(r * System.Math.Cos(t));
            float y = (float)(r * System.Math.Sin(t));
            float zz = (float)z;
            return new Vector3(x, y, zz);
        }
    }
}
