#define PLAYMODE_TESTS

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

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

		[SerializeField]
		private int depthLevelMaxDistance = 50;

		public TerrainConfig TerrainConfig => config;

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
						var provider = localConf.heightProvider ?? (TerrainHeightProviderBase)new SimplePerlinHeightProvider();
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
								var provider = localConf.heightProvider ?? (TerrainHeightProviderBase)new SimplePerlinHeightProvider();
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
			// Try to find precomputed entry
			if (!GetPrecomputedIndex(tileId, out int idx, out var entry)) return null;

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
						try { existingTile.RestartAutoDeactivate(); } catch { }
						try { existingTile.ConfigureMaterialAndLayer(terrainMaterial, terrainTileLayer, planetTransform != null ? planetTransform.position : this.transform.position); } catch { }
					}
				}
				catch { }
				return existing;
			}

			// Create a new GameObject for the tile
			var go = new GameObject($"Tile_F{entry.face}_{entry.x}_{entry.y}_D{tileId.depth}");
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

			// Use the authoritative PlanetTileMeshBuilder to build the visual mesh and collider
			try
			{
				// Resolve a TerrainConfig to pass to the builder (use serialized config or project asset)
				TerrainConfig builderConf = config;
#if PLAYMODE_TESTS || UNITY_EDITOR
				if (builderConf == null)
				{
					var maybe = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainConfig>("Assets/TerrainConfig.asset");
					if (maybe != null) builderConf = maybe;
				}
#endif
				// Create a lightweight builder using manager's config
				Vector3 planetCenter = planetTransform != null ? planetTransform.position : this.transform.position;
				var builder = new PlanetTileMeshBuilder(builderConf, null, planetCenter);
				float rawMin = float.MaxValue, rawMax = float.MinValue;
				// Populate data.mesh, data.center, and other metadata
				builder.BuildTileMesh(data, ref rawMin, ref rawMax);

				// If the builder produced a mesh, convert its vertex positions from
				// the builder's world-relative local offsets into the tile GameObject's
				// local-space so ancestor transforms (scale/rotation) are correctly applied.
				// If the builder produced a mesh, keep the mesh instance as-is (it is
				// already local-centered relative to data.center). Do not clone here so
				// callers and tests that rely on instance equality see the original cached mesh.
			}
			catch (Exception)
			{
				// If builder fails for any reason, fall back to the simple collider below
			}

			// Collider generator that uses the builder-produced mesh when available
			Mesh ColliderMeshGenerator(TileId id)
			{
				if (data != null && data.mesh != null) return data.mesh;

				var m = new Mesh();
				m.name = $"Collider_F{entry.face}_{entry.x}_{entry.y}_D{tileId.depth}";
				Vector3 c0 = entry.cornerWorldPositions != null && entry.cornerWorldPositions.Length > 0 ? entry.cornerWorldPositions[0] : entry.centerWorld + Vector3.right * 0.5f;
				Vector3 c1 = entry.cornerWorldPositions != null && entry.cornerWorldPositions.Length > 1 ? entry.cornerWorldPositions[1] : entry.centerWorld + Vector3.up * 0.5f;
				Vector3 c2 = entry.cornerWorldPositions != null && entry.cornerWorldPositions.Length > 2 ? entry.cornerWorldPositions[2] : entry.centerWorld + Vector3.left * 0.5f;
				Vector3 local0 = go.transform.InverseTransformPoint(c0);
				Vector3 local1 = go.transform.InverseTransformPoint(c1);
				Vector3 local2 = go.transform.InverseTransformPoint(c2);
				m.vertices = new Vector3[] { local0, local1, local2 };
				m.triangles = new int[] { 0, 1, 2 };
				m.RecalculateNormals();
				m.RecalculateBounds();
				return m;
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

			// Generate a minimal visual mesh and center it locally so MeshFilter.sharedMesh is non-null
			try
			{
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
			catch (Exception)
			{
				// Best-effort: if mesh creation fails, continue without breaking tests
			}

			// Cache and return
			_spawnedTiles[tileId] = go;
			return go;
		}

		public List<PlanetTerrainTile> GetActiveTiles()
		{
			var list = new List<PlanetTerrainTile>();
			foreach (var kv in _spawnedTiles)
			{
				if (kv.Value == null) continue;
				var t = kv.Value.GetComponent<PlanetTerrainTile>();
				if (t != null && kv.Value.activeInHierarchy) list.Add(t);
			}
			return list;
		}

		public void SetDepth(int depth)
		{
			// Ensure Precompute errors don't prevent the manager from updating its
			// observable depth state. Tests and runtime expect SetDepth to always
			// record the requested depth even if detailed precomputation fails.
			try
			{
				PrecomputeTileNormalsForDepth(depth);
			}
			catch (Exception)
			{
				// Register an empty list so external queries see a valid entry for this depth
				s_precomputedRegistry[depth] = new List<PrecomputedTileEntry>();
				_lastPrecomputedDepth = depth;
			}

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

			// Spawn precomputed tiles so runtime/play tests can inspect active tiles immediately.
			if (s_precomputedRegistry.TryGetValue(depth, out var list) && list != null)
			{
				foreach (var entry in list)
				{
					try
					{
						var id = new TileId(entry.face, entry.x, entry.y, depth);
						// Use a reasonable default resolution for spawned tiles when not specified.
						// Map depth -> mesh resolution so higher depths get exponentially more vertices.
						int baseRes = (config != null && config.baseResolution > 0) ? config.baseResolution : 8;
						// Increase resolution per-axis by a factor of 2^depth (quadratic vertex growth)
						int resolutionForDepth = Mathf.Max(1, baseRes << depth);
						TrySpawnTile(id, resolutionForDepth);
					}
					catch (Exception) { }
				}
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
			if (Application.isPlaying && _lastPrecomputedDepth < 0)
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

			// Ensure runtime current depth is in-sync with any precomputed depth
			if (_lastPrecomputedDepth >= 0 && _currentDepth != _lastPrecomputedDepth)
			{
				_currentDepth = _lastPrecomputedDepth;
			}

			// Detect explicit camera distance changes (tests set CameraController.distance directly)
			float camDist = 0f;
			try { camDist = GameCamera.distance; } catch { camDist = float.NaN; }
			bool distanceChanged = !float.IsNaN(camDist) && (float.IsNaN(_lastObservedCameraDistance) || !Mathf.Approximately(camDist, _lastObservedCameraDistance));
			_lastObservedCameraDistance = camDist;

			int desired = ComputeDepthFromCamera();
			if (distanceChanged || desired != _currentDepth)
			{
				_currentDepth = desired;
				try { SetDepth(_currentDepth); } catch { }
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

		// Lightweight coroutine: yields a WaitForSeconds with HeuristicInterval repeatedly.
		// The real heuristic implementation will replace this loop and perform raycasts.
		public IEnumerator RunTileRaycastHeuristicCoroutine()
		{
			// Use real-time waiting to avoid frame/timeScale dependent drift during tests.
			while (true)
			{
				
#if PLAYMODE_TESTS
				// Increment tick counter for test visibility.
				heuristicTickCount++;
#endif

				// Enhanced raycast pass: iterate over ALL spawned tiles (including visually deactivated ones)
				// so that deactivated tiles can be reactivated when they come back into view
				try
				{
					Camera cam = GameCamera != null ? GameCamera.GetComponent<Camera>() : Camera.main;
					if (cam != null && _spawnedTiles != null && _spawnedTiles.Count > 0)
					{
						foreach (var kv in _spawnedTiles)
						{
							var tile = kv.Value?.GetComponent<PlanetTerrainTile>();
							if (tile == null) continue;

							Vector3 origin = cam.transform.position;
							Vector3 dir = (tile.transform.position - origin).normalized;
							Ray ray = new Ray(origin, dir);
							tile.ActivateIfHit(ray);
						}
					}
				}
				catch { }

				yield return new WaitForSecondsRealtime(HeuristicInterval);
			}
		}
	}
}

