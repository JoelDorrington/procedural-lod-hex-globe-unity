using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HexGlobeProject.TerrainSystem;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Manages baking and child tile LOD for procedural planet terrain.
    ///
    /// DEPRECATION NOTICE:
    /// This class is deprecated. Tile streaming and camera-driven spawning have been
    /// moved into camera-centric components such as `PlanetTileExplorerCam` and
    /// `PlanetTileSpawner`. Prefer using those newer components for runtime LOD.
    /// </summary>
    [System.Obsolete("PlanetLodManager is deprecated. Use PlanetTileExplorerCam and PlanetTileSpawner for camera-driven tile streaming.")]
    public class PlanetLodManager : MonoBehaviour
    {
        // ...existing fields...
        [Header("References")]
        [SerializeField] public Transform planetSphereTransform;
        public Transform PlanetSphereTransform => planetSphereTransform != null ? planetSphereTransform : this.transform;
        public OctaveMaskHeightProvider OctaveWrapper => _octaveWrapper;
        public bool EdgePromotionRebuild => _edgePromotionRebuild;
        public float SplitChildResolutionMultiplier => splitChildResolutionMultiplier;
        public float ChildHeightEnhancement => childHeightEnhancement;
        public float SplitFadeDuration => splitFadeDuration;

        public Dictionary<TileId, TileData> ChildTiles => _childTiles;
        public Dictionary<TileId, GameObject> ChildTileObjects => _childTileObjects;
        public Material TerrainMaterial => terrainMaterial;
        [SerializeField] private TerrainConfig config;
        [SerializeField] private TerrainHeightProviderBase heightProvider;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private float childHeightEnhancement = 1.0f;
        [SerializeField] private bool enableProximitySplit = true;
        [SerializeField] private int splitTargetDepthOverride = -1;
        [SerializeField] private float splitFadeDuration = 0.35f;
        [SerializeField] private float splitChildResolutionMultiplier = 1f;
        [Header("Proximity Split Thresholds")]
        [SerializeField] public float splitDistanceThreshold = 500f;
        [SerializeField] public float mergeDistanceThreshold = 800f;

        private OctaveMaskHeightProvider _octaveWrapper;
        private bool _edgePromotionRebuild = false;
        private int bakedDepth = -1;
        private PlanetTileSpawner _tileSpawner = new PlanetTileSpawner();

        private readonly Dictionary<TileId, TileData> _tiles = new();
        private readonly Dictionary<TileId, GameObject> _tileObjects = new();
        private readonly Dictionary<TileId, TileData> _childTiles = new();
        private readonly Dictionary<TileId, GameObject> _childTileObjects = new();

        [ContextMenu("Bake Base Depth")]
        public void BakeBaseDepthContextMenu()
        {
            if (bakedDepth >= 0) return;
            StartCoroutine(BakeBaseDepth());
        }

        public IEnumerator BakeBaseDepth()
        {
            Debug.Log("BakeBaseDepth started");
            EnsureHeightProvider();
            _tiles.Clear();
            ClearActiveTiles();
            int depth = Mathf.Max(0, config.bakeDepth);
            PrepareOctaveMaskForDepth(depth);
            yield return StartCoroutine(BakeDepthSingle(depth));
            bakedDepth = depth;
            Debug.Log($"BakeBaseDepth finished, depth={depth}, tiles={_tiles.Count}");
            // Tile spawning is now managed by PlanetLodSpawnCam
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

        public void BuildTileMesh(TileData data)
        {
            Debug.Log($"BuildTileMesh for {data.id}");
            float rmin = float.MaxValue; float rmax = float.MinValue;
            var meshBuilder = new PlanetTileMeshBuilder(
                config,
                heightProvider,
                _octaveWrapper,
                bakedDepth,
                splitChildResolutionMultiplier,
                childHeightEnhancement,
                _edgePromotionRebuild
            );
            meshBuilder.BuildTileMesh(data, ref rmin, ref rmax);
            Debug.Log($"Tile {data.id} mesh assigned: {data.mesh != null}");
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

        public int ResolveResolutionForDepth(int depth, int tilesPerEdge)
        {
            int baseRes = config.baseResolution;
            return Mathf.Max(2, baseRes / tilesPerEdge);
        }

        private void SpawnAllTiles()
        {
            _tileSpawner.ClearActiveTiles(_tileObjects);
            foreach (var kv in _tiles)
            {
                var td = kv.Value;
                if (td.mesh == null) continue;
                _tileSpawner.SpawnOrUpdateTileGO(td, _tileObjects, terrainMaterial, transform);
            }
            TerrainShaderGlobals.Apply(config, terrainMaterial);
        }

        private void ClearActiveTiles()
        {
            _tileSpawner.ClearActiveTiles(_tileObjects);
        }

        private void EnsureHeightProvider()
        {
            if (heightProvider == null && config != null && config.heightProvider != null)
                heightProvider = config.heightProvider;
        }

        public TerrainConfig Config => config;
        public TerrainHeightProviderBase HeightProvider => heightProvider;
        public Camera TargetCamera => targetCamera;
        public bool EnableProximitySplit => enableProximitySplit;
        public int SplitTargetDepthOverride => splitTargetDepthOverride;
        public int BakedDepth => bakedDepth;
        public Dictionary<TileId, TileData> Tiles => _tiles;
        public Dictionary<TileId, GameObject> TileObjects => _tileObjects;
    }
}
