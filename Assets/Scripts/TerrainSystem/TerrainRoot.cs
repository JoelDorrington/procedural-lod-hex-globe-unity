using UnityEngine;
using System.Collections.Generic;
using HexGlobeProject.TerrainSystem; // TerrainShaderGlobals

namespace HexGlobeProject.TerrainSystem
{
    /// <summary>
    /// Simplified TerrainRoot: builds six static cube-face meshes once.
    /// </summary>
    [ExecuteAlways]
    public class TerrainRoot : MonoBehaviour
    {
    public TerrainConfig config;
    public Material terrainMaterial;
    [SerializeField]
    [Tooltip("Hide the ocean's MeshRenderer in the scene but keep the Ocean GameObject and its transform present for positioning.")]
    public bool hideOceanRenderer = false;
    private GameObject _oceanGO;

    private readonly List<TerrainPatch> _patches = new();
    private bool _usingDetail;
    private float _lastRebuildTime;

        [ContextMenu("Rebuild Terrain")]
        public void Rebuild()
        {
            Clear();
            if (config == null || config.heightProvider == null) { Debug.LogWarning("TerrainConfig or heightProvider missing."); return; }
            BuildBasePatches();
        }

        private void OnEnable()
        {
            if (Application.isPlaying == false) return;
            if (_patches.Count == 0) Rebuild();
        }

        private void Update()
        {
            if (!Application.isPlaying || config == null) return;
            if (Camera.main == null) return;
            float dist = Vector3.Distance(Camera.main.transform.position, transform.position);
            // Distance detail boost removed.
        }

        private void Clear()
        {
            for (int i = 0; i < _patches.Count; i++)
            {
                if (_patches[i].gameObject != null)
                {
                    if (Application.isPlaying)
                        Destroy(_patches[i].gameObject);
                    else
                        DestroyImmediate(_patches[i].gameObject);
                }
            }
            _patches.Clear();
            if (_oceanGO != null)
            {
                if (Application.isPlaying) Destroy(_oceanGO); else DestroyImmediate(_oceanGO);
                _oceanGO = null;
            }
        }

        private void BuildBasePatches()
        {
            int res = Mathf.Max(4, config.baseResolution);
            float radius = config.baseRadius;
            var hp = config.heightProvider;
            for (int face = 0; face < 6; face++)
            {
                var patch = CreatePatch(face);
                patch.mesh = GenerateFaceMesh(face, res, radius, hp, config.heightScale);
                patch.gameObject.GetComponent<MeshFilter>().sharedMesh = patch.mesh;
                patch.state = PatchState.Ready;
                _patches.Add(patch);
            }
            CreateOceanIfNeeded(radius);
            TerrainShaderGlobals.Apply(config, terrainMaterial);
        }

        // (Removed inline PushShaderGlobals - now centralized in TerrainShaderGlobals)

        private TerrainPatch CreatePatch(int face)
        {
            var patch = new TerrainPatch { face = face, lod = 0, x = 0, y = 0, state = PatchState.Generating };
            patch.gameObject = new GameObject(patch.Name);
            patch.gameObject.transform.SetParent(transform, false);
            var mf = patch.gameObject.AddComponent<MeshFilter>();
            var mr = patch.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = terrainMaterial;
            return patch;
        }

        private void CreateOceanIfNeeded(float baseRadius)
        {
            // Ocean feature removed.
            if (config == null) return;
            if (_oceanGO != null) return; // already
            float radius = baseRadius + config.seaLevel; // seaLevel expressed in same height units (pre heightScale)
            if (radius <= 0.01f) radius = Mathf.Max(0.01f, baseRadius * 0.01f);
            _oceanGO = new GameObject("Ocean");
            _oceanGO.transform.SetParent(transform, false);
            var mf = _oceanGO.AddComponent<MeshFilter>();
            var mr = _oceanGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = terrainMaterial;
            mf.sharedMesh = BuildIcoSphere(32, radius); // fixed moderate resolution
            mr.enabled = !hideOceanRenderer;
        }

        /// <summary>
        /// Public API to update the ocean renderer visibility at runtime or from other components.
        /// This updates the serialized flag and the existing Ocean GameObject's MeshRenderer if present.
        /// </summary>
        /// <param name="hide">If true, disables the Ocean MeshRenderer (keeps GameObject/transform).</param>
        public void SetHideOceanRenderer(bool hide)
        {
            hideOceanRenderer = hide;
            if (_oceanGO != null)
            {
                var mr = _oceanGO.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = !hide;
            }
        }

        private Mesh BuildIcoSphere(int resolution, float radius)
        {
            // Cheap UV-less icosphere (subdividing triangles) for smooth ocean surface.
            // resolution = number of subdivision iterations (clamped 0..5). oceanResolution serialized as segments; map approx.
            int subdiv = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(Mathf.Max(4, resolution), 2f)) - 1, 0, 5);
            // Base icosahedron
            var t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(-1,  t,  0),new Vector3( 1,  t,  0),new Vector3(-1, -t,  0),new Vector3( 1, -t,  0),
                new Vector3( 0, -1,  t),new Vector3( 0,  1,  t),new Vector3( 0, -1, -t),new Vector3( 0,  1, -t),
                new Vector3( t,  0, -1),new Vector3( t,  0,  1),new Vector3(-t,  0, -1),new Vector3(-t,  0,  1)
            };
            for (int i=0;i<verts.Count;i++) verts[i]=verts[i].normalized;
            var faces = new System.Collections.Generic.List<int[]>
            {
                new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11},
                new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
                new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9},
                new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1}
            };
            var midpointCache = new System.Collections.Generic.Dictionary<long,int>();
            int GetMid(int a,int b){ long key = a<b ? ((long)a<<32)| (uint)b : ((long)b<<32)| (uint)a; if(midpointCache.TryGetValue(key,out int m)) return m; var v = (verts[a]+verts[b])*0.5f; v.Normalize(); verts.Add(v); m=verts.Count-1; midpointCache[key]=m; return m; }
            for(int s=0;s<subdiv;s++){
                var newFaces = new System.Collections.Generic.List<int[]>();
                foreach(var f in faces){ int a=f[0],b=f[1],c=f[2]; int ab=GetMid(a,b); int bc=GetMid(b,c); int ca=GetMid(c,a); newFaces.Add(new[]{a,ab,ca}); newFaces.Add(new[]{b,bc,ab}); newFaces.Add(new[]{c,ca,bc}); newFaces.Add(new[]{ab,bc,ca}); }
                faces = newFaces;
            }
            var mesh = new Mesh{ name="OceanIco"};
            var mverts = new Vector3[verts.Count];
            var normals = new Vector3[verts.Count];
            for(int i=0;i<verts.Count;i++){ mverts[i]=verts[i]*radius; normals[i]=verts[i]; }
            var tris = new int[faces.Count*3];
            int ti=0; foreach(var f in faces){ tris[ti++]=f[0]; tris[ti++]=f[1]; tris[ti++]=f[2]; }
            mesh.SetVertices(mverts); mesh.SetNormals(normals); mesh.SetTriangles(tris,0); mesh.RecalculateBounds();
            return mesh;
        }

        private Mesh GenerateFaceMesh(int face, int resolution, float radius, TerrainHeightProviderBase hp, float heightScale)
        {
            int vertsPerEdge = resolution + 1;
            int vertCount = vertsPerEdge * vertsPerEdge;
            int quadCount = resolution * resolution;
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var indices = new int[quadCount * 6];
            int idx = 0;
            float seaRadius = radius + (config != null ? config.seaLevel : 0f);
            bool doCull = config != null && config.cullBelowSea;
            bool removeTris = doCull && config.removeFullySubmergedTris;
            float epsilon = (config != null ? config.seaClampEpsilon : 0.01f);
            var below = new bool[vertCount];
            for (int y = 0; y < vertsPerEdge; y++)
            {
                float v = (y / (float)resolution) * 2f - 1f;
                for (int x = 0; x < vertsPerEdge; x++)
                {
                    float u = (x / (float)resolution) * 2f - 1f;
                    Vector3 dir = CubeSphere.FaceLocalToUnit(face, u, v);
                    float raw = hp.Sample(dir, resolution);
                    // Use raw sampled height directly (realistic height remap removed)
                    float h = raw;
                    float finalR = radius + h;
                    bool submerged = doCull && finalR < seaRadius;
                    if (submerged)
                    {
                        if (!removeTris)
                        {
                            finalR = seaRadius + epsilon; // clamp up slightly
                        }
                    }
                    below[idx] = submerged;
                    Vector3 pos = dir * finalR;
                    verts[idx] = pos;
                    normals[idx] = dir;
                    uvs[idx] = new Vector2(x / (float)resolution, y / (float)resolution);
                    idx++;
                }
            }

            // Detect orientation (some face parametrizations may invert winding)
            // Use top-left corner and its immediate right & down neighbors.
            // If cross(right, down) points inward (dot < 0) we need to flip all triangle windings.
            bool flip = false;
            if (vertsPerEdge >= 2)
            {
                Vector3 a = verts[0];
                Vector3 right = verts[1] - a;
                Vector3 down = verts[vertsPerEdge] - a; // next row same column
                Vector3 cross = Vector3.Cross(right, down); // expected to point roughly outward
                if (Vector3.Dot(cross, a) < 0f)
                {
                    flip = true;
                }
            }
            int ti = 0;
            for (int y = 0; y < resolution; y++)
            {
                int row = y * vertsPerEdge;
                int nextRow = (y + 1) * vertsPerEdge;
                for (int x = 0; x < resolution; x++)
                {
                    int a = row + x;
                    int b = row + x + 1;
                    int c = nextRow + x;
                    int d = nextRow + x + 1;
                    if (removeTris)
                    {
                        // Skip triangles if all three corner verts fully submerged
                        bool sub0 = below[a]; bool sub1 = below[b]; bool sub2 = below[c]; bool sub3 = below[d];
                        bool tri1Skip = sub0 && sub3 && sub2; // a,d,c (or reversed)
                        bool tri2Skip = sub0 && sub1 && sub3; // a,b,d (or reversed)
                        if (!flip)
                        {
                            if (!tri1Skip) { indices[ti++] = a; indices[ti++] = d; indices[ti++] = c; }
                            if (!tri2Skip) { indices[ti++] = a; indices[ti++] = b; indices[ti++] = d; }
                        }
                        else
                        {
                            if (!tri1Skip) { indices[ti++] = a; indices[ti++] = c; indices[ti++] = d; }
                            if (!tri2Skip) { indices[ti++] = a; indices[ti++] = d; indices[ti++] = b; }
                        }
                    }
                    else
                    {
                        if (!flip)
                        {
                            indices[ti++] = a; indices[ti++] = d; indices[ti++] = c;
                            indices[ti++] = a; indices[ti++] = b; indices[ti++] = d;
                        }
                        else
                        {
                            indices[ti++] = a; indices[ti++] = c; indices[ti++] = d;
                            indices[ti++] = a; indices[ti++] = d; indices[ti++] = b;
                        }
                    }
                }
            }
            var mesh = new Mesh { name = $"Face_{face}" };
            if (vertCount > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            if (removeTris)
            {
                // Trim unused portion of index buffer
                if (ti < indices.Length)
                {
                    var finalIdx = new int[ti];
                    System.Array.Copy(indices, finalIdx, ti);
                    mesh.SetTriangles(finalIdx, 0);
                }
                else
                {
                    mesh.SetTriangles(indices, 0);
                }
            }
            else
            {
                mesh.SetTriangles(indices, 0);
            }
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
