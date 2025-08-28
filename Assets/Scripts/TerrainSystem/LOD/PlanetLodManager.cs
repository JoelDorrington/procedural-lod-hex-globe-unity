using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Manages baking and child tile LOD for procedural planet terrain.
    /// </summary>
    public class PlanetLodManager : MonoBehaviour
    {
        [SerializeField] private TerrainConfig config;
        [SerializeField] private TerrainHeightProviderBase heightProvider;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private bool autoBakeOnStart = true;
        [SerializeField] private bool enableEdgeConstraint = true;
        [SerializeField] private bool hierarchicalAlignedSampling = true;
        [SerializeField] private bool promoteConstrainedEdgesPostFade = true;
        [SerializeField] private float childHeightEnhancement = 1.0f;
        [SerializeField] private bool enableProximitySplit = true;
        [SerializeField] private int splitTargetDepthOverride = -1;
        [SerializeField] private float splitFadeDuration = 0.35f;
        [SerializeField] private float splitChildResolutionMultiplier = 1f;

        private OctaveMaskHeightProvider _octaveWrapper;
        private bool _edgePromotionRebuild = false;
        public int bakedDepth = -1;

        private readonly Dictionary<TileId, TileData> _tiles = new();
        private readonly Dictionary<TileId, GameObject> _tileObjects = new();
        private readonly Dictionary<TileId, TileData> _childTiles = new();
        private readonly Dictionary<TileId, GameObject> _childTileObjects = new();
        private int _bakedTileResolution = -1;

        [ContextMenu("Bake Base Depth")]
        public void BakeBaseDepthContextMenu()
        {
            if (bakedDepth >= 0) return;
            StartCoroutine(BakeBaseDepth());
        }

        public IEnumerator BakeBaseDepth()
        {
            EnsureHeightProvider();
            _tiles.Clear();
            ClearActiveTiles();
            int depth = Mathf.Max(0, config.bakeDepth);
            PrepareOctaveMaskForDepth(depth);
            yield return StartCoroutine(BakeDepthSingle(depth));
            bakedDepth = depth;
            SpawnAllTiles();
        }

        private IEnumerator BakeDepthSingle(int depth)
        {
            int tilesPerEdge = 1 << depth;
            int resolution = ResolveResolutionForDepth(depth, tilesPerEdge);
            for (int face = 0; face < 6; face++)
            {
                for (int y = 0; y < tilesPerEdge; y++)
                {
                    for (int x = 0; x < tilesPerEdge; x++)
                    {
                        var id = new TileId((byte)face, (byte)depth, (ushort)x, (ushort)y);
                        var data = new TileData { id = id, resolution = resolution, isBaked = true };
                        BuildTileMesh(data);
                        _tiles[id] = data;
                    }
                }
            }
            yield break;
        }

        private void BuildTileMesh(TileData data)
        {
            float rmin = float.MaxValue; float rmax = float.MinValue;
            var meshBuilder = new PlanetTileMeshBuilder(
                config,
                heightProvider,
                _octaveWrapper,
                hierarchicalAlignedSampling,
                enableEdgeConstraint,
                bakedDepth,
                splitChildResolutionMultiplier,
                childHeightEnhancement,
                _edgePromotionRebuild
            );
            meshBuilder.BuildTileMesh(data, ref rmin, ref rmax);
        }

        private void PrepareOctaveMaskForDepth(int depth)
        {
            int maxOct = (depth == config.bakeDepth) ? config.maxOctaveBake : config.maxOctaveSplit;
            if (maxOct == -1)
            {
                if (_octaveWrapper != null && heightProvider == _octaveWrapper && _octaveWrapper.inner != null)
                    heightProvider = _octaveWrapper.inner;
                return;
            }
            if (_octaveWrapper == null)
                _octaveWrapper = new OctaveMaskHeightProvider();
            if (heightProvider is OctaveMaskHeightProvider existingWrap)
                existingWrap.maxOctave = maxOct;
            else
            {
                _octaveWrapper.inner = heightProvider;
                _octaveWrapper.maxOctave = maxOct;
                heightProvider = _octaveWrapper;
            }
        }

        private int ResolveResolutionForDepth(int depth, int tilesPerEdge)
        {
            int baseRes = config.baseResolution;
            return Mathf.Max(2, baseRes / tilesPerEdge);
        }

        private void SpawnAllTiles()
        {
            ClearActiveTiles();
            foreach (var kv in _tiles)
            {
                var td = kv.Value;
                if (td.mesh == null) continue;
                SpawnOrUpdateTileGO(td);
            }
            TerrainShaderGlobals.Apply(config, terrainMaterial);
        }

        private void ClearActiveTiles()
        {
            foreach (var kv in _tileObjects)
            {
                var go = kv.Value;
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
            _tileObjects.Clear();
        }

        private void EnsureHeightProvider()
        {
            if (heightProvider == null && config != null && config.heightProvider != null)
                heightProvider = config.heightProvider;
        }

        private void SpawnOrUpdateTileGO(TileData td)
        {
            if (_tileObjects.TryGetValue(td.id, out var existing) && existing != null)
            {
                var mf = existing.GetComponent<MeshFilter>();
                if (mf.sharedMesh != td.mesh) mf.sharedMesh = td.mesh;
                return;
            }
            var go = new GameObject($"Tile_{td.id.face}_d{td.id.depth}_{td.id.x}_{td.id.y}");
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = td.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = terrainMaterial;
            _tileObjects[td.id] = go;
        }

        private void Update()
        {
            // Update loop to trigger baking and manage tile visibility/LOD transitions
            if (autoBakeOnStart && bakedDepth < 0)
            {
                autoBakeOnStart = false;
                StartCoroutine(BakeBaseDepth());
            }
            // Proximity split logic
            var splitter = new PlanetLodSplitter();
            splitter.UpdateProximitySplits(this);
        }
    }
}
