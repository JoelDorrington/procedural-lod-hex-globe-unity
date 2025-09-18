using System.Collections.Generic;
using UnityEditor.Graphs;
using UnityEngine;

namespace HexGlobeProject.HexMap
{
    /// <summary>
    /// Manages upload of dual-line segments to materials using a ComputeBuffer.
    /// Each segment is two Vector3s stored as Vector4s (w=0) for alignment.
    /// Materials receive a buffer named _DualSegments and an int _DualSegmentCount.
    /// </summary>
    public static class DualOverlayBuffer
    {
    static readonly Dictionary<Material, ComputeBuffer> s_buffers = new Dictionary<Material, ComputeBuffer>();
    static readonly Dictionary<Material, ComputeBuffer> s_boundsBuffers = new Dictionary<Material, ComputeBuffer>();
        // Enable to get upload timing and sample data in the Editor logs
        // Debug logging removed for playtest; uploads are always performed.

        // If 'spaceTransform' is provided, segment endpoints will be transformed into world-space
        // using spaceTransform.TransformPoint for correct shader sampling against world positions.
        public static int UploadSegmentsToMaterial(Material mat, Mesh mesh, float baseRadius, Transform spaceTransform = null)
        {
            if (mat == null || mesh == null) return 0;

            // Extract segments
            // Use raw (unprojected) mesh-space centroids; apply transform explicitly below if provided
            var segments = DualMeshBuilder.ExtractSegments(mesh, baseRadius, true, null);
            int pairCount = segments.Length / 2;

            if (pairCount == 0)
            {
                ReleaseBufferForMaterial(mat);
                mat.SetInt("_DualSegmentCount", 0);
                return 0;
            }

            // Prepare packed segment array for spherical great-circle test: two float4 entries per segment
            // Entry 0: aDir.xyz, dotAB (w)
            // Entry 1: bDir.xyz, 0
            var data = new Vector4[pairCount * 2];
            // Read planet center and base radius from material, default to world origin / baseRadius
            Vector3 planetCenter = Vector3.zero;
            float matBaseRadius = baseRadius;
            try {
                if (mat != null && mat.HasProperty("_PlanetCenter")) { var v = mat.GetVector("_PlanetCenter"); planetCenter = new Vector3(v.x, v.y, v.z); }
                if (mat != null && mat.HasProperty("_BaseRadius")) matBaseRadius = mat.GetFloat("_BaseRadius");
            } catch { }

            // angular margin to account for height variation (radians). We'll add a modest margin.
            float angularMargin = 0.0f; // small extra margin can be tuned via shader if needed
            for (int si = 0; si < pairCount; ++si)
            {
                int sidx = si * 2;
                // endpoints in world space
                Vector3 a = segments[sidx];
                Vector3 b = segments[sidx + 1];
                if (spaceTransform != null)
                {
                    a = spaceTransform.TransformPoint(a);
                    b = spaceTransform.TransformPoint(b);
                }

                // Convert to radial directions relative to planet center and normalize
                Vector3 aDir = (a - planetCenter);
                Vector3 bDir = (b - planetCenter);
                if (aDir.sqrMagnitude <= 1e-12f || bDir.sqrMagnitude <= 1e-12f)
                {
                    // skip degenerate
                    aDir = Vector3.up;
                    bDir = Vector3.up;
                }
                aDir.Normalize();
                bDir.Normalize();

                float dotAB = Mathf.Clamp(Vector3.Dot(aDir, bDir), -1f, 1f);

                data[si * 2] = new Vector4(aDir.x, aDir.y, aDir.z, dotAB);
                data[si * 2 + 1] = new Vector4(bDir.x, bDir.y, bDir.z, 0.0f);
            }

            // Create or replace existing buffer
            if (s_buffers.TryGetValue(mat, out var existing))
            {
                // If size differs, recreate
                if (existing.count != data.Length)
                {
                    existing.Release();
                    s_buffers.Remove(mat);
                    existing = null;
                }
            }

            if (existing == null)
            {
                var buf = new ComputeBuffer(data.Length, sizeof(float) * 4, ComputeBufferType.Default);
                buf.SetData(data);
                s_buffers[mat] = buf;
                mat.SetBuffer("_DualSegments", buf);
            }
            else
            {
                existing.SetData(data);
                mat.SetBuffer("_DualSegments", existing);
            }

            // Compute per-segment coarse angular bounds (midDir.xyz, halfArcAngleRadians)
            var bounds = new Vector4[pairCount];
            for (int si = 0; si < pairCount; ++si)
            {
                int sidx = si * 2;
                var a4 = data[sidx];
                var b4 = data[sidx + 1];
                Vector3 aDir = new Vector3(a4.x, a4.y, a4.z);
                Vector3 bDir = new Vector3(b4.x, b4.y, b4.z);
                float dotAB = a4.w;
                // mid direction on sphere (approximate)
                Vector3 midDir = aDir + bDir;
                if (midDir.sqrMagnitude < 1e-6f) midDir = aDir; else midDir.Normalize();
                // half arc angle
                float arc = Mathf.Acos(dotAB) * 0.5f;
                float coarse = arc + angularMargin + Mathf.Max(0.0f, Mathf.Atan2(0.5f * 0.0f, matBaseRadius));
                bounds[si] = new Vector4(midDir.x, midDir.y, midDir.z, coarse);
            }

            if (s_boundsBuffers.TryGetValue(mat, out var existingBounds))
            {
                if (existingBounds.count != bounds.Length)
                {
                    existingBounds.Release();
                    s_boundsBuffers.Remove(mat);
                    existingBounds = null;
                }
            }

            if (existingBounds == null)
            {
                var bbuf = new ComputeBuffer(bounds.Length, sizeof(float) * 4, ComputeBufferType.Default);
                bbuf.SetData(bounds);
                s_boundsBuffers[mat] = bbuf;
                mat.SetBuffer("_DualSegmentBounds", bbuf);
            }
            else
            {
                existingBounds.SetData(bounds);
                mat.SetBuffer("_DualSegmentBounds", existingBounds);
            }

            // Dump first few samples for manual inspection when debug logging enabled

            mat.SetInt("_DualSegmentCount", pairCount);
            return pairCount;
        }

        public static void ReleaseBufferForMaterial(Material mat)
        {
            if (mat == null) return;
            if (s_buffers.TryGetValue(mat, out var buf))
            {
                try { buf.Release(); } catch { }
                s_buffers.Remove(mat);
            }
            if (s_boundsBuffers.TryGetValue(mat, out var bbuf))
            {
                try { bbuf.Release(); } catch { }
                s_boundsBuffers.Remove(mat);
            }
            mat.SetInt("_DualSegmentCount", 0);
            mat.SetBuffer("_DualSegments", (ComputeBuffer)null);
            mat.SetBuffer("_DualSegmentBounds", (ComputeBuffer)null);
        }

        // Expose buffer for tests/inspection (may return null)
        public static ComputeBuffer GetBuffer(Material mat)
        {
            if (mat == null) return null;
            if (s_buffers.TryGetValue(mat, out var buf)) return buf;
            return null;
        }

        public static ComputeBuffer GetBoundsBuffer(Material mat)
        {
            if (mat == null) return null;
            if (s_boundsBuffers.TryGetValue(mat, out var buf)) return buf;
            return null;
        }

        public static void ReleaseAll()
        {
            // Release and clear segment buffers
            var segMats = new System.Collections.Generic.List<Material>(s_buffers.Keys);
            foreach (var mat in segMats)
            {
                if (mat != null)
                {
                    mat.SetInt("_DualSegmentCount", 0);
                    mat.SetBuffer("_DualSegments", (ComputeBuffer)null);
                    mat.SetBuffer("_DualSegmentBounds", (ComputeBuffer)null);
                }
                if (s_buffers.TryGetValue(mat, out var buf))
                {
                    try { buf.Release(); } catch { }
                }
            }
            s_buffers.Clear();

            // Release and clear bounds buffers
            var boundMats = new System.Collections.Generic.List<Material>(s_boundsBuffers.Keys);
            foreach (var mat in boundMats)
            {
                if (mat != null)
                {
                    mat.SetBuffer("_DualSegmentBounds", (ComputeBuffer)null);
                }
                if (s_boundsBuffers.TryGetValue(mat, out var bbuf))
                {
                    try { bbuf.Release(); } catch { }
                }
            }
            s_boundsBuffers.Clear();
        }
    }
}
