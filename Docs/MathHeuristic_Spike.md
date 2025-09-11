Math-Only Visibility Heuristic Spike
===================================

Goal
----
Replace the expensive raycast heuristic with a purely mathematical, deterministic selection of tiles to build and keep visible; we no longer need any colliders. Provide a predictive build scheduler so meshes finish building before they enter the camera view. Provide a global cache for terrain height meshes keyed by tile geometry.

High-level idea
---------------
- Use spherical geometry (dot product / angular distance) between the camera direction (from planet center) and each tile's face/center normal to choose candidate tiles.
- Avoid iterating all tiles. Instead, map cameraDir -> face and (u,v) barycentric coordinates then derive tile x,y indices for a given depth. Expand to a small neighborhood (k-ring) around that tile.
- Maintain a precomputed registry: depth -> list of (TileId, TileData) including the tile's center normal and barycentric center (u,v) and optionally precomputed neighbor indices.
- No collider prespawn: decide visibility using math (angular overlap with camera frustum / spherical cap). Do not construct MeshCollider.
- The K circle should be large enough to accomodate time to build before the tile is placed on screen. The K circle size should be recalculated a few times per second if the camera velocity has changed but not if the camera has stopped.

Mathematical core
-----------------
- Camera direction: c = normalize(cameraWorldPosition - planetCenter)
- Tile normal: n_i (from precomputed TileData)
- Cosine similarity: s_i = dot(c, n_i) = cos(theta_i)
- Angular distance: theta_i = arccos(s_i)

Selection rule:
- Pick tiles with s_i >= cos(maxAngle) (equivalently theta <= maxAngle).
- For ordered selection, choose top-K by descending s_i.
- Use depth-specific maxAngle or a depth -> angular footprint mapping.

Efficient candidate generation (avoid full scan)
------------------------------------------------
1. Map camera direction onto an icosphere face: use IcosphereMapping.BarycentricFromWorldDirection(face, c) or find closest face by testing precomputed face normals (20 faces). This is O(F) with F=20, cheap.
2. For chosen face and depth d: compute tile index (x,y) from barycentric coords and tilesPerEdge=1<<d. That gives the canonical tile covering the camera direction.
3. Expand to a local neighborhood (k-ring) around (x,y) on that face (and optionally neighbors on adjacent faces across edges). Choose k based on angular footprint at depth d.
4. Also include a small set of tiles from other faces if their precomputed face-normal dot with cameraDir is sufficiently large.

Time-to-view & predictive scheduling
------------------------------------
- Angular distance theta.
- If camera has angular velocity w (radians/sec) relative to planet center: estimate timeToEnter = max(0, (theta - viewAngleBuffer) / w).
- Measure average buildTimePerDepth[d] as moving average of actual build durations for depth d in the global mesh cache.
- Schedule build if buildTimePerDepth[d] <= timeToEnter - safetyMargin.
- If not enough time, schedule lower-res build (lower depth or coarse mesh) or increase parallelism if available.

Global mesh cache
-----------------
- Key: TileMeshKey(face,x,y,depth,meshQuality) or hashed TileId+resolution signature.
- Value: Mesh + lastUsedTime + buildDurationStats + sizeBytes + referenceCount.
- Policy: LRU by lastUsedTime with soft memory budget. When evicting, prefer removing high-depth/high-res meshes first.
- Async build: queue jobs to a WorkerPool (Unity JobSystem/Worker threads or Task.Run) that produce Mesh objects on a background thread if possible; final mesh assignment to MeshFilter.sharedMesh must occur on main thread.

No colliders
---------------------
- Use mathematical visibility / sphere intersection for all selection and debouncing.

Data structures & APIs
----------------------
- Precomputed registry (existing): depth -> List<PrecomputedTileEntry> where PrecomputedTileEntry contains normal, centerWorld, u/v, cornerWorldPositions.

- TileMeshCache (singleton):
  - Get(tileKey) -> Mesh or null
  - EnsureScheduled(tileKey, buildPriority) -> returns handle
  - Cancel(tileKey)
  - Add(mesh)
  - Stats: AvgBuildTime(depth), MemoryUsage

- Scheduler:
  - Accepts candidates (TileId, priority, depth)
  - Computes time-to-view and schedule decisions using build time estimates
  - Maintains worker queue and concurrency limit

Pseudocode (selection per frame)
-------------------------------

1. c = normalize(camera.position - planetCenter)
2. face = FindClosestFace(c) // test 20 face normals
3. (u,v) = BarycentricFromWorldDirection(face, c)
4. (x,y) = TileIndicesFromBarycentric(u,v, depth)
5. candidates = GetKRingTiles(face,x,y, depth, k)
6. for each tile in candidates:
     s = dot(c, tile.normal)
     if s < cos(maxAngleForDepth[depth]) continue
     theta = acos(s)
     timeToView = EstimateTimeToView(theta, cameraAngularVelocity)
     if TileMeshCache.Has(tileKey): ensure tile shown (assign mesh, show visuals)
     else TileMeshCache.EnsureScheduled(tileKey, priority = f(timeToView, s))

Priority function example: priority = (1/timeToView) * (1 + s)

Edge cases • concurrency
-----------------------
- Rapid camera teleport: if timeToView ~0, prefer lowest-res placeholder or show nothing until mesh builds; avoid overloading workers.
- Camera angular velocity near zero: fall back to angular distance only and use an absolute time threshold.
- Multiple cameras: compute c per camera and merge candidate sets.

Complexity & performance
------------------------
- Face lookup: O(20)
- K-ring expansion: O(k^2) could be 12-24. Not necessary if the whole planet is visible. The distance to the planet matters. The base selection ring must encompass all visible tiles. K-ring is for buffer tiles to avoid visual glitches
- Scheduling: O(#candidates) per several frames, tiny compared to raycasts across many samples. Max 32hz

Testing and metrics
-------------------
- Add microbenchmarks to measure average mesh build time per depth and worker throughput.
- Unit tests for mapping cameraDir->tile indices (tile coordinate mapping) and for k-ring expansions and face boundary handling.
- Integration test: emulate camera motion and verify meshes for nearest tiles are scheduled and available before entering view.

Implementation plan (spike -> MVP)
----------------------------------
1. Spike: implement a pure-math selector function that, given camera position & depth, returns a small set of TileIds expected to be visible (Docs/MathHeuristic_Spike.md) — DONE (this file)
2. Implement `TileMeshCache` (in `Assets/Scripts/TerrainSystem/LOD/TileMeshCache.cs`) with async build queue (Task-based), LRU eviction, stats.
3. Implement `MathVisibilityProvider` for tile visibility detection
4. Integrate predictive scheduler inside `PlanetTileVisibilityManager`: call TileMeshCache.EnsureScheduled for selected tiles.
5. Measure & tune: concurrency, k, maxAngle per depth.

Risks & mitigations
-------------------
- Risk: Background mesh generation - Unity meshes must be created/assigned on main thread. Mitigation: Generate vertex arrays & algorithms off-main-thread; create Mesh on main thread but only after computation finished; use small worker threads to compute geometry.
- Risk: Memory bloat from mesh cache. Mitigation: LRU eviction, memory budgeting, aggressive low-res fallback.
- Risk: Edge-face mapping complexity. Mitigation: rely on existing IcosphereMapping utilities and precomputed tile registry edges.

Next steps (deliverables)
-------------------------
- Prototype `MathSelector` (fast pure-math) and add a unit test asserting it returns expected TileIds for canonical camera directions.
- Implement `TileMeshCache` (async scheduler + LRU) and wire an MVP pipeline to build & assign meshes on demand.
- Replace raycast heuristic in `PlanetTileVisibilityManager` with `MathSelector` + scheduler behind a feature flag.

Notes
-----
This approach eliminates expensive per-frame raycasting and leverages deterministic spherical geometry for tile selection. Buffer K-ring and a global mesh cache preserve responsiveness and avoid the need for prespawning colliders.



Appendix: Quick pseudocode for `FindClosestFace` and `TileIndicesFromDirection`
------------------------------------------------------------------------------

FindClosestFace(c):
  best = -1; bestDot = -Inf
  for face in 0..19:
    dot = Dot(c, faceNormal[face])
    if dot > bestDot: bestDot = dot; best = face
  return best

TileIndicesFromDirection(face, c, depth):
  // convert world direction to barycentric (u,v) inside face
  (u,v) = IcosphereMapping.BarycentricFromWorldDirection(face, c)
  tilesPerEdge = 1 << depth
  x = Clamp(Floor(u * tilesPerEdge), 0, tilesPerEdge-1)
  y = Clamp(Floor(v * tilesPerEdge), 0, tilesPerEdge-1)
  // handle u+v>1 if mapping uses triangular barycentric layout
  return (x,y)
