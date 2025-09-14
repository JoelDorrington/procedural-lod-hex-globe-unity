using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexGlobeProject.HexMap.Model
{
    // Fast directional lookup: quantize a unit direction to a face/u/v bucket and return a representative cell index.
    // This is a pragmatic, approximate method suitable for quick selection. It relies on TopologyResult.centers to seed buckets.
    public class DirectionalCellLookup
    {
        private readonly int bucketsPerAxis;
        private readonly int faceCount = 6; // cube faces approximation
        private readonly int totalBuckets;
        private readonly List<int>[] bucketLists; // per-bucket list of cell indices
        private readonly Vector3[] centers;

        public DirectionalCellLookup(TopologyResult topo, int bucketsPerAxis = 64)
        {
            if (topo == null) throw new ArgumentNullException(nameof(topo));
            this.bucketsPerAxis = Math.Max(8, bucketsPerAxis);
            centers = topo.centers;
            totalBuckets = faceCount * this.bucketsPerAxis * this.bucketsPerAxis;
            bucketLists = new List<int>[totalBuckets];
            for (int i = 0; i < totalBuckets; i++) bucketLists[i] = new List<int>();

            // bucket each center by face and projected uv
            for (int i = 0; i < centers.Length; i++)
            {
                Vector3 dir = centers[i].normalized;
                int face = MajorAxisFace(dir);
                (float u, float v) = FaceUV(face, dir);
                int bu = Quantize(u, this.bucketsPerAxis);
                int bv = Quantize(v, this.bucketsPerAxis);
                int bid = BucketId(face, bu, bv);
                bucketLists[bid].Add(i);
            }
        }

        // Returns single nearest cell index or -1 if none.
        public int Lookup(Vector3 dir)
        {
            if (centers == null || centers.Length == 0) return -1;
            dir = dir.normalized;
            int face = MajorAxisFace(dir);
            (float u, float v) = FaceUV(face, dir);
            int bu = Quantize(u, bucketsPerAxis);
            int bv = Quantize(v, bucketsPerAxis);
            int bid = BucketId(face, bu, bv);
            var list = bucketLists[bid];
            if (list.Count == 0)
            {
                // fallback: search nearby buckets (spiral) up to radius
                int maxr = Math.Max(1, bucketsPerAxis/8);
                for (int r=1;r<=maxr;r++)
                {
                    for (int du=-r;du<=r;du++) for (int dv=-r;dv<=r;dv++)
                    {
                        int nbU = bu + du; int nbV = bv + dv;
                        if (nbU < 0 || nbU >= bucketsPerAxis || nbV < 0 || nbV >= bucketsPerAxis) continue;
                        int nid = BucketId(face, nbU, nbV);
                        if (bucketLists[nid].Count>0) { list = bucketLists[nid]; goto found;
                        }
                    }
                }
                return -1;
            }
        found:
            // find nearest center in the bucket
            float best = float.MaxValue; int bestIdx = -1;
            foreach (var i in list)
            {
                float d = Vector3.SqrMagnitude(centers[i].normalized - dir);
                if (d < best) { best = d; bestIdx = i; }
            }
            return bestIdx;
        }

        private int BucketId(int face, int bu, int bv) => face * bucketsPerAxis * bucketsPerAxis + bu * bucketsPerAxis + bv;

        private int Quantize(float v, int n)
        {
            int q = (int)(v * n);
            if (q < 0) q = 0; if (q >= n) q = n-1; return q;
        }

        // Map a unit direction to a cube face index 0..5 (approximation)
        private int MajorAxisFace(Vector3 d)
        {
            float ax = Math.Abs(d.x), ay = Math.Abs(d.y), az = Math.Abs(d.z);
            if (ax >= ay && ax >= az) return d.x > 0 ? 0 : 1; // +X, -X
            if (ay >= ax && ay >= az) return d.y > 0 ? 2 : 3; // +Y, -Y
            return d.z > 0 ? 4 : 5; // +Z, -Z
        }

        // Project onto face-local UV coords in 0..1 range
        private (float u, float v) FaceUV(int face, Vector3 d)
        {
            float u=0,v=0;
            switch(face)
            {
                case 0: // +X
                    u = ( -d.z / Math.Abs(d.x) + 1f) * 0.5f;
                    v = (  d.y / Math.Abs(d.x) + 1f) * 0.5f; break;
                case 1: // -X
                    u = (  d.z / Math.Abs(d.x) + 1f) * 0.5f;
                    v = (  d.y / Math.Abs(d.x) + 1f) * 0.5f; break;
                case 2: // +Y
                    u = (  d.x / Math.Abs(d.y) + 1f) * 0.5f;
                    v = ( -d.z / Math.Abs(d.y) + 1f) * 0.5f; break;
                case 3: // -Y
                    u = (  d.x / Math.Abs(d.y) + 1f) * 0.5f;
                    v = (  d.z / Math.Abs(d.y) + 1f) * 0.5f; break;
                case 4: // +Z
                    u = (  d.x / Math.Abs(d.z) + 1f) * 0.5f;
                    v = (  d.y / Math.Abs(d.z) + 1f) * 0.5f; break;
                case 5: // -Z
                    u = ( -d.x / Math.Abs(d.z) + 1f) * 0.5f;
                    v = (  d.y / Math.Abs(d.z) + 1f) * 0.5f; break;
            }
            return (u,v);
        }
    }
}
