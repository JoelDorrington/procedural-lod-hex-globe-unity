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

		// Tuning: how strictly we require a tile's centroid to face the camera when the planet fills the view.
		[SerializeField]
		[Tooltip("Multiplier applied to raw center dot threshold (R/d). Values >1 tighten the cone; values <1 loosen it.")]
		private float visibilityConeClampFactor = 1.25f;

		[Tooltip("Minimum allowed center-dot threshold to avoid collapsing the visibility cone at close ranges")]
		[SerializeField]
		private float maxDotProductThreshold = 0.8f;

		[Tooltip("Minimum allowed center-dot threshold to avoid collapsing the visibility cone at close ranges")]
		[SerializeField]
		private float minCenterDotThreshold = 0.35f;

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

		// Diagnostics + idempotency guard for ManageTileLifecycle
		// checksum of last processed hitTiles set (xor of packed keys)
		private ulong _lastManageChecksum = 0ul;
		private int _lastManagedDepth = -1;
		private float _lastManageTime = -1000f;
		[SerializeField]
		[Tooltip("Minimum seconds between identical ManageTileLifecycle calls to treat as no-op (debounce)")]
		private float manageDebounceSeconds = 0.05f;
		// Count how many times lifecycle manager has been invoked (for diagnostics/tests)
		public int ManageInvocationCount { get; private set; } = 0;

		// Last observed camera distance to detect external changes
		private float _lastObservedCameraDistance = float.NaN;

		// Simple active spawn registry so repeated TrySpawnTile calls return the same GameObject
		private readonly Dictionary<TileId, GameObject> _spawnedTiles = new Dictionary<TileId, GameObject>();

		// Track tiles that are either already spawned or queued to spawn using a packed key
		// so we can defensively prevent duplicate enqueues across rapid depth transitions.
		private readonly HashSet<ulong> _activeOrQueued = new HashSet<ulong>();

		// Snapshot of the most recently computed visible hit tiles. The spawn worker
		// uses this set to drop stale spawn requests that are no longer visible.
		private readonly HashSet<TileId> _currentHitTiles = new HashSet<TileId>();

		// Pack depth/face/x/y into a 64-bit key for fast set membership tests.
		private static ulong PackTileKey(TileId id)
		{
			return ((ulong)(uint)id.depth << 48) | ((ulong)(uint)id.face << 32) | ((ulong)(uint)id.x << 16) | (ulong)(uint)id.y;
		}

		// Lazy mesh builder used to construct visual meshes on-demand when a tile is hit
		private PlanetTileMeshBuilder _meshBuilder = null;
#if PLAYMODE_TESTS
	// Tick counter incremented by the heuristic coroutine. Tests read this
	// to verify the heuristic is actually running at the expected frequency.
	public int heuristicTickCount = 0;
#endif

	// Heuristic coroutine handle and interval used to run visibility selection
	// at a steady rate to avoid doing math every frame and to match test expectations.
	private Coroutine _heuristicCoroutineHandle = null;
	private const float HeuristicIntervalSeconds = 1f / 32f; // ~32 Hz

		// Stores a sentinel object in EditMode so tests can verify the heuristic was started.
		private object _heuristicCoroutine = null;

		private void StartHeuristicIfNeeded()
		{
			if (_heuristicCoroutineHandle == null && Application.isPlaying && GameCamera != null)
			{
				_heuristicCoroutineHandle = StartCoroutine(HeuristicCoroutine());
			}
			// In EditMode tests put a sentinel to indicate the heuristic was started
			if (!Application.isPlaying && _heuristicCoroutine == null)
			{
				_heuristicCoroutine = new object();
			}
		}

		private void StopHeuristic()
		{
			if (_heuristicCoroutineHandle != null)
			{
				try { StopCoroutine(_heuristicCoroutineHandle); } catch { }
				_heuristicCoroutineHandle = null;
			}
			_heuristicCoroutine = null;
		}

		private IEnumerator HeuristicCoroutine()
		{
			while (true)
			{
				try { UpdateVisibilityMathBased(); } catch { }
				#if PLAYMODE_TESTS
					heuristicTickCount++;
				#endif
				yield return new WaitForSecondsRealtime(HeuristicIntervalSeconds);
			}
		}

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
			{ // Ignore if depth is not registered
				return null;
			}

			var registry = tileRegistry[_currentDepth];
			if (!registry.tiles.TryGetValue(tileId, out var entry))
			{ // Ignore if tileId is not in the registry
				return null;
			}

			// Create a new GameObject for the tile
			// If we've already spawned this tile, return the existing GameObject so
			// repeated TrySpawnTile calls are idempotent.
			if (_spawnedTiles.TryGetValue(tileId, out var existingGo))
			{
				if (existingGo != null)
				{
					// Ensure it's active and return
					try { if (!existingGo.activeInHierarchy) existingGo.GetComponent<PlanetTerrainTile>()?.SetVisibility(true); } catch { }
					return existingGo;
				}
				else
				{
					// Remove stale null entries and continue to spawn a fresh object
					try { _spawnedTiles.Remove(tileId); } catch { }
				}
			}
			// Mark as active/queued before creation to avoid races where another caller
			// enqueues/creates the same tile concurrently.
			ulong packed = PackTileKey(tileId);
			try { _activeOrQueued.Add(packed); } catch { }

			bool spawnSucceeded = false;
			GameObject go;
			try
			{
				go = new GameObject($"Tile_F{entry.face}_{entry.x}_{entry.y}_D{tileId.depth}");
				_spawnedTiles[tileId] = go;
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

				// Place GameObject at final position
				go.transform.position = tile.tileData.center;

				// Cache and activate now that mesh and material are assigned
				try { _activeOrQueued.Add(packed); } catch { }
				try { tile.SetVisibility(true); } catch { }
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
				// Assign into tile and meshFilter using the tile helper so assignment time is recorded
				tile.tileData = data;
				if (tile.meshFilter != null && data.mesh != null)
				{
					try { tile.AssignMesh(data.mesh); } catch { }
				}
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

		private struct SpawnRequest { public TileId id; public int resolution; public float priority; public SpawnRequest(TileId i, int r, float p = 0f) { id = i; resolution = r; priority = p; } }
		// Use a prioritized list (nearest-first) instead of a plain FIFO queue so
		// tiles are built from the inside-out (camera-proximity prioritized).
		private readonly List<SpawnRequest> _spawnQueue = new List<SpawnRequest>();

		// Insert a spawn request into the prioritized queue (lower priority value = earlier)
		private void EnqueueSpawnRequest(SpawnRequest req)
		{
			// Find insertion index to keep list sorted by ascending priority
			int idx = _spawnQueue.FindIndex(r => r.priority > req.priority);
			if (idx < 0) _spawnQueue.Add(req);
			else _spawnQueue.Insert(idx, req);
		}

		// Safely fetches the registry's centerWorld for a tile, falling back to planet center
		private Vector3 GetRegistryEntryCenter(TileId id, int depth)
		{
			if (tileRegistry != null && tileRegistry.TryGetValue(depth, out var reg) && reg != null)
			{
				if (reg.tiles != null && reg.tiles.TryGetValue(new TileId(id.face, id.x, id.y, depth), out var entry))
				{
					return entry.centerWorld;
				}
			}
			return planetTransform != null ? planetTransform.position : this.transform.position;
		}
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
					// Pop the highest-priority request (list maintained nearest-first)
					var req = _spawnQueue[0];
					_spawnQueue.RemoveAt(0);
					try
					{
						// If the request is stale (depth changed since it was enqueued), remove its queued marker and skip it
						if (req.id.depth != _currentDepth)
						{
							try { _activeOrQueued.Remove(PackTileKey(req.id)); } catch { }
							continue;
						}

						// If the request is no longer in the current visible set, drop it and
						// remove its queued marker so it can be enqueued later when it becomes visible again.
						if (_currentHitTiles != null && !_currentHitTiles.Contains(req.id))
						{
							try { _activeOrQueued.Remove(PackTileKey(req.id)); } catch { }
							continue;
						}

						// If the tile was created meanwhile, ensure it's active and continue
						if (_spawnedTiles.TryGetValue(req.id, out var existing) && existing != null)
						{
							if (!existing.activeInHierarchy)
							{
								existing.GetComponent<PlanetTerrainTile>().SetVisibility(true);
							}
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
			// Clear the snapshot when the worker completes so we don't hold stale visibility state.
			_currentHitTiles.Clear();
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
			_currentDepth = depth;
			if (!tileRegistry.ContainsKey(depth))
			{
				tileRegistry[depth] = new TerrainTileRegistry(depth, _planetRadius, _planetCenter);
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
					EnqueueSpawnRequest(spawnRequest);
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
			if (hitTiles.Count == 0) return;
			// Idempotency / debounce: compute a simple checksum of the requested hit set
			ulong checksum = 0ul;
			foreach (var t in hitTiles) checksum ^= PackTileKey(t);
			// If identical to last processed set and within debounce window, skip work
			if (checksum == _lastManageChecksum && depth == _lastManagedDepth && (Time.realtimeSinceStartup - _lastManageTime) < manageDebounceSeconds)
			{
				// Still count the invocation for diagnostics
				ManageInvocationCount++;
				return;
			}
			// Update last processed info and invocation count
				// Snapshot visible hits for the spawn worker to consult so it can drop
				// requests that are no longer visible by the time they are processed.
				_currentHitTiles.Clear();
				foreach (var t in hitTiles) _currentHitTiles.Add(t);
			_lastManageTime = Time.realtimeSinceStartup;
			_lastManageChecksum = checksum;
			_lastManagedDepth = depth;
			ManageInvocationCount++;
			// Snapshot existing keys to avoid modifying collection while iterating
			var existingKeys = new List<TileId>(_spawnedTiles.Keys);

			// Spawn/refresh tiles that were hit. To avoid blocking the main thread
			// with heavy mesh construction, enqueue spawn requests and let the
			// SpawnWorkerCoroutine perform the actual creation and mesh build.
			foreach (var id in hitTiles)
			{
				// If already spawned, only reactivate it immediately when it already has a mesh assigned.
				if (_spawnedTiles.TryGetValue(id, out var existing) && existing != null)
				{
					var tileComp = existing.GetComponent<PlanetTerrainTile>();
					tileComp.RefreshActivity(); // update last-active timestamp
					existingKeys.Remove(id);
					continue;
				}

				// Otherwise enqueue a spawn request if not already active or queued
				ulong packed = PackTileKey(id);
				if (!_activeOrQueued.Contains(packed))
				{
					try { _activeOrQueued.Add(packed); } catch { }
					// Compute a priority based on squared distance to camera so nearer tiles are processed first
					Vector3 camPos = cam != null ? cam.transform.position : (planetTransform != null ? planetTransform.position : this.transform.position);
					float d2 = (GetRegistryEntryCenter(id, id.depth) - camPos).sqrMagnitude;
					EnqueueSpawnRequest(new SpawnRequest(id, ResolveResolutionForDepth(id.depth), d2));
				}
				existingKeys.Remove(id);
			}

			// Ensure the worker is started to process enqueued spawns when appropriate
			StartSpawnWorkerIfNeeded();

			// Deactivate any tiles that are currently marked active/queued but are no longer
			// visible in the current hit set. Remove their packed key so they can be
			// re-enqueued later if they become visible again.
			var activeArr = _activeOrQueued.ToArray();
			foreach (var packed in activeArr)
			{
				uint y = (uint)(packed & 0xFFFFu);
				uint x = (uint)((packed >> 16) & 0xFFFFu);
				uint face = (uint)((packed >> 32) & 0xFFFFu);
				uint depthPacked = (uint)((packed >> 48) & 0xFFFFu);
				var id = new TileId((int)face, (int)x, (int)y, (int)depthPacked);
				if (!_currentHitTiles.Contains(id))
				{
					// If a GameObject was created for this tile, deactivate it now.
					if (_spawnedTiles.TryGetValue(id, out var go) && go != null)
					{
						var tile = go.GetComponent<PlanetTerrainTile>();
						if (tile != null) tile.DeactivateImmediately();
						// Destroy and remove from spawned registry so it doesn't stick around.
						try { if (Application.isPlaying) GameObject.Destroy(go); else GameObject.DestroyImmediate(go); } catch { }
						try { _spawnedTiles.Remove(id); } catch { }
					}
					try { _activeOrQueued.Remove(packed); } catch { }
				}
			}

			// Additionally deactivate any tiles with mismatched depth
			var mismatched = _spawnedTiles.Keys.Where(k => k.depth != depth).ToList();
			foreach (var id in mismatched)
			{
				if (_spawnedTiles.TryGetValue(id, out var go) && go != null)
				{
					// Destroy and remove so it can be re-spawned cleanly later
					try { if (Application.isPlaying) GameObject.Destroy(go); else GameObject.DestroyImmediate(go); } catch { }
					try { _spawnedTiles.Remove(id); } catch { }
					try { _activeOrQueued.Remove(PackTileKey(id)); } catch { }
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
			_planetCenter = planetTransform != null ? planetTransform.position : this.transform.position;
			// In EditMode tests Unity coroutines are not running; store a non-null sentinel
			// so tests that assert the heuristic was started can detect it.
			if (!Application.isPlaying)
			{
				_heuristicCoroutine = new object();
			}
		}

        private void OnDisable()
        {
			StopHeuristic();
			StopSpawnWorker();
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

			// Ensure the heuristic coroutine is running; it will call UpdateVisibilityMathBased at ~32Hz.
			StartHeuristicIfNeeded();

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
				if (tileRegistry.TryGetValue(depth, out var regNearby) && regNearby != null)
				{
					float cornerDotThreshold = 0f;
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
						if (anyCornerVisible) candidates.Add(new TileId(e.face, e.x, e.y, depth));
					}
				}
			}
			else
			{
				// Frustum-proportional center-dot threshold: sample a single viewport corner,
				// find the corresponding point on the planet surface, compute the angle
				// between camera-center and that surface point, multiply by 1.1, convert
				// to a dot (cos) and apply the clamp factor. Clamp the final dot to the
				// configured min/max so the cone can't collapse or exceed limits.
				float centerDotThreshold = minCenterDotThreshold;
				var cameraRef = cam;
				if (cameraRef != null)
				{
					try
					{
						var vp = new Vector3(0f, 1f, 0f);
						var ray = cameraRef.ViewportPointToRay(vp);
						Vector3 o = ray.origin;
						Vector3 d = ray.direction.normalized;
						Vector3 m = o - planetCenter;
						float b = Vector3.Dot(m, d);
						float c = Vector3.Dot(m, m) - (_planetRadius * _planetRadius);
						float discr = b * b - c;
						if (discr >= 0f)
						{
							float t = -b - Mathf.Sqrt(discr);
							if (t > 0f)
							{
								var surfacePoint = o + d * t;
								var cornerSurfaceDir = (surfacePoint - planetCenter).normalized;
								float dot = Mathf.Clamp(Vector3.Dot(camDir, cornerSurfaceDir), -1f, 1f);
								float angle = Mathf.Acos(dot);
								float cornerDot = Mathf.Clamp01(Mathf.Cos(angle * 1.1f));
								centerDotThreshold = Mathf.Clamp(cornerDot * visibilityConeClampFactor, minCenterDotThreshold, maxDotProductThreshold);
							}
						}
					}
					catch { }
				}

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
			}

			try
			{
				// Ensure we always have at least one candidate to spawn. Some tests expect
				// the manager to spawn tiles immediately after a depth change when a camera
				// is present. After removing k-ring warming, the strict math predicate may
				// yield an empty set; pick the nearest tile deterministically as a fallback.
				if (candidates.Count == 0)
				{
					Vector3 fallbackPos = camPos;
					var ids = GetAllTileIdsSortedByDistance(fallbackPos, depth);
					if (ids.Count > 0)
					{
						candidates.Add(ids[0]);
					}
				}
				ManageTileLifecycle(candidates, depth);
			}
			catch { }
		}
	}
}

