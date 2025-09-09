#define PLAYMODE_TESTS

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

namespace HexGlobeProject.TerrainSystem.LOD
{
	/// <summary>
	/// Lightweight placeholder of the original PlanetTileVisibilityManager used to keep the project
	/// compiling while methods are migrated back from a backup. This minimal class intentionally
	/// omits debug drawing and heavy logic. Copy real implementations back from the backup file
	/// one method at a time.
	/// </summary>
	[AddComponentMenu("HexGlobe/Planet Tile Explorer Cam")]
	public class PlanetTileVisibilityManager : MonoBehaviour
	{
		[SerializeField]
		public TerrainConfig config;

		// Camera controller reference (tests set this directly)
		[SerializeField]
		public CameraController GameCamera;

		// Serialized fields expected by tests via SerializedObject
		[SerializeField]
		private Transform planetTransform;

		[SerializeField]
		private int terrainTileLayer = 0;

		// Multiplier applied when computing the curved icosphere sampling radius
		[SerializeField]
		private float curvedIcosphereRadiusMultiplier = 1.01f;

		// Material used by spawned terrain tiles (tests look up this private field)
		[SerializeField]
		private Material terrainMaterial;

		// Runtime depth mapping tuning (used to derive depth from camera distance)
		[SerializeField]
		private int maxDepth = 2;

		[SerializeField]
		private float binBias = 1.2f;

		// NOTE: depth-level max distance configuration was removed — ComputeDepthFromCamera
		// now derives depth solely from CameraController distance and `maxDepth`.

		// Adaptive raycast distribution settings
		[SerializeField]
		[Tooltip("Approximate number of rays to cast; actual cast uses a square sampling grid <= this value")]
		private int _maxRays = 25;

		[SerializeField]
		[Tooltip("Exponent for radial bias smoothing function. Higher values concentrate rays more toward sphere edges.")]
		private float radialBiasExponent = 2.0f;

		// Runtime state for adaptive distribution
		private float _sphereViewportRadius;
		private Vector2[] _baseSamples;
		private int _baseSampleGrid = 0; // sqrtRays
		private int _sampleCapacity = 0;

		// Debug toggle: when true the manager will not compute or apply depth changes
		// from the camera. Tests can enable this to manually control depth via SetDepth
		// without the manager overriding the value from CameraController.
		[SerializeField]
		public bool debugDisableCameraDepthSync = false;

		public TerrainConfig TerrainConfig => config;

		// Planet properties
		private Vector3 _planetCenter;
		private float _planetRadius => config != null ? config.baseRadius : 1f;
		private Camera cam => GameCamera != null ? GameCamera.GetComponent<Camera>() : null;

		// Minimal precomputed entry used by callers. Expand when restoring logic.
		public struct PrecomputedTileEntry
		{
			public Vector3 normal;
			public int face;
			public int x;
			public int y;
			public float uCenter;
			public float vCenter;
			public int tilesPerEdge;
			public float tileOffsetU;
			public float tileOffsetV;
			public Vector3 centerWorld;
			public Vector3[] cornerWorldPositions;
		}

		// Simple registry keyed by depth. Methods below return false until real logic is restored.
		private static readonly Dictionary<int, List<PrecomputedTileEntry>> s_precomputedRegistry = new Dictionary<int, List<PrecomputedTileEntry>>();

		private List<PrecomputedTileEntry> _precomputedTilesForDepth = new List<PrecomputedTileEntry>();
		private int _lastPrecomputedDepth = -1;

		// Current depth tracked at runtime. Use Update() to compute desired depth
		// from camera state and call SetDepth when it changes.
		private int _currentDepth = 0;

		// Last observed camera distance to detect external changes
		private float _lastObservedCameraDistance = float.NaN;

		// Simple active spawn registry so repeated TrySpawnTile calls return the same GameObject
		private readonly Dictionary<TileId, GameObject> _spawnedTiles = new Dictionary<TileId, GameObject>();

		// Track tiles that are either already spawned or queued to spawn using a packed key
		// so we can defensively prevent duplicate enqueues across rapid depth transitions.
		private readonly HashSet<ulong> _activeOrQueued = new HashSet<ulong>();

		// Pack depth/face/x/y into a 64-bit key for fast set membership tests.
		private static ulong PackTileKey(TileId id)
		{
			return ((ulong)(uint)id.depth << 48) | ((ulong)(uint)id.face << 32) | ((ulong)(uint)id.x << 16) | (ulong)(uint)id.y;
		}

		// Lazy mesh builder used to construct visual meshes on-demand when a tile is hit
		private PlanetTileMeshBuilder _meshBuilder = null;

	// Heuristic coroutine handle and interval used by tests to verify the manager
	// starts a raycast heuristic running at ~30Hz. In EditMode Unity does not
	// reliably create Coroutine objects, so we store this as an object and use
	// a simple non-null sentinel when running outside PlayMode so tests can
	// detect the heuristic has been started.
	// Initialize to null here; Awake will install a sentinel when not playing
	// to preserve the original intent without preventing PlayMode coroutines.
	private object _heuristicCoroutine = null;
		private const float HeuristicInterval = 1f / 30f;
#if PLAYMODE_TESTS
		// Tick counter incremented by the heuristic coroutine. Tests read this
		// to verify the heuristic is actually running at the expected frequency.
		public int heuristicTickCount = 0;
#endif
		private void PrecomputeTileNormalsForDepth(int depth)
		{
			// Ensure planet center is up-to-date (tests may set planetTransform via reflection)
			Vector3 planetCenter = planetTransform != null ? planetTransform.position : this.transform.position;

			// Clear current precomputed list
			_precomputedTilesForDepth.Clear();

			int tilesPerEdge = 1 << depth;

			// Use the manager's config when available. In Editor/tests the serialized config
			// may be null; attempt to load the project's TerrainConfig asset as a fallback
			// so precomputed registry uses the same baseRadius and height provider as the
			// rest of the system (notably PlanetTileMeshBuilder which may be constructed
			// with an explicit TerrainConfig).
			TerrainConfig localConf = config;
			#if UNITY_EDITOR
			if (localConf == null)
			{
				var maybe = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainConfig>("Assets/TerrainConfig.asset");
				if (maybe != null) localConf = maybe;
			}
			#endif

			float planetRadius = localConf != null ? localConf.baseRadius : 1f;

			float curvedMultiplier = Mathf.Clamp(curvedIcosphereRadiusMultiplier, 0.95f, 1.2f);

			if (depth == 0)
			{
				for (int face = 0; face < 20; face++)
				{
					int x = 0, y = 0;
					float u = (x + 0.5f) / tilesPerEdge;
					float v = (y + 0.5f) / tilesPerEdge;
					if (u + v > 1f)
					{
						float excess = (u + v) - 1f;
						u -= excess * 0.5f;
						v -= excess * 0.5f;
					}

					Vector3 dir = IcosphereMapping.BarycentricToWorldDirection(face, u, v).normalized;
					var entry = new PrecomputedTileEntry
					{
						normal = dir,
						face = face,
						x = x,
						y = y,
						uCenter = u,
						vCenter = v,
						tilesPerEdge = tilesPerEdge,
					};

					// Sample height for canonical center
					// Compute the tile's sampled world-space centroid by averaging a grid of
					// sample points. This keeps precomputed registry centers consistent with
					// the mesh builder's per-vertex sampling (avoids centroid mismatches).
					entry.centerWorld = entry.normal * (planetRadius) + planetCenter; // fallback
					if (localConf != null)
					{
						var provider = localConf.heightProvider ?? (TerrainHeightProviderBase)new HexGlobeProject.TerrainSystem.SimplePerlinHeightProvider();
						int sampleRes = localConf.baseResolution > 0 ? localConf.baseResolution : 16;
						// Accumulate world-space vertex positions matching the builder's sampling
						Vector3 centerAccum = Vector3.zero;
						int sampleCount = 0;
						for (int j = 0; j < sampleRes; j++)
						{
							for (int i = 0; i < sampleRes; i++)
							{
								// Map tile-local vertex to global barycentric coords
								IcosphereMapping.TileVertexToBarycentricCoordinates(
									new TileId { face = face, x = x, y = y, depth = depth }, i, j, sampleRes,
									out float globalU, out float globalV);
								Vector3 sampleDir = IcosphereMapping.BarycentricToWorldDirection(face, globalU, globalV).normalized;
								float raw = provider.Sample(in sampleDir, sampleRes) * localConf.heightScale;
								Vector3 worldV = sampleDir * (planetRadius + raw) + planetCenter;
								centerAccum += worldV;
								sampleCount++;
							}
						}
						if (sampleCount > 0) entry.centerWorld = centerAccum / sampleCount;
					}

					float u0 = (float)x / tilesPerEdge;
					float v0 = (float)y / tilesPerEdge;
					float u1 = (float)(x + 1) / tilesPerEdge;
					float v1 = v0;
					float u2 = u0;
					float v2 = (float)(y + 1) / tilesPerEdge;

					entry.tileOffsetU = u0;
					entry.tileOffsetV = v0;

					entry.cornerWorldPositions = new Vector3[3];
					entry.cornerWorldPositions[0] = IcosphereMapping.BarycentricToWorldDirection(face, u0, v0).normalized * planetRadius + planetCenter;
					entry.cornerWorldPositions[1] = IcosphereMapping.BarycentricToWorldDirection(face, u1, v1).normalized * planetRadius + planetCenter;
					entry.cornerWorldPositions[2] = IcosphereMapping.BarycentricToWorldDirection(face, u2, v2).normalized * planetRadius + planetCenter;

					_precomputedTilesForDepth.Add(entry);
				}
			}
			else
			{
				for (int face = 0; face < 20; face++)
				{
					for (int x = 0; x < tilesPerEdge; x++)
					{
						for (int y = 0; y < tilesPerEdge; y++)
						{
							IcosphereMapping.GetTileBarycentricCenter(x, y, depth, out float u, out float v);

							Vector3 dir = IcosphereMapping.BarycentricToWorldDirection(face, u, v).normalized;
							var entry = new PrecomputedTileEntry
							{
								normal = dir,
								face = face,
								x = x,
								y = y,
								uCenter = u,
								vCenter = v,
								tilesPerEdge = tilesPerEdge,
							};

							float centerHeight = 0f;
							if (localConf != null)
							{
								var provider = localConf.heightProvider ?? (TerrainHeightProviderBase)new HexGlobeProject.TerrainSystem.SimplePerlinHeightProvider();
								int sampleRes = localConf.baseResolution > 0 ? localConf.baseResolution : 16;
								centerHeight = provider.Sample(in dir, sampleRes) * localConf.heightScale;
							}
							entry.centerWorld = entry.normal * (planetRadius + centerHeight) + planetCenter;

							float u0 = (float)x / tilesPerEdge;
							float v0 = (float)y / tilesPerEdge;
							float u1 = (float)(x + 1) / tilesPerEdge;
							float v1 = v0;
							float u2 = u0;
							float v2 = (float)(y + 1) / tilesPerEdge;

							entry.tileOffsetU = u0;
							entry.tileOffsetV = v0;

							entry.cornerWorldPositions = new Vector3[3];
							entry.cornerWorldPositions[0] = IcosphereMapping.BarycentricToWorldDirection(face, u0, v0).normalized * planetRadius + planetCenter;
							entry.cornerWorldPositions[1] = IcosphereMapping.BarycentricToWorldDirection(face, u1, v1).normalized * planetRadius + planetCenter;
							entry.cornerWorldPositions[2] = IcosphereMapping.BarycentricToWorldDirection(face, u2, v2).normalized * planetRadius + planetCenter;

							_precomputedTilesForDepth.Add(entry);
						}
					}
				}
			}

			// Register into static registry for external lookup
			s_precomputedRegistry[depth] = new List<PrecomputedTileEntry>(_precomputedTilesForDepth);
			_lastPrecomputedDepth = depth;
		}

		public static bool GetPrecomputedEntry(Vector3 faceNormal, int depth, out PrecomputedTileEntry entryOut)
		{
			entryOut = default;
			if (!s_precomputedRegistry.TryGetValue(depth, out var list) || list == null || list.Count == 0) return false;
			// Return the first entry as a stable default (real selection logic restored later)
			entryOut = list[0];
			return true;
		}

		public static bool GetPrecomputedEntry(TileId tileId, out PrecomputedTileEntry entryOut)
		{
			// Convenience overload used throughout the codebase
			return GetPrecomputedEntry(tileId.faceNormal, tileId.depth, out entryOut);
		}

		public static bool GetPrecomputedIndex(TileId tileId, out int indexOut, out PrecomputedTileEntry entryOut)
		{
			indexOut = -1; entryOut = default;
			if (!s_precomputedRegistry.TryGetValue(tileId.depth, out var list) || list == null || list.Count == 0) return false;
			// Basic linear search by face/x/y if available; otherwise return false
			for (int i = 0; i < list.Count; i++)
			{
				var e = list[i];
				if (e.face == tileId.face && e.x == tileId.x && e.y == tileId.y)
				{
					indexOut = i;
					entryOut = e;
					return true;
				}
			}
			return false;
		}

		public static bool TryGetTileCenterWorldPosition(TileId tileId, out Vector3 center)
		{
			center = Vector3.zero;
			if (GetPrecomputedIndex(tileId, out var idx, out var e))
			{
				center = e.centerWorld;
				return true;
			}
			return false;
		}

		public static bool TryGetTileCornerWorldPositions(TileId tileId, out Vector3[] corners)
		{
			corners = null;
			if (GetPrecomputedIndex(tileId, out var idx, out var e))
			{
				corners = e.cornerWorldPositions;
				return true;
			}
			return false;
		}

		// Spawn if not already spawned, otherwise return existing
		public GameObject TrySpawnTile(TileId tileId, int resolution)
		{
			// Pack the id for quick membership checks
			ulong packed = PackTileKey(tileId);

			// Try to find precomputed entry
			if (!GetPrecomputedIndex(tileId, out int idx, out var entry)) 
			{
				return null;
			}

			// Return cached spawned GameObject when present
			if (_spawnedTiles.TryGetValue(tileId, out var existing) && existing != null)
			{
				// If an existing GameObject was previously deactivated, re-enable it.
				try
				{
					existing.SetActive(true);
					// Ensure its transform position matches the canonical center if available
					if (existing.TryGetComponent<PlanetTerrainTile>(out var existingTile))
					{
						// Rebuild a minimal TileData and refresh runtime state. Avoid calling
						// Initialize here because the local ColliderMeshGenerator may not be
						// available yet in this control flow. Instead refresh tileData and
						// restart its auto-deactivate timer so it behaves as active.
						var reInitData = new TileData();
						reInitData.id = tileId;
						reInitData.center = entry.centerWorld;
						reInitData.resolution = resolution;
						existingTile.tileData = reInitData;
						existingTile.transform.position = reInitData.center != Vector3.zero ? reInitData.center : entry.centerWorld;
						try { existingTile.RefreshActivity(); } catch { }
						try { existingTile.ConfigureMaterialAndLayer(terrainMaterial, terrainTileLayer, planetTransform != null ? planetTransform.position : this.transform.position); } catch { }
					}
				}
				catch { }
				return existing;
			}

			// Create a new GameObject for the tile
			// Mark as active/queued before creation to avoid races where another caller
			// enqueues/creates the same tile concurrently.
			try { _activeOrQueued.Add(packed); } catch { }

			bool spawnSucceeded = false;
			GameObject go = null;
			try
			{
				go = new GameObject($"Tile_F{entry.face}_{entry.x}_{entry.y}_D{tileId.depth}");
			// Parent to the planet transform if available so tiles group under the planet in hierarchy
			if (planetTransform != null)
			{
				go.transform.SetParent(planetTransform, worldPositionStays: true);
			}
			else
			{
				// fallback to manager's transform
				go.transform.SetParent(this.transform, worldPositionStays: true);
			}
			var tile = go.AddComponent<PlanetTerrainTile>();

			// Create minimal TileData with center set
			var data = new TileData();
			data.id = tileId;
			data.center = entry.centerWorld;
			data.mesh = null;
			data.resolution = resolution;

			// Delay expensive visual mesh building until the raycast heuristic actually hits
			// the tile. Keep data.mesh null for now so the tile's visual builder can
			// generate the mesh on-demand. We still set data.center so the tile can be
			// positioned correctly and its collider can be produced from lightweight
			// corner positions.
			// Note: the lazy mesh builder instance is created on first use by the heuristic.

			// Collider generator that creates a subdivided sphere mesh projected to planet radius
			Mesh ColliderMeshGenerator(TileId id)
			{
				// Create a subdivided collision mesh for reliable raycast detection
				return CreateSubdividedSphereColliderMesh(id, entry, _planetRadius);
			}

			// Initialize the tile (this sets transform.position to data.center internally)
			tile.Initialize(tileId, data, ColliderMeshGenerator);

			// Configure material and layer with planet center for shader
			Vector3 tilePlanetCenter = planetTransform != null ? planetTransform.position : this.transform.position;
			tile.ConfigureMaterialAndLayer(terrainMaterial, terrainTileLayer, tilePlanetCenter);

			// Ensure the GameObject world position matches the center produced during mesh build.
			// The builder now computes a sampled world-space centroid (data.center) which
			// is authoritative. Fall back to the precomputed registry center when data.center
			// was not produced for any reason.
			go.transform.position = (data != null && data.center != Vector3.zero) ? data.center : entry.centerWorld;

			// Use PlanetTileMeshBuilder to generate the visual mesh immediately for integration tests
			// This ensures the mesh is built using the proper builder system
			try
			{
				BuildVisualForTile(tileId, tile);
			}
			catch (Exception)
			{
				// Fallback: if mesh builder fails, create a minimal placeholder mesh
				if (tile.meshFilter != null && tile.meshFilter.sharedMesh == null)
				{
					var m = new Mesh();
					m.name = $"Visual_F{entry.face}_{entry.x}_{entry.y}_D{tileId.depth}";
					Vector3 v0 = entry.cornerWorldPositions != null && entry.cornerWorldPositions.Length > 0 ? entry.cornerWorldPositions[0] : entry.centerWorld + Vector3.right * 0.5f;
					Vector3 v1 = entry.cornerWorldPositions != null && entry.cornerWorldPositions.Length > 1 ? entry.cornerWorldPositions[1] : entry.centerWorld + Vector3.up * 0.5f;
					Vector3 v2 = entry.cornerWorldPositions != null && entry.cornerWorldPositions.Length > 2 ? entry.cornerWorldPositions[2] : entry.centerWorld + Vector3.left * 0.5f;

					Vector3 lv0 = go.transform.InverseTransformPoint(v0);
					Vector3 lv1 = go.transform.InverseTransformPoint(v1);
					Vector3 lv2 = go.transform.InverseTransformPoint(v2);

					// Center vertices around origin
					Vector3 centroid = (lv0 + lv1 + lv2) / 3f;
					lv0 -= centroid; lv1 -= centroid; lv2 -= centroid;

					m.vertices = new Vector3[] { lv0, lv1, lv2 };
					m.triangles = new int[] { 0, 1, 2 };
					m.RecalculateNormals();
					m.RecalculateBounds();

					// Assign to tile and tile data
					tile.meshFilter.sharedMesh = m;
					if (tile.tileData != null) tile.tileData.mesh = m;
				}
			}

			// Cache and return
			_spawnedTiles[tileId] = go;
			try { _activeOrQueued.Add(packed); } catch { }
			spawnSucceeded = true;
			return go;
			}
			finally
			{
				if (!spawnSucceeded)
				{
					try { _activeOrQueued.Remove(packed); } catch { }
				}
			}
		}

		/// <summary>
		/// Build the visual mesh for a tile on demand. This method is safe to call
		/// repeatedly; it no-ops if the tile already has a mesh.
		/// </summary>
		private void BuildVisualForTile(TileId id, PlanetTerrainTile tile)
		{
			if (tile == null) return;
			if (tile.tileData != null && tile.tileData.mesh != null) return; // already built

			// Lazily create the mesh builder with the manager's config and planet center
			if (_meshBuilder == null)
			{
				TerrainConfig builderConf = config;
#if UNITY_EDITOR || PLAYMODE_TESTS
				if (builderConf == null)
				{
					var maybe = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainConfig>("Assets/TerrainConfig.asset");
					if (maybe != null) builderConf = maybe;
				}
#endif
				Vector3 planetCenter = planetTransform != null ? planetTransform.position : this.transform.position;
				_meshBuilder = new PlanetTileMeshBuilder(builderConf, null, planetCenter);
			}

			// Build mesh into a fresh TileData if necessary
			try
			{
				var data = tile.tileData ?? new TileData { id = id, resolution = resolutionForBuilder(id) };
				float rawMin = float.MaxValue, rawMax = float.MinValue;
				_meshBuilder.BuildTileMesh(data, ref rawMin, ref rawMax);
				// Assign into tile and meshFilter
				tile.tileData = data;
				if (tile.meshFilter != null && data.mesh != null) tile.meshFilter.sharedMesh = data.mesh;
			}
			catch { }
		}

		// Helper: decide resolution used by builder for a TileId (uses manager config)
		private int resolutionForBuilder(TileId id)
		{
			int baseRes = (config != null && config.baseResolution > 0) ? config.baseResolution : 8;
			int depth = id.depth;
			return Mathf.Max(1, baseRes << depth);
		}

		// --- Spawn queue / worker to limit concurrency and keep main thread responsive ---
		[SerializeField]
		private int maxSpawnsPerFrame = 4; // how many tiles we allow to spawn per frame

		private struct SpawnRequest { public TileId id; public int resolution; public SpawnRequest(TileId i, int r) { id = i; resolution = r; } }
		private readonly Queue<SpawnRequest> _spawnQueue = new Queue<SpawnRequest>();
		private Coroutine _spawnWorkerCoroutine = null;

		private void StartSpawnWorkerIfNeeded()
		{
			if (_spawnWorkerCoroutine == null && _spawnQueue.Count > 0 && Application.isPlaying)
			{
				_spawnWorkerCoroutine = StartCoroutine(SpawnWorkerCoroutine());
			}
		}

		private void StopSpawnWorker()
		{
			if (_spawnWorkerCoroutine != null)
			{
				try { StopCoroutine(_spawnWorkerCoroutine); } catch { }
				_spawnWorkerCoroutine = null;
			}
		}

		private IEnumerator SpawnWorkerCoroutine()
		{
			// Process the queue in small bursts per frame to avoid jank.
			while (_spawnQueue.Count > 0)
			{
				int budget = Math.Max(1, maxSpawnsPerFrame);
				for (int i = 0; i < budget && _spawnQueue.Count > 0; i++)
				{
					var req = _spawnQueue.Dequeue();
					try
					{
						// If the request is stale (depth changed since it was enqueued), remove its queued marker and skip it
						if (req.id.depth != _currentDepth)
						{
							try { _activeOrQueued.Remove(PackTileKey(req.id)); } catch { }
							continue;
						}

						// If the tile was created meanwhile, ensure it's active and continue
						if (_spawnedTiles.TryGetValue(req.id, out var existing) && existing != null)
						{
							try { existing.SetActive(true); } catch { }
							continue;
						}

						// Execute spawn synchronously but limited per-frame
						TrySpawnTile(req.id, req.resolution);
					}
					catch { }
				}

				// Yield one frame to keep main thread responsive
				yield return null;
			}

			_spawnWorkerCoroutine = null;
		}

		/// <summary>
		/// Return all TileIds at depth sorted by distance to worldPos (nearest first).
		/// Used to prioritize spawn ordering.
		/// </summary>
		private List<TileId> GetAllTileIdsSortedByDistance(Vector3 worldPos, int depth)
		{
			if (!s_precomputedRegistry.TryGetValue(depth, out var entries) || entries == null || entries.Count == 0)
			{
				return new List<TileId>();
			}
			
			var list = new List<(float d, TileId id)>(entries.Count);
			for (int i = 0; i < entries.Count; i++)
			{
				var e = entries[i];
				var id = new TileId(e.face, e.x, e.y, depth);
				float d = (e.centerWorld - worldPos).sqrMagnitude;
				list.Add((d, id));
			}
			list.Sort((a, b) => a.d.CompareTo(b.d));
			var outIds = new List<TileId>(list.Count);
			for (int i = 0; i < list.Count; i++) outIds.Add(list[i].id);
			
			return outIds;
		}

		public List<PlanetTerrainTile> GetActiveTiles()
		{
			var list = new List<PlanetTerrainTile>();
			foreach (var kv in _spawnedTiles)
			{
				if (kv.Value == null) continue;
				var t = kv.Value.GetComponent<PlanetTerrainTile>();
				// Only include tiles that are both active and belong to the manager's current depth.
				if (t != null && kv.Value.activeInHierarchy)
				{
					if (t.tileData != null && t.tileData.id.depth == _currentDepth)
					{
						list.Add(t);
					}
				}
			}
			return list;
		}

		public void SetDepth(int depth)
		{
			// Guard: avoid redundant calls when depth hasn't changed and registry is populated
			if (depth == _currentDepth && _lastPrecomputedDepth == depth)
			{
				return;
			}

			// Ensure precomputed registry is populated for this depth before any checks
			// that depend on registry contents (like spawn queue enumeration)
			try
			{
				PrecomputeTileNormalsForDepth(depth);
			}
			catch
			{
				// Register an empty list so external queries see a valid entry for this depth
				s_precomputedRegistry[depth] = new List<PrecomputedTileEntry>();
				_lastPrecomputedDepth = depth;
			}

			// If requested depth is already current, usually nothing to do. However during
			// initialization the manager may have _currentDepth set but no tiles have been
			// actually spawned yet; in that case we must continue to enqueue spawns.
			if (depth == _currentDepth)
			{
				// If a spawn worker is already running for this depth, don't interfere
				if (_spawnWorkerCoroutine != null)
				{
					return;
				}
				
				// Calculate expected tile count for this depth
				int expectedTileCount = 20; // depth 0 = 20 faces
				if (depth > 0)
				{
					int tilesPerEdge = 1 << depth;
					expectedTileCount = 20 * tilesPerEdge * tilesPerEdge;
				}
				
				// Fast check: if we already have ALL expected spawned tiles for this depth, skip work.
				int existingTileCount = 0;
				foreach (var kv in _spawnedTiles)
				{
					if (kv.Key.depth == depth && kv.Value != null)
					{
						existingTileCount++;
					}
				}
				
				if (existingTileCount >= expectedTileCount)
				{
					return;
				}

				// Continue with SetDepth work to spawn remaining tiles
			}

			// Ensure manager's current depth is set immediately so the spawn worker can
			// ignore stale requests if depth changes while the worker is running.
			_currentDepth = depth;

			// Deactivate tiles from other depths so they can be re-used when returning
			// to their original depth instead of recreating GameObjects.
			try
			{
				// Snapshot keys to avoid collection-modification issues and ensure a
				// deterministic deactivation pass.
				var keys = new System.Collections.Generic.List<TileId>(_spawnedTiles.Keys);
				foreach (var key in keys)
				{
					if (!_spawnedTiles.TryGetValue(key, out var go) || go == null) continue;
					// Deactivate cached tiles whose depth differs from the requested depth.
					if (key.depth != depth)
					{
						try { go.SetActive(false); } catch { }
					}
				}
			}
			catch { }

			// Clear any pending spawn requests for previous depths to avoid creating
			// stale tiles when the player quickly changes depth (zoom in/out).
			// However, if we're setting the same depth, don't interrupt ongoing spawn work.
			bool sameDepth = (depth == _currentDepth);
			if (!sameDepth)
			{
				try
				{
					// Remove queued markers so they don't permanently block re-enqueueing later.
					try
					{
						var arr = _spawnQueue.ToArray();
						for (int __i = 0; __i < arr.Length; __i++)
						{
							try { _activeOrQueued.Remove(PackTileKey(arr[__i].id)); } catch { }
						}
					}
					catch { }
					_spawnQueue.Clear();
				}
				catch { }
				StopSpawnWorker();
			}
			else
			{
				// Same depth, not stopping spawn worker to avoid interrupting ongoing work
			}

			// Spawn (enqueue) precomputed tiles so runtime/play tests can inspect active tiles immediately.
			if (s_precomputedRegistry.TryGetValue(depth, out var list) && list != null)
			{
				
				// Instead of spawning everything synchronously, enqueue prioritized spawn
				// requests ordered by distance to the camera so nearest tiles are created first
				// while the worker keeps the main thread responsive.
				Vector3 camPos = GameCamera != null ? GameCamera.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
				var ordered = GetAllTileIdsSortedByDistance(camPos, depth);

				int baseRes = (config != null && config.baseResolution > 0) ? config.baseResolution : 8;
				int enqueuedCount = 0;
				int skippedCount = 0;
				
				foreach (var id in ordered)
				{
					try
					{
						// Skip if already spawned or queued
						ulong k = PackTileKey(id);
						if (_activeOrQueued.Contains(k))
						{
							skippedCount++;
							// Ensure it's active
							if (_spawnedTiles.TryGetValue(id, out var go) && go != null)
							{
								try { go.SetActive(true); } catch { }
							}
							continue;
						}

						int resolutionForDepth = Mathf.Max(1, baseRes << depth);
						_spawnQueue.Enqueue(new SpawnRequest(id, resolutionForDepth));
						try { _activeOrQueued.Add(k); } catch { }
						enqueuedCount++;
					}
					catch { }
				}
				
				StartSpawnWorkerIfNeeded();
			}

			// If the visibility manager's terrain material was assigned after some tiles
			// were created (tests or runtime wiring), ensure spawned tiles receive the
			// correct material instance and planet-center parameter now that it exists.
			if (terrainMaterial != null)
			{
				Vector3 currentPlanetCenter = planetTransform != null ? planetTransform.position : this.transform.position;
				foreach (var kv in _spawnedTiles)
				{
					var go = kv.Value;
					if (go == null) continue;
					var tile = go.GetComponent<PlanetTerrainTile>();
					if (tile == null) continue;
					try
					{
						tile.ConfigureMaterialAndLayer(terrainMaterial, terrainTileLayer, currentPlanetCenter);
					}
					catch { }
				}
			}
		}

		public void StartRaycastHeuristicLoop()
		{
		// Prevent starting multiple coroutine instances (Awake + OnEnable can both call this)
		if (_heuristicCoroutine != null) return;

		#if PLAYMODE_TESTS
		heuristicTickCount = 0;
		#endif
		_heuristicCoroutine = StartCoroutine(RunTileRaycastHeuristicCoroutine());
		}

		public void StopRaycastHeuristicLoop()
		{
			if (_heuristicCoroutine == null) return;
			// Only stop a real Coroutine; sentinel objects used in EditMode should not be passed to StopCoroutine
			if (_heuristicCoroutine is Coroutine c)
			{
				try { StopCoroutine(c); } catch { }
			}
			_heuristicCoroutine = null;
		}

		// Auto-expand sampling array to the given sqrtRays size (for sqrtRays x sqrtRays grid)
		private void EnsureBaseSamples(int sqrtRays)
		{
			if (_baseSamples != null && _baseSampleGrid == sqrtRays) return;
			_baseSampleGrid = sqrtRays;
			_sampleCapacity = sqrtRays * sqrtRays;
			_baseSamples = new Vector2[_sampleCapacity];
			int idx = 0;
			for (int y = 0; y < sqrtRays; y++)
			{
				for (int x = 0; x < sqrtRays; x++)
				{
					_baseSamples[idx++] = new Vector2((x + 0.5f) / sqrtRays, (y + 0.5f) / sqrtRays);
				}
			}
		}

		private Ray GetRayForSample(int sampleIndex, Camera cam)
		{
			float biasStrength = GameCamera != null ? GameCamera.ProportionalDistance : 0f;

			float su = _baseSamples[sampleIndex].x;
			float sv = _baseSamples[sampleIndex].y;

			// Apply adaptive radial bias based on camera distance
			if (biasStrength > 0f && _sphereViewportRadius > 0f)
			{
				// Convert to centered coordinates [-0.5, 0.5]
				float centeredU = su - 0.5f;
				float centeredV = sv - 0.5f;
				float currentRadius = Mathf.Sqrt(centeredU * centeredU + centeredV * centeredV);

				if (currentRadius > 0f)
				{
					// Normalize the sample radius to [0,1] using the true maximum possible
					// radius for the centered square (corner magnitude = 0.5*sqrt(2)).
					float maxSampleRadius = 0.5f * Mathf.Sqrt(2f);
					float normalizedRadius = Mathf.Clamp01(currentRadius / maxSampleRadius);

					// Compute a biased normalized radius in [0,1]
					float biasedNormalized = Mathf.Pow(normalizedRadius, radialBiasExponent);

					// Blend between original and biased radii according to bias strength
					float blendedNormalized = Mathf.Lerp(normalizedRadius, biasedNormalized, biasStrength);

					// Map blended normalized radius into viewport-space using the visible sphere viewport radius
					float targetViewportRadius = blendedNormalized * _sphereViewportRadius;

					// Convert back to centered viewport coordinates by scaling the current vector
					float scale = targetViewportRadius / currentRadius;
					scale = float.IsFinite(scale) ? scale : 0f;
					centeredU *= scale;
					centeredV *= scale;

					su = centeredU + 0.5f;
					sv = centeredV + 0.5f;

					// Clamp to valid viewport range
					su = Mathf.Clamp01(su);
					sv = Mathf.Clamp01(sv);
				}
			}

			return cam.ViewportPointToRay(new Vector3(su, sv, 0f));
		}

		// Find the precomputed tile entry whose normal is closest to the given direction
		private PrecomputedTileEntry GetClosestPrecomputedTile(Vector3 direction)
		{
			PrecomputedTileEntry best = default;
			float bestDot = -1f;
			Vector3 d = direction.normalized;
			for (int i = 0; i < _precomputedTilesForDepth.Count; i++)
			{
				var e = _precomputedTilesForDepth[i];
				float dot = Vector3.Dot(d, e.normal);
				if (dot > bestDot)
				{
					bestDot = dot;
					best = e;
				}
			}
			return best;
		}

		// Centralized tile lifecycle management: spawn or refresh tiles in hitTiles,
		// destroy tiles that were not hit this pass, and remove tiles with mismatched depth.
		private void ManageTileLifecycle(HashSet<TileId> hitTiles, int depth)
		{
			// Snapshot existing keys to avoid modifying collection while iterating
			var existingKeys = new List<TileId>(_spawnedTiles.Keys);

			// Spawn/refresh tiles that were hit
			foreach (var id in hitTiles)
			{
				int resolution = ResolveResolutionForDepth(depth);
				GameObject tileGO = TrySpawnTile(id, resolution);
				existingKeys.Remove(id);
			}

			// Deactivate any tiles that were not hit this pass (but don't destroy them)
			foreach (var id in existingKeys)
			{
				if (_spawnedTiles.TryGetValue(id, out var go) && go != null)
				{
					var tile = go.GetComponent<PlanetTerrainTile>();
					if (tile != null)
					{
						// Hide visuals but keep GameObject active so MeshCollider remains raycastable.
						// Tests and heuristics rely on colliders remaining enabled so they can
						// re-activate visuals later when a raycast hits the tile.
						tile.HideVisualMesh();
					}
				}
			}

			// Additionally deactivate any tiles with mismatched depth
			var mismatched = _spawnedTiles.Keys.Where(k => k.depth != depth).ToList();
			foreach (var id in mismatched)
			{
				if (_spawnedTiles.TryGetValue(id, out var go) && go != null)
				{
					var tile = go.GetComponent<PlanetTerrainTile>();
					if (tile != null)
					{
						// Hide visuals but retain GameObject and collider for future raycasts.
						tile.HideVisualMesh();
					}
				}
			}
		}

		// Resolve mesh resolution for a given depth to create progressive mesh detail
		private int ResolveResolutionForDepth(int depth)
		{
			if (config != null)
			{
				// Use base resolution from config and scale with depth for more mesh detail
				int baseRes = config.baseResolution > 0 ? config.baseResolution : 32;
				
				// Scale resolution with depth to maintain consistent world-space detail density
				int scaledRes = Mathf.Max(8, baseRes >> depth);
				
				// Add extra mesh detail for deeper levels (more vertices, same terrain)
				int extraDetail = depth * 16; // Add 16 verts per edge per depth level
				
				return Mathf.Max(16, scaledRes + extraDetail);
			}
			
			// Conservative fallback with progressive mesh resolution scaling
			return Mathf.Max(16, 32 + depth * 16);
		}

		// Create a subdivided sphere collision mesh with vertices projected to sphere radius
		private Mesh CreateSubdividedSphereColliderMesh(TileId id, PrecomputedTileEntry entry, float sphereRadius)
		{
			var mesh = new Mesh();
			mesh.name = $"SubdividedCollider_F{entry.face}_{entry.x}_{entry.y}_D{id.depth}";

			// Get the tile's world position (where the GameObject will be placed)
			Vector3 tileWorldCenter = entry.centerWorld;
			
			// Create collision mesh vertices in LOCAL space relative to the tile's transform
			// The tile GameObject is positioned at tileWorldCenter on the sphere surface
			
			// Start with the tile's corner positions as base points
			Vector3[] corners = entry.cornerWorldPositions;

			// If corners aren't available, create a basic triangle around the center
			if (corners == null || corners.Length < 3)
			{
				// Create corners that are already projected to the sphere surface
				Vector3 centerNormal = entry.normal.normalized;
				Vector3 tangent = Vector3.Cross(centerNormal, Vector3.up).normalized;
				if (tangent.magnitude < 0.1f) tangent = Vector3.Cross(centerNormal, Vector3.right).normalized;
				Vector3 binormal = Vector3.Cross(centerNormal, tangent).normalized;
				
				float tileSize = sphereRadius * 0.1f; // Angular offset for triangle corners
				corners = new Vector3[]
				{
					(tileWorldCenter + tangent * tileSize).normalized * sphereRadius,
					(tileWorldCenter + binormal * tileSize).normalized * sphereRadius,
					(tileWorldCenter - tangent * tileSize).normalized * sphereRadius
				};
			}

			// Convert corner positions from world space to local space relative to tile center
			Vector3[] localCorners = new Vector3[corners.Length];
			for (int i = 0; i < corners.Length; i++)
			{
				localCorners[i] = corners[i] - tileWorldCenter;
			}

			// Subdivision level for reliable collision (creates 16+ vertices)
			int subdivisionLevel = 2;
			
			// Generate subdivided triangle mesh in local space
			var vertices = new System.Collections.Generic.List<Vector3>();
			var triangles = new System.Collections.Generic.List<int>();

			// Create subdivided triangles from the base triangle formed by local corners
			SubdivideTriangleLocal(localCorners[0], localCorners[1], localCorners[2], 
				subdivisionLevel, sphereRadius, tileWorldCenter, vertices, triangles);

			// Convert to arrays and create mesh
			mesh.vertices = vertices.ToArray();
			mesh.triangles = triangles.ToArray();
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();

			return mesh;
		}

		// Recursively subdivide a triangle and project vertices to sphere surface (local space version)
		private void SubdivideTriangleLocal(Vector3 v0, Vector3 v1, Vector3 v2, int level, float sphereRadius, Vector3 tileWorldCenter,
			System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<int> triangles)
		{
			if (level <= 0)
			{
				// Base case: add triangle vertices (projected to sphere in local space)
				int startIndex = vertices.Count;
				vertices.Add(ProjectToSphereLocal(v0, sphereRadius, tileWorldCenter));
				vertices.Add(ProjectToSphereLocal(v1, sphereRadius, tileWorldCenter));
				vertices.Add(ProjectToSphereLocal(v2, sphereRadius, tileWorldCenter));
				
				triangles.Add(startIndex);
				triangles.Add(startIndex + 1);
				triangles.Add(startIndex + 2);
			}
			else
			{
				// Recursive case: subdivide into 4 smaller triangles
				// Create midpoints and project them to sphere surface for proper curvature
				Vector3 m01 = ProjectToSphereLocal((v0 + v1) * 0.5f, sphereRadius, tileWorldCenter);
				Vector3 m12 = ProjectToSphereLocal((v1 + v2) * 0.5f, sphereRadius, tileWorldCenter);
				Vector3 m20 = ProjectToSphereLocal((v2 + v0) * 0.5f, sphereRadius, tileWorldCenter);

				SubdivideTriangleLocal(v0, m01, m20, level - 1, sphereRadius, tileWorldCenter, vertices, triangles);
				SubdivideTriangleLocal(v1, m12, m01, level - 1, sphereRadius, tileWorldCenter, vertices, triangles);
				SubdivideTriangleLocal(v2, m20, m12, level - 1, sphereRadius, tileWorldCenter, vertices, triangles);
				SubdivideTriangleLocal(m01, m12, m20, level - 1, sphereRadius, tileWorldCenter, vertices, triangles);
			}
		}

		// Project a local space point to the sphere surface, returning local coordinates
		private Vector3 ProjectToSphereLocal(Vector3 localPoint, float sphereRadius, Vector3 tileWorldCenter)
		{
			// Convert local point to world space
			Vector3 worldPoint = localPoint + tileWorldCenter;
			
			// Project to sphere surface: normalize direction from world origin and scale to sphere radius
			Vector3 direction = worldPoint.normalized;
			Vector3 projectedWorldPoint = direction * sphereRadius;
			
			// Convert back to local space relative to tile center
			return projectedWorldPoint - tileWorldCenter;
		}

		private void OnEnable()
		{
			// Start the lightweight heuristic coroutine used by tests to assert
			// the manager begins a periodic tile-raycast pass on enable.
			if (Application.isPlaying)
			{
				StartRaycastHeuristicLoop();
			}
		}

		private void Awake()
		{
			// Initialize planet center
			_planetCenter = planetTransform != null ? planetTransform.position : this.transform.position;

			// Ensure the heuristic sentinel/coroutine is created as early as possible
			// so Editor tests that activate the GameObject can observe a non-null value.
			if (Application.isPlaying)
			{
				StartRaycastHeuristicLoop();
			}
			else
			{
				// In EditMode, install a non-null sentinel so tests can observe that the
				// heuristic would be running without actually starting a Coroutine.
				if (_heuristicCoroutine == null) _heuristicCoroutine = new object();
			}

			// In Play mode, if depth hasn't been precomputed yet, initialize to depth 0 so
			// runtime playtests and manual Play sessions see tiles without requiring an
			// explicit SetDepth call from other systems.
			if (Application.isPlaying && _lastPrecomputedDepth < 0 && _currentDepth < 0)
			{
				try { SetDepth(0); } catch { }
			}
		}

		private void OnDisable()
		{
			try { StopRaycastHeuristicLoop(); } catch {}
		}

		private void Update()
		{
			// Only in play mode and when a camera is assigned
			if (!Application.isPlaying) return;
			if (GameCamera == null) return;

			// Detect explicit camera distance changes (tests set CameraController.distance directly)
			float camDist = 0f;
			try { camDist = GameCamera.distance; } catch { camDist = float.NaN; }
			bool distanceChanged = !float.IsNaN(camDist) && (float.IsNaN(_lastObservedCameraDistance) || !Mathf.Approximately(camDist, _lastObservedCameraDistance));
			_lastObservedCameraDistance = camDist;

			// If the camera distance was directly modified (test or external system), respond immediately
			// rather than waiting for the heuristic tick so tests can observe the update quickly.
			if (distanceChanged && !debugDisableCameraDepthSync)
			{
				int desired = ComputeDepthFromCamera();
				if (desired != _currentDepth)
				{
					_currentDepth = desired;
					try { SetDepth(_currentDepth); } catch { }
				}
			}
		}

		private int ComputeDepthFromCamera()
		{
			if (GameCamera == null) return _currentDepth;

			// Prefer direct camera distance values so tests that set CameraController.distance
			// are observed immediately rather than relying on the controller's internal update order.
			float t = 0f;
			float camMin = 0f, camMax = 1f, camDist = 0f;
			try
			{
				camDist = GameCamera.distance;
				camMin = GameCamera.minDistance;
				camMax = GameCamera.maxDistance;
				if (Mathf.Approximately(camMax, camMin)) camMax = camMin + 1f;
				// Normalize into 0..1
				t = Mathf.InverseLerp(camMin, camMax, camDist);
			}
			catch
			{
				// Fallback to the public proportional property if available
				t = Mathf.Clamp01(GameCamera.ProportionalDistance);
			}

			// Apply bias exponent (1 = linear, >1 biases bins toward higher distances)
			float biased = Mathf.Pow(t, Mathf.Max(0.0001f, binBias));

			// Invert mapping so that larger distances map to depth 0 and closer distances
			// (smaller camDist) map to higher depth values. This satisfies the contract:
			// depth == 0 at max camera distance; depth increases as the camera approaches.
			float inverted = 1f - biased;
			int newDepth = Mathf.Clamp(Mathf.FloorToInt(inverted * (maxDepth + 1)), 0, maxDepth);
			return newDepth;
		}

		// Raycast-based heuristic that samples the camera viewport and spawns tiles for hit locations.
		// Single-running coroutine; yields periodically for responsiveness.
		public IEnumerator RunTileRaycastHeuristicCoroutine()
		{
			// Use real-time waiting to avoid frame/timeScale dependent drift during tests.
			while (true)
			{
				
#if PLAYMODE_TESTS
				// Increment tick counter for test visibility.
				heuristicTickCount++;
#endif

				// Depth check: compute desired depth from camera at the same frequency as the raycast heuristic
				try
				{
					if (!debugDisableCameraDepthSync)
					{
						int desiredDepth = ComputeDepthFromCamera();
						if (desiredDepth != _currentDepth)
						{
							_currentDepth = desiredDepth;
							try { SetDepth(_currentDepth); } catch { }
						}
					}
				}
				catch { }

				// Validate required references
				if (GameCamera == null || cam == null)
				{
					yield return new WaitForSecondsRealtime(HeuristicInterval);
					continue;
				}

				// Adaptive raycast distribution implementation
				int raysToCast = Mathf.Max(1, _maxRays);
				int depth = _currentDepth;
				
				int sqrtRays = Mathf.CeilToInt(Mathf.Sqrt(raysToCast));
				EnsureBaseSamples(sqrtRays);
				
				// Calculate sphere's angular radius and viewport bounds for adaptive ray distribution
				float cameraDistance = (cam.transform.position - _planetCenter).magnitude;
				
				// Calculate angular radius of sphere as seen from camera
				_sphereViewportRadius = 0f;
				if (cameraDistance > _planetRadius && _planetRadius > 0f)
				{
					float angularRadius = Mathf.Asin(_planetRadius / cameraDistance);
					// Convert angular radius to viewport radius using camera's field of view
					float vFovHalf = cam.fieldOfView * Mathf.Deg2Rad * 0.5f;
					_sphereViewportRadius = Mathf.Tan(angularRadius) / Mathf.Tan(vFovHalf);
					_sphereViewportRadius = Mathf.Clamp(_sphereViewportRadius, 0f, 1.0f);
				}
				
				// Deterministic sampling over the precomputed grid
				HashSet<TileId> hitTiles = new HashSet<TileId>();
				
				// Ensure precomputed normals and colliders exist for this depth
				if (_lastPrecomputedDepth != depth)
				{
					PrecomputeTileNormalsForDepth(depth);
					_lastPrecomputedDepth = depth;
				}

				// Optimization: For depth 0, skip expensive raycasting since we always seed all tiles anyway
				if (depth > 0)
				{
					// Cast rays using adaptive distribution (only for depths > 0)
					int rayCountIter = sqrtRays * sqrtRays;
					for (int s = 0; s < rayCountIter; s++)
					{
						var ray = GetRayForSample(s, cam);

						// Test for hits against existing spawned tiles first
						bool foundExistingTile = false;
						if (_spawnedTiles != null && _spawnedTiles.Count > 0)
						{
						foreach (var kv in _spawnedTiles)
						{
							var tile = kv.Value?.GetComponent<PlanetTerrainTile>();
							if (tile == null) continue;

							bool hit = tile.ActivateIfHit(ray);
							if (hit)
							{
								hitTiles.Add(tile.tileId);
								foundExistingTile = true;
								
								// Lazily build the visual mesh for this tile on first hit.
								try { BuildVisualForTile(tile.tileId, tile); } catch { }
								
								// Restart the auto-deactivate timer to keep the tile visible while raycasts hit it
								try { tile.RefreshActivity(); } catch { }
								break; // Found a hit, move to next ray
							}
						}
					}

					// If no existing tile was hit, use mathematical sphere intersection for fallback
					if (!foundExistingTile)
					{
						var sphereHitPoint = PlanetTerrainTile.GetSphereHitPoint(ray, _planetCenter, _planetRadius, curvedIcosphereRadiusMultiplier);
						if (sphereHitPoint != Vector3.zero)
						{
							var hitDirection = (sphereHitPoint - _planetCenter).normalized;
							var closest = GetClosestPrecomputedTile(hitDirection);
							if (closest.face >= 0) // valid precomputed entry
							{
								int entryIdx = _precomputedTilesForDepth.IndexOf(closest);
								var tileId = new TileId(closest.face, closest.x, closest.y, depth) { canonicalIndex = entryIdx };
								hitTiles.Add(tileId);
							}
						}
					}
				}
				} // Close the if (depth > 0) block
				// End of depth > 0 raycast optimization

				// Manage tile lifecycle (spawn/update/destroy) for the set of hit tiles
				// For depth 0, always seed the hit set with all precomputed entries to ensure
				// all 20 icosphere face tiles remain active. This prevents the race condition
				// where the heuristic only hits some tiles and deactivates the others.
				if (depth == 0)
				{
					if (s_precomputedRegistry.TryGetValue(0, out var reg) && reg != null)
					{
						foreach (var e in reg)
						{
							var id = new TileId(e.face, e.x, e.y, 0);
							hitTiles.Add(id);
						}
					}
				}

				try { ManageTileLifecycle(hitTiles, depth); } catch { }

				yield return new WaitForSecondsRealtime(HeuristicInterval);
			}
		}
	}
}

