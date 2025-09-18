using UnityEngine;
using HexGlobeProject.Graphics.DataStructures;

#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using System.Threading.Tasks;
// Generates a low-resolution cubemap where the red channel encodes edge strength for great-circle segments.
// This is a simple CPU rasterizer: for each cubemap texel we compute its direction and evaluate distance to every segment.
// It's intentionally straightforward and safe for editor-time precomputation.
public class OverlayCubemapGenerator : MonoBehaviour
{
    [Tooltip("Cubemap face size (per face). 64-256 recommended.")]
    public int faceSize = 128;

    [Tooltip("Line thickness in world units (same semantics as shader _LineThickness)")]
    public float lineThickness = 0.05f;

    [Tooltip("Base planet radius used to convert thickness to angular tolerance")] 
    public float baseRadius = 30.0f;

    [Tooltip("If true, the generated cubemap will be saved as an asset in the project (Editor only)")]
    public bool saveAsset = true;

    [Tooltip("Name for saved cubemap asset (Editor only)")]
    public string assetName = "DualOverlayCube";
    [Tooltip("Hex cell side length in UV space (0..1). Recommended ~0.03-0.08")] 
    public float hexSize = 0.05f;

    [Tooltip("Edge thickness in UV space (0..1)")]
    public float edgeThickness = 0.007f;
    [Tooltip("Number of samples along each great-circle arc when building UV boxes (higher = fewer misses, slower precompute)")]
    public int arcSampleCount = 16;

    [Tooltip("If true, rasterize each candidate box directly before the per-texel pass (guarantees no corner misses)")]
    public bool rasterizeBoxes = true;

    [Tooltip("Padding added to box UV extents (0..1) to be safe around seams")]
    public float boxPadding = 0.05f;

    public List<int> GetDualMeshOverlaySegments(float radius, int subdivisions)
    {
        Mesh mesh = IcosphereGenerator.GenerateIcosphere(radius, subdivisions);
        var tris = mesh.triangles;
        var verts = mesh.vertices;
        int faceCount = tris.Length / 3;

        // Compute raw dual vertices (no normalization), then optionally project/blend to sphere for display
        var dualVertsRaw = new Vector3[faceCount];
        for (int f = 0; f < faceCount; f++)
        {
            int i0 = tris[3 * f + 0];
            int i1 = tris[3 * f + 1];
            int i2 = tris[3 * f + 2];
            Vector3 a = verts[i0];
            Vector3 b = verts[i1];
            Vector3 c = verts[i2];

            Vector3 p = (a + b + c) / 3f; // centroid-only for best regularity
            dualVertsRaw[f] = p;
        }

        // Map each undirected edge to the two faces that share it, then connect their centroids
        long EdgeKey(int a, int b)
        {
            int min = a < b ? a : b;
            int max = a ^ b ^ min; // faster: other
            return ((long)min << 32) | (uint)max;
        }

        var edgeToFace = new Dictionary<long, int>(faceCount * 3);
        var lineIndices = new List<int>(faceCount * 3); // ~3F indices (E*2, with E≈3F/2)
        var neighbors = new List<int>[faceCount];
        for (int i = 0; i < faceCount; i++) neighbors[i] = new List<int>(6);
        for (int f = 0; f < faceCount; f++)
        {
            int a = tris[3 * f + 0];
            int b = tris[3 * f + 1];
            int c = tris[3 * f + 2];

            void AddEdge(int u, int v, int face)
            {
                long key = EdgeKey(u, v);
                if (edgeToFace.TryGetValue(key, out int other))
                {
                    // Found the second face; connect centroids
                    lineIndices.Add(other);
                    lineIndices.Add(face);
                    // Build adjacency
                    if (neighbors[other].Count == 0 || neighbors[other][neighbors[other].Count - 1] != face) neighbors[other].Add(face);
                    if (neighbors[face].Count == 0 || neighbors[face][neighbors[face].Count - 1] != other) neighbors[face].Add(other);
                    // optional: remove to keep dict small
                    edgeToFace.Remove(key);
                }
                else
                {
                    edgeToFace[key] = face;
                }
            }

            AddEdge(a, b, f);
            AddEdge(b, c, f);
            AddEdge(c, a, f);
        }
        return lineIndices;
    }

    // Public entry to generate a Cubemap. Returns the generated cubemap (not assigned to material automatically).
    public Cubemap GenerateCubemap(float radius, int subdivisions, int resolution)
    {
        int size = Mathf.Max(4, resolution);
        Cubemap cube = new Cubemap(size, TextureFormat.RGBA32, false);

        var lineIndices = GetDualMeshOverlaySegments(radius, subdivisions);

        // Recompute dual vertex centroids so we can map indices to 3D directions
        Mesh mesh = IcosphereGenerator.GenerateIcosphere(radius, subdivisions);
        var tris = mesh.triangles;
        var verts = mesh.vertices;
        int faceCount = tris.Length / 3;
        var dualVerts = new Vector3[faceCount];
        for (int f = 0; f < faceCount; f++)
        {
            int i0 = tris[3 * f + 0];
            int i1 = tris[3 * f + 1];
            int i2 = tris[3 * f + 2];
            dualVerts[f] = ((verts[i0] + verts[i1] + verts[i2]) / 3f).normalized; // directions
        }

        // Build segment pairs as direction vectors (unit)
        var segments = new List<(Vector3 a, Vector3 b)>();
        for (int i = 0; i + 1 < lineIndices.Count; i += 2)
        {
            int ia = lineIndices[i];
            int ib = lineIndices[i + 1];
            if (ia < 0 || ia >= dualVerts.Length || ib < 0 || ib >= dualVerts.Length) continue;
            segments.Add((dualVerts[ia], dualVerts[ib]));
        }

        // angular threshold (radians approximate) for thickness -> face-space units
        float angularThresh = Mathf.Max(1e-6f, lineThickness / Mathf.Max(1e-5f, baseRadius));
        float soft = Mathf.Max(0.001f, angularThresh * 0.5f);

        // Precompute per-face candidate lists using tighter spherical caps per segment (slerp midpoint,
        // half-arc radius + angularThresh). Also precompute cos(capRadius) for fast dot tests.
        // Additionally compute per-face texel bounding boxes for each candidate to skip large swathes of texels.
        var faceBoxes = new List<CandidateBox>[6];
        for (int i = 0; i < 6; ++i) faceBoxes[i] = new List<CandidateBox>();
        var capCenters = new Vector3[segments.Count];
        var capCos = new float[segments.Count];
        for (int si = 0; si < segments.Count; ++si)
        {
            var seg = segments[si];
            Vector3 a = seg.a.normalized;
            Vector3 b = seg.b.normalized;
            // Spherical midpoint gives a tighter center along the arc
            float dab = Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f);
            float arc = Mathf.Acos(dab);
            Vector3 capCenter = (arc < 1e-6f) ? a : Vector3.Slerp(a, b, 0.5f).normalized;
            float capRadius = 0.5f * arc + angularThresh;
            capCenters[si] = capCenter;
            capCos[si] = Mathf.Cos(capRadius);

            // For robustness at seams/corners: sample multiple points along the great-circle arc and project each sample
            // into every face; accumulate per-face min/max UV for samples whose UV lands inside the face.
            int samples = Mathf.Max(1, arcSampleCount);
            float[] minUPerFace = new float[6];
            float[] maxUPerFace = new float[6];
            float[] minVPerFace = new float[6];
            float[] maxVPerFace = new float[6];
            bool[] anyPerFace = new bool[6];
            for (int fidx = 0; fidx < 6; ++fidx)
            {
                minUPerFace[fidx] = 1f; maxUPerFace[fidx] = -1f; minVPerFace[fidx] = 1f; maxVPerFace[fidx] = -1f; anyPerFace[fidx] = false;
            }
            for (int siu = 0; siu <= samples; ++siu)
            {
                float t = (float)siu / (float)samples;
                Vector3 sampleDir = Vector3.Slerp(a, b, t).normalized;
                // project this sample into every face and accept if uv in [-1..1]
                for (int fidx = 0; fidx < 6; ++fidx)
                {
                    CubemapFace fface = fidx == 0 ? CubemapFace.PositiveX : fidx == 1 ? CubemapFace.NegativeX : fidx == 2 ? CubemapFace.PositiveY : fidx == 3 ? CubemapFace.NegativeY : fidx == 4 ? CubemapFace.PositiveZ : CubemapFace.NegativeZ;
                    float su, sv;
                    DirectionToFaceUV(sampleDir, fface, out su, out sv);
                    if (su >= -1f && su <= 1f && sv >= -1f && sv <= 1f)
                    {
                        anyPerFace[fidx] = true;
                        minUPerFace[fidx] = Mathf.Min(minUPerFace[fidx], su);
                        maxUPerFace[fidx] = Mathf.Max(maxUPerFace[fidx], su);
                        minVPerFace[fidx] = Mathf.Min(minVPerFace[fidx], sv);
                        maxVPerFace[fidx] = Mathf.Max(maxVPerFace[fidx], sv);
                    }
                }
            }
            // For each face that had samples, create the candidate box from accumulated UV ranges
            for (int fidx = 0; fidx < 6; ++fidx)
            {
                if (!anyPerFace[fidx]) continue;
                float minU = Mathf.Clamp(minUPerFace[fidx] - Mathf.Clamp01(boxPadding), -1f, 1f);
                float maxU = Mathf.Clamp(maxUPerFace[fidx] + Mathf.Clamp01(boxPadding), -1f, 1f);
                float minV = Mathf.Clamp(minVPerFace[fidx] - Mathf.Clamp01(boxPadding), -1f, 1f);
                float maxV = Mathf.Clamp(maxVPerFace[fidx] + Mathf.Clamp01(boxPadding), -1f, 1f);
                int xMin = Mathf.Clamp(Mathf.FloorToInt(((minU + 1f) * 0.5f) * size - 0.5f), 0, size - 1);
                int xMax = Mathf.Clamp(Mathf.CeilToInt(((maxU + 1f) * 0.5f) * size - 0.5f), 0, size - 1);
                int yMin = Mathf.Clamp(Mathf.FloorToInt(((minV + 1f) * 0.5f) * size - 0.5f), 0, size - 1);
                int yMax = Mathf.Clamp(Mathf.CeilToInt(((maxV + 1f) * 0.5f) * size - 0.5f), 0, size - 1);
                if (xMax >= xMin && yMax >= yMin)
                {
                    faceBoxes[fidx].Add(new CandidateBox { segIndex = si, xMin = xMin, xMax = xMax, yMin = yMin, yMax = yMax });
                }
            }
        }

        // Build quadtree roots for each face (serial)
        QuadNode[] roots = new QuadNode[6];
        for (int fi = 0; fi < 6; ++fi)
        {
            roots[fi] = new QuadNode(0, 0, size - 1, size - 1);
            var boxes = faceBoxes[fi];
            for (int bi = 0; bi < boxes.Count; ++bi) roots[fi].Insert(boxes[bi], 0);
        }

        // Prepare storage for per-face results
        Color[][] faceColors = new Color[6][];
        bool[][] facePrefilled = new bool[6][];

        // Parallelize face rasterization (safe since we only use local arrays and math)
        Parallel.For(0, 6, fi =>
        {
            var face = fi == 0 ? CubemapFace.PositiveX : fi == 1 ? CubemapFace.NegativeX : fi == 2 ? CubemapFace.PositiveY : fi == 3 ? CubemapFace.NegativeY : fi == 4 ? CubemapFace.PositiveZ : CubemapFace.NegativeZ;
            if (face == CubemapFace.Unknown) return;

            Color[] colors = new Color[size * size];
            bool[] prefilled = new bool[size * size];

            // Optional per-box rasterization pass: iterate candidate boxes and rasterize segment distances into the box
            if (rasterizeBoxes)
            {
                var boxes = faceBoxes[fi];
                for (int bi = 0; bi < boxes.Count; ++bi)
                {
                    var cb = boxes[bi];
                    int sidx = cb.segIndex;
                    var seg = segments[sidx];
                    for (int yy = cb.yMin; yy <= cb.yMax; ++yy)
                    {
                        for (int xx = cb.xMin; xx <= cb.xMax; ++xx)
                        {
                            int idx = yy * size + xx;
                            Vector3 dir = TexelCoordToDirection(face, xx, yy, size).normalized;
                            // quick cone test
                            float dDot = Vector3.Dot(dir, capCenters[sidx]);
                            if (dDot < capCos[sidx]) continue;
                            float ang = AngularDistanceToSegment(dir, seg.a, seg.b);
                            float val = 1f - SmoothStep(angularThresh, angularThresh + soft, ang);
                            if (val <= 0f) continue;
                            if (val > colors[idx].r) colors[idx] = new Color(val, val, val, 1f);
                            prefilled[idx] = true;
                        }
                    }
                }
            }

            // Main per-texel pass: skip prefilled pixels
            var root = roots[fi];
            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    int idx = y * size + x;
                    if (prefilled[idx]) continue;
                    Vector3 dir = TexelCoordToDirection(face, x, y, size).normalized;
                    float best = 0f;
                    List<int> candidates = root.Query(x, y);
                    for (int ci = 0; ci < candidates.Count; ++ci)
                    {
                        int s = candidates[ci];
                        float dDot = Vector3.Dot(dir, capCenters[s]);
                        if (dDot < capCos[s]) continue;
                        var seg = segments[s];
                        float ang = AngularDistanceToSegment(dir, seg.a, seg.b);
                        float t = 1f - SmoothStep(angularThresh, angularThresh + soft, ang);
                        if (t > best) best = t;
                        if (best >= 0.999f) break;
                    }
                    best = Mathf.Clamp01(best);
                    if (best > colors[idx].r) colors[idx] = new Color(best, best, best, 1f);
                }
            }
            faceColors[fi] = colors;
            facePrefilled[fi] = prefilled;
        });

        // Apply computed face colors to the cubemap on main thread
        for (int fi = 0; fi < 6; ++fi)
        {
            var face = fi == 0 ? CubemapFace.PositiveX : fi == 1 ? CubemapFace.NegativeX : fi == 2 ? CubemapFace.PositiveY : fi == 3 ? CubemapFace.NegativeY : fi == 4 ? CubemapFace.PositiveZ : CubemapFace.NegativeZ;
            if (face == CubemapFace.Unknown) continue;
            cube.SetPixels(faceColors[fi], face);
        }

        cube.Apply();

#if UNITY_EDITOR
        if (saveAsset)
        {
            string path = "Assets/Resources/" + assetName + ".cubemap.asset";
            System.IO.Directory.CreateDirectory("Assets/Resources");
            UnityEditor.AssetDatabase.CreateAsset(cube, path);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log("Saved overlay cubemap to: " + path);
        }
#endif
        return cube;
    }

    // Small helper: candidate bounding box in texel coordinates for a segment on a face
    struct CandidateBox
    {
        public int segIndex;
        public int xMin, xMax;
        public int yMin, yMax;
    }

    // Simple integer quadtree node specialized for texel-space bounds
    class QuadNode
    {
        const int MAX_ITEMS = 8;
        const int MAX_DEPTH = 10;
        public int x0, y0, x1, y1;
        public List<int> items; // segment indices stored at node
        public QuadNode[] children;
        public QuadNode(int x0, int y0, int x1, int y1)
        {
            this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1;
            items = new List<int>();
            children = null;
        }

        public void Insert(CandidateBox box, int depth)
        {
            // If node has no children and capacity isn't reached, store here
            if (children == null && (items.Count < MAX_ITEMS || depth >= MAX_DEPTH))
            {
                items.Add(box.segIndex);
                return;
            }
            if (children == null) Subdivide();
            // Try to insert into a child that fully contains the box
            foreach (var c in children)
            {
                if (box.xMin >= c.x0 && box.xMax <= c.x1 && box.yMin >= c.y0 && box.yMax <= c.y1)
                {
                    c.Insert(box, depth + 1);
                    return;
                }
            }
            // otherwise store here
            items.Add(box.segIndex);
        }

        void Subdivide()
        {
            children = new QuadNode[4];
            int mx = (x0 + x1) >> 1;
            int my = (y0 + y1) >> 1;
            children[0] = new QuadNode(x0, y0, mx, my);
            children[1] = new QuadNode(mx + 1, y0, x1, my);
            children[2] = new QuadNode(x0, my + 1, mx, y1);
            children[3] = new QuadNode(mx + 1, my + 1, x1, y1);
        }

        public List<int> Query(int x, int y)
        {
            var result = new List<int>(items);
            if (children != null)
            {
                int midx = (x0 + x1) >> 1;
                int midy = (y0 + y1) >> 1;
                int idx = (x > midx ? 1 : 0) + (y > midy ? 2 : 0);
                var child = children[Mathf.Clamp(idx, 0, 3)];
                if (child != null && x >= child.x0 && x <= child.x1 && y >= child.y0 && y <= child.y1)
                {
                    var childRes = child.Query(x, y);
                    if (childRes.Count > 0) result.AddRange(childRes);
                }
            }
            return result;
        }
    }

    // Project a unit direction into a specific face's [-1..1] UV coords (same as TexelCoordToDirection inverse)
    static void DirectionToFaceUV(Vector3 d, CubemapFace face, out float u, out float v)
    {
        d = d.normalized;
        switch (face)
        {
            case CubemapFace.PositiveX: u = -d.z / Mathf.Abs(d.x); v = -d.y / Mathf.Abs(d.x); return;
            case CubemapFace.NegativeX: u = d.z / Mathf.Abs(d.x); v = -d.y / Mathf.Abs(d.x); return;
            case CubemapFace.PositiveY: u = d.x / Mathf.Abs(d.y); v = d.z / Mathf.Abs(d.y); return;
            case CubemapFace.NegativeY: u = d.x / Mathf.Abs(d.y); v = -d.z / Mathf.Abs(d.y); return;
            case CubemapFace.PositiveZ: u = d.x / Mathf.Abs(d.z); v = -d.y / Mathf.Abs(d.z); return;
            case CubemapFace.NegativeZ: u = -d.x / Mathf.Abs(d.z); v = -d.y / Mathf.Abs(d.z); return;
        }
        u = 0f; v = 0f;
    }

    static float SmoothStep(float a, float b, float x)
    {
        if (x <= a) return 0f;
        if (x >= b) return 1f;
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float ab2 = Vector2.Dot(ab, ab);
        if (ab2 == 0f) return Vector2.Distance(p, a);
        float t = Vector2.Dot(p - a, ab) / ab2;
        t = Mathf.Clamp01(t);
        Vector2 proj = a + t * ab;
        return Vector2.Distance(p, proj);
    }

    // Angular distance (in radians) from unit direction 'd' to the great-circle arc from 'a' to 'b'.
    // 'a' and 'b' must be unit-length direction vectors on the sphere. Returns >= 0.
    static float AngularDistanceToSegment(Vector3 d, Vector3 a, Vector3 b)
    {
        // Ensure unit length
        d.Normalize();
        a.Normalize();
        b.Normalize();

        // Great-circle plane normal
        Vector3 gcNormal = Vector3.Cross(a, b);
        float gcNormSqr = gcNormal.sqrMagnitude;
        if (gcNormSqr < 1e-12f)
        {
            // a and b are nearly identical or antipodal; fall back to min endpoint distance
            float da = Vector3.Angle(d, a) * Mathf.Deg2Rad;
            float db = Vector3.Angle(d, b) * Mathf.Deg2Rad;
            return Mathf.Min(da, db);
        }

        gcNormal.Normalize();

        // Project d onto the great-circle: closest point direction is cross(gcNormal, cross(d, gcNormal)) normalized
        // But a simpler approach: compute closest point on the infinite great-circle as:
        Vector3 closest = Vector3.Cross(gcNormal, Vector3.Cross(d, gcNormal)).normalized;

        // Check whether 'closest' lies between a and b along the shorter arc.
        // We can check by comparing angular distance along the circle: if sign of triple products matches.
        // Compute sign tests using dot products of cross products.
        float crossA = Vector3.Dot(Vector3.Cross(a, closest), gcNormal);
        float crossB = Vector3.Dot(Vector3.Cross(closest, b), gcNormal);
        bool between = (crossA >= -1e-6f) && (crossB >= -1e-6f);

        if (between)
        {
            // distance is angular separation between d and closest
            float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, closest), -1f, 1f));
            return ang;
        }
        else
        {
            // outside the arc: distance is min angular distance to endpoints
            float da = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, a), -1f, 1f));
            float db = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, b), -1f, 1f));
            return Mathf.Min(da, db);
        }
    }

    // Map CubemapFace enum to array index 0..5
    static int FaceIndexFromCubemapFace(CubemapFace face)
    {
        switch (face)
        {
            case CubemapFace.PositiveX: return 0;
            case CubemapFace.NegativeX: return 1;
            case CubemapFace.PositiveY: return 2;
            case CubemapFace.NegativeY: return 3;
            case CubemapFace.PositiveZ: return 4;
            case CubemapFace.NegativeZ: return 5;
        }
        return 0;
    }

    // Given a unit direction, return which cubemap face it's closest to and approximate u,v in [-1,1]
    static void DirectionToFaceAndUV(Vector3 d, out CubemapFace face, out float u, out float v)
    {
        d = d.normalized;
        float ax = Mathf.Abs(d.x);
        float ay = Mathf.Abs(d.y);
        float az = Mathf.Abs(d.z);
        if (ax >= ay && ax >= az)
        {
            if (d.x > 0) { face = CubemapFace.PositiveX; u = -d.z / ax; v = -d.y / ax; return; }
            else { face = CubemapFace.NegativeX; u = d.z / ax; v = -d.y / ax; return; }
        }
        else if (ay >= ax && ay >= az)
        {
            if (d.y > 0) { face = CubemapFace.PositiveY; u = d.x / ay; v = d.z / ay; return; }
            else { face = CubemapFace.NegativeY; u = d.x / ay; v = -d.z / ay; return; }
        }
        else
        {
            if (d.z > 0) { face = CubemapFace.PositiveZ; u = d.x / az; v = -d.y / az; return; }
            else { face = CubemapFace.NegativeZ; u = -d.x / az; v = -d.y / az; return; }
        }
    }

    // Reconstruct unit direction from a face and u,v in [-1,1]
    static Vector3 FaceUVToDirection(CubemapFace face, float u, float v)
    {
        switch (face)
        {
            case CubemapFace.PositiveX: return new Vector3(1f, -v, -u).normalized;
            case CubemapFace.NegativeX: return new Vector3(-1f, -v, u).normalized;
            case CubemapFace.PositiveY: return new Vector3(u, 1f, v).normalized;
            case CubemapFace.NegativeY: return new Vector3(u, -1f, -v).normalized;
            case CubemapFace.PositiveZ: return new Vector3(u, -v, 1f).normalized;
            case CubemapFace.NegativeZ: return new Vector3(-u, -v, -1f).normalized;
        }
        return Vector3.forward;
    }

    static Vector3 TexelCoordToDirection(CubemapFace face, int x, int y, int size)
    {
        // Map uv to -1..1 with texel center sampling
        float u = (x + 0.5f) / size * 2f - 1f;
        float v = (y + 0.5f) / size * 2f - 1f;
        switch (face)
        {
            case CubemapFace.PositiveX: return new Vector3(1f, -v, -u);
            case CubemapFace.NegativeX: return new Vector3(-1f, -v, u);
            case CubemapFace.PositiveY: return new Vector3(u, 1f, v);
            case CubemapFace.NegativeY: return new Vector3(u, -1f, -v);
            case CubemapFace.PositiveZ: return new Vector3(u, -v, 1f);
            case CubemapFace.NegativeZ: return new Vector3(-u, -v, -1f);
        }
        return Vector3.forward;
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Overlay Cubemap (Editor)")]
    public void EditorGenerate()
    {
            var c = GenerateCubemap(baseRadius, 4, faceSize);
        if (c != null)
        {
            Debug.Log("Overlay cubemap generated. Assign it to the material's _DualOverlayCube.");
        }
    }
#endif
}
