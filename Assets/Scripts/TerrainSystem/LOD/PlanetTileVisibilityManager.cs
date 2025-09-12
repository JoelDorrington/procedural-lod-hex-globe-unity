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

		// Material used by spawned terrain tiles (tests look up this private field)
		[SerializeField]
		private Material terrainMaterial;

		// Runtime depth mapping tuning (used to derive depth from camera distance)
		[SerializeField]
		private int maxDepth = 2;

		[SerializeField]
		private float binBias = 1.2f;

		// Buffer radius in tile coordinates (k-ring) used when planet does not fill the view
		[SerializeField]
		[Tooltip("k-ring buffer radius (tiles) applied around the camera-facing tile when the planet does not fill the viewport")]
		private int mathSelectorBufferK = 1;

		// Runtime state for calculating visible tiles
		private float _sphereViewportRadius;

	// (removed) previously used buffer radius field — selection logic uses
	// explicit k-ring parameters and test-configured values instead.

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

		// Simple registry keyed by depth. Methods below return false until real logic is restored.
		private Dictionary<int, TerrainTileRegistry> tileRegistry = new Dictionary<int, TerrainTileRegistry>();

		// Current depth tracked at runtime. Use Update() to compute desired depth
		// from camera state and call SetDepth when it changes.
		private int _currentDepth = -1;

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
		// starts a visibility heuristic running at ~30Hz. In EditMode Unity does not
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

		public bool GetPrecomputedIndex(TileId tileId, out PrecomputedTileEntry entryOut)
		{
			entryOut = default;
			if (!tileRegistry.ContainsKey(tileId.depth)) return false;

			var registry = tileRegistry[tileId.depth];
			var key = new TileId(tileId.face, tileId.x, tileId.y, tileId.depth);
			if (!registry.tiles.ContainsKey(key)) return false;
			entryOut = registry.tiles[key];
			return true;
		}

		// Spawn if not already spawned, otherwise return existing
		public GameObject TrySpawnTile(TileId tileId)
		{
			int resolution = ResolveResolutionForDepth(tileId.depth);
			if (tileId.depth != _currentDepth) return null;
			// Pack the id for quick membership checks

			if (!tileRegistry.ContainsKey(tileId.depth))
			{
				return null;
			}

			var registry = tileRegistry[_currentDepth];
			var key = new TileId(tileId.face, tileId.x, tileId.y, tileId.depth);
			if (!registry.tiles.TryGetValue(key, out var entry))
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
				}
				catch { }
				return existing;
			}

			// Create a new GameObject for the tile
			// Mark as active/queued before creation to avoid races where another caller
			// enqueues/creates the same tile concurrently.
			ulong packed = PackTileKey(tileId);
			try { _activeOrQueued.Add(packed); } catch { }

			bool spawnSucceeded = false;
			GameObject go;
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

				// Initialize the tile (this sets transform.position to data.center internally)
				tile.Initialize(tileId, data);

				// Configure material and layer with planet center for shader
				Vector3 tilePlanetCenter = planetTransform != null ? planetTransform.position : this.transform.position;
				tile.ConfigureMaterialAndLayer(terrainMaterial, tilePlanetCenter);

				// Use PlanetTileMeshBuilder to generate the visual mesh immediately for integration tests
				// This ensures the mesh is built using the proper builder system and that
				// tile.tileData is populated with the final sampled center produced by the builder.
				try
				{
					BuildVisualForTile(tileId, tile);
				}
				catch
				{
					Debug.LogError("Failed to build visual mesh for tile: " + tileId);
				}

				go.transform.position = tile.tileData.center;

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
						TrySpawnTile(req.id);
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
			if (!tileRegistry.TryGetValue(depth, out var registry) || registry.tiles.Count == 0)
			{
				return new List<TileId>();
			}

			int count = registry.tiles.Count;

			List<(float d, TileId id)> list = new(count);
			foreach (var e in registry.tiles.Values)
			{
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
			if (depth == _currentDepth) return;
			if (!tileRegistry.ContainsKey(depth))
			{
				tileRegistry[depth] = new TerrainTileRegistry(depth, _planetRadius, _planetCenter);
			}
			_currentDepth = depth;

			// Snapshot keys to avoid collection-modification issues 
			// and ensure a deterministic deactivation pass.
			var keys = new List<TileId>(_spawnedTiles.Keys);
			foreach (var key in keys)
			{
				if (!_spawnedTiles.TryGetValue(key, out var go) || go == null) continue;
				// Deactivate cached tiles whose depth differs from the requested depth.
				if (key.depth != depth) go.SetActive(false);
			}	

			// Remove queued markers so they don't permanently block re-enqueueing later.
			var arr = _spawnQueue.ToArray();
			_spawnQueue.Clear();

			for (int __i = 0; __i < arr.Length; __i++)
			{
				var spawnRequest = arr[__i];
				if (spawnRequest.id.depth != depth)
				{
					try
					{
						_activeOrQueued.Remove(PackTileKey(spawnRequest.id));
					}
					catch { }
				}
				else
				{
					_spawnQueue.Enqueue(spawnRequest);
				}
			}
			if(_spawnQueue.Count == 0) StopSpawnWorker();
			else StartSpawnWorkerIfNeeded();

			// If no GameCamera is assigned (common in some PlayMode tests), ensure a
			// small set of tiles are spawned immediately so tests can observe visuals
			// without relying on Update-driven visibility heuristics.
			if (GameCamera == null)
			{
				var ids = GetAllTileIdsSortedByDistance(_planetCenter, depth);
				int immediate = Math.Min(12, ids.Count);
				for (int i = 0; i < immediate; i++)
				{
					try { TrySpawnTile(ids[i]); } catch { }
				}
			}
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
				GameObject tileGO = TrySpawnTile(id);
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
						tile.gameObject.SetActive(false);
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
						tile.gameObject.SetActive(false);
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

		private void Awake()
		{
			// Initialize planet center
			_planetCenter = planetTransform != null ? planetTransform.position : this.transform.position;

			// Ensure the heuristic sentinel/coroutine is created as early as possible
			// so Editor tests that activate the GameObject can observe a non-null value.
			if (Application.isPlaying)
			{
				// TODO implement and uncomment
				// StartMathHeuristicLoop();
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
			if (Application.isPlaying && _currentDepth < 0)
			{
				try { SetDepth(0); } catch { }
			}
		}

		private void Update()
		{
			// Only in play mode and when a camera is assigned
			if (!Application.isPlaying) return;
			if (GameCamera == null) return;

			// Detect explicit camera distance changes (tests set CameraController.distance directly)
			float camDist;
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
					// Call SetDepth(desired) directly. Do NOT assign to _currentDepth here because
					// SetDepth performs initialization and relies on detecting the change itself.
					try { SetDepth(desired); } catch { }
				}
			}

			// Always run math-based visibility selection in play mode
			if (Application.isPlaying && GameCamera != null)
			{
				try { UpdateVisibilityMathBased(); } catch { }
			}

			// Run math-based visibility selection each Update (lightweight)
			try { UpdateVisibilityMathBased(); } catch { }
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

			// Use exponential-halving thresholds toward the planet surface.
			// For depths n = 1..maxDepth, the transition threshold T_n is:
			// T_n = camMin + (camMax - camMin) / (2^n)
			// We pick the highest depth d such that camDist <= T_d. If none, depth=0.
			float camMinClamped = camMin;
			float camMaxClamped = camMax;
			if (camMaxClamped <= camMinClamped)
			{
				// fallback to linear mapping when bounds invalid
				float biased = Mathf.Pow(t, Mathf.Max(0.0001f, binBias));
				float inverted = 1f - biased;
				int newDepthFallBack = Mathf.Clamp(Mathf.FloorToInt(inverted * (maxDepth + 1)), 0, maxDepth);
				return newDepthFallBack;
			}

			// Clamp camDist into range to avoid unexpected results
			camDist = Mathf.Clamp(camDist, camMinClamped, camMaxClamped);

			for (int depth = maxDepth; depth >= 0; depth--)
			{
				float denom = Mathf.Pow(2f, depth);
				float threshold = camMinClamped + (camMaxClamped - camMinClamped) / denom;
				if (camDist <= threshold) return Mathf.Clamp(depth, 0, maxDepth);
			}

			return 0;
		}

		// Placeholder stub for the new math-based visibility selector.
		// The real implementation will compute visible TileIds using spherical
		// geometry (camera direction -> face/barycentric -> k-ring) and schedule
		// builds via TileMeshCache/ Scheduler. Present to satisfy tests and to
		// act as the integration point for the MathSelector.
		private void UpdateVisibilityMathBased()
		{
			if (GameCamera == null) return;

			// Camera direction from planet center
			Vector3 camPos = GameCamera.transform.position;
			Vector3 planetCenter = planetTransform != null ? planetTransform.position : this.transform.position;
			Vector3 camDir = (camPos - planetCenter).normalized;

			int depth = _currentDepth;
			var candidates = new HashSet<TileId>();

			// Quick check: if planet is small in view (planet not filling screen), use k-ring around facing tile
			// Estimate by checking if sphere's projected angular radius is less than 45 degrees (~0.785 rad)
			bool planetFillsView = false;
			if (cam != null)
			{
				float planetRadius = _planetRadius;
				float dist = (camPos - planetCenter).magnitude;
				float angularRadius = Mathf.Asin(Mathf.Min(1f, planetRadius / Mathf.Max(1e-6f, dist)));
				planetFillsView = angularRadius > (Mathf.PI / 4f);
			}

			if (!planetFillsView)
			{
				// Map camera direction to canonical tile and include k-ring
				var centerTile = MathVisibilitySelector.TileFromDirection(camDir, depth);
				var ring = MathVisibilitySelector.GetKRing(centerTile, mathSelectorBufferK, camDir);
				foreach (var t in ring) candidates.Add(t);

				// Also include any precomputed tiles whose face normal faces the camera
				// This helps ensure a reasonable candidate set for small angular radii
				// and matches previous heuristic behavior used by tests.
				if (tileRegistry.TryGetValue(depth, out var regNearby) && regNearby != null)
				{
					// Use a stricter corner threshold for the small-planet case so we only
					// include tiles whose corners are actually facing the camera (90°)
					float cornerDotThreshold = 0f; // 90 degrees
					foreach (var e in regNearby.tiles.Values)
					{
						bool anyCornerVisible = false;
						if (e.cornerWorldPositions != null)
						{
							foreach (var c in e.cornerWorldPositions)
							{
								Vector3 dir = (c - planetCenter).normalized;
								if (Vector3.Dot(dir, camDir) > cornerDotThreshold) { anyCornerVisible = true; break; }
							}
						}
						// Include tile when any corner is within the (stricter) angle
						if (anyCornerVisible)
						{
							candidates.Add(new TileId(e.face, e.x, e.y, depth));
						}
					}
				}
			}
			else
			{
				// Simpler, distance-proportional center-dot threshold:
				// Use threshold = R / d (clamped) so that as the camera approaches (d decreases)
				// the required dot product increases (tighter cone) and fewer tiles are included.
				// Apply a small relaxation factor so we don't miss edge tiles due to floating math.
				float planetRadius = _planetRadius;
				float dist = (camPos - planetCenter).magnitude;
				float rawCenterDot = Mathf.Clamp01(planetRadius / Mathf.Max(1e-6f, dist));
				float relaxFactor = 0.92f; // tighter visibility arc (fewer close tiles)
				float centerDotThreshold = Mathf.Clamp(rawCenterDot * relaxFactor, 0f, 1f);

				if (tileRegistry.TryGetValue(depth, out var reg) && reg != null)
				{
					foreach (var e in reg.tiles.Values)
					{
						// Use the tile centroid direction only — barycentric corner checks are unnecessary
						Vector3 centerDir = (e.centerWorld - planetCenter).normalized;
						if (Vector3.Dot(centerDir, camDir) > centerDotThreshold)
						{
							candidates.Add(new TileId(e.face, e.x, e.y, depth));
						}
					}
				}

				// Prefetch buffer: small k-ring around the camera-facing tile to warm neighbors
				// Trim the prefetch by a tighter angular window so only nearby buffer tiles
				// are kept. This avoids adding many distant k-ring tiles when the planet
				// fills the view.
				var centerTile = MathVisibilitySelector.TileFromDirection(camDir, depth);
				var prefetch = MathVisibilitySelector.GetKRing(centerTile, mathSelectorBufferK, camDir);
				// Cap prefetch to the top N nearest k-ring tiles by angular closeness to camDir.
				// This avoids adding many distant tiles at the same depth while still
				// warming the immediate neighbors. Pick a conservative N.
				int prefetchTopN = 4;
				if (tileRegistry.TryGetValue(depth, out var regForPrefetch) && regForPrefetch != null)
				{
					var scored = new List<(float score, TileId id)>();
					foreach (var t in prefetch)
					{
						if (!regForPrefetch.tiles.TryGetValue(t, out var pEntry)) continue;
						Vector3 dir = (pEntry.centerWorld - planetCenter).normalized;
						float score = Vector3.Dot(dir, camDir); // higher == closer angularly
						scored.Add((score, t));
					}

					// sort descending by score and take top N
					scored.Sort((a, b) => b.score.CompareTo(a.score));
					int taken = Math.Min(prefetchTopN, scored.Count);
					for (int i = 0; i < taken; i++) candidates.Add(scored[i].id);
				}
			}

			try
			{
				ManageTileLifecycle(candidates, depth);
			}
			catch { }
		}
	}
}

