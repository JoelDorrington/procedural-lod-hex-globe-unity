## Summary

Product: This game will be a multiplayer grand strategy turn based 4X relying on emergent complex system gameplay. Each cell will have a population (float), starting at 0 and a max population (int). Each cell with at least 1 population will have a mutable allegiance to one of the players. They will compete for the loyalty of their populations. Each cell may contain zero or one military units but not more. The military units will also have a mutable allegiance to one player.

Goal: Place a population spawn point on the cell graph. That cell will start with 1 population and a military unit will start there. It should be rendered on the planet surface if the depth is 4. In the play test I can select and deselect the military unit, and move it by right clicking. 
Do not worry about UI effects. Only the functionality matters now. No bugs.

Performance Critical: planet hex-cell model that is deterministic, cache-friendly, low-allocation, and optimized for high-throughput queries (neighbor lookup, region ops, hierarchical pathfinding). 

Assumptions
- The exact TileId bit-encoding/format is undecided. The design exposes a thin TileId → index mapping so the encoding can change without refactoring the core storage.
- Topology (connectivity) is mostly static at runtime. Mutable per-cell properties are supported but topology rebuilds are rare.

Design goals (succinct)
- Deterministic: same input → identical internal arrays.
- Contiguous memory: flat arrays or NativeArrays, minimal per-cell GC pressure.
- O(1) random lookup by TileId via a lookup table (dense or sparse depending on chosen encoding).
- Fast neighbor iteration with contiguous neighbor slices.
- Low per-query allocations: reuse heaps, buffers, and use version tagging for seen/visited.

Data layout (single canonical implementation)

Primary storage (Array-of-Structs, blittable):

- struct CellNode (blittable)
	- int index        // stable node index in arrays
	- int firstNeigh   // start index into flat neighbors[]
	- byte neighCount  // number of neighbors (typically 6)
	- int parent       // parent node index for hierarchy (-1 if none)
	- int childStart   // start index into children[]
	- byte childCount  // number of children
	- ushort flags     // region/attribute bitflags

Storage arrays:
- NativeArray<CellNode> nodes          // primary contiguous node records
- NativeArray<int> neighbors           // flat contiguous neighbor indices
- NativeArray<int> children            // flat child index list for hierarchy
- NativeArray<float3> centers         // optionally compressed; used for heuristics
- NativeArray<float> properties[...]   // separate arrays for multi-processed scalars (SOA)

Gameplay data model (runtime per-cell state)

- Each cell must expose a compact, mutable runtime state separate from topology. Keep it SOA to avoid per-cell GC pressure and enable SIMD/Jobs processing.

- Recommended CellState fields (managed arrays or NativeArrays):
	- float population        // current population (continuous)
	- int maxPopulation      // maximum population (capacity)
	- int allegiance         // player id owning the population (-1 = neutral)
	- byte hasUnit           // 0/1 flag whether a military unit occupies the cell
	- int unitId             // id of the unit occupying the cell (-1 if none)

- Notes:
	- Population is float for smooth simulation (growth, migration). Determinism requires fixed-step updates and deterministic RNG seeded per-simulation tick.
	- Store allegiance and unit ownership as compact ints so they can be bulk-processed and snapshot for networking.

API augmentation (gameplay and migration)

- Gameplay operations (mutation-safe, minimal locking):
	- bool TrySpawnPopulation(int nodeIndex, float amount) -> success
	- bool TryMoveUnit(int fromIndex, int toIndex, int unitId) -> success (checks occupancy and allegiance rules)
	- bool TryChangeAllegiance(int nodeIndex, int playerId)
	- float SamplePopulation(int nodeIndex)

- Migration helpers:
	- void MigrateFromMonoBehaviours(IEnumerable<MonoBehaviour> cells) — deterministic translation that emits topology and populates CellState arrays. Include a one-time migration map file for reproducibility.

Rendering and playtest notes

- Render condition: units and population markers are only rendered when a tile is visible at the target LOD (e.g., depth == 4). The rendering system should query `TryGetIndexForTileId` and the per-cell `hasUnit`/`population` arrays to decide visibility.
- For the playtest, keep selection/move logic in a small, deterministic controller that issues unit-move commands to the authoritative game model (no direct mutation from UI). This simplifies replay/debug and keeps model deterministic.

Lookup table (replacement for binary tree)

- indexFromTileId: a compact mapping structure with O(1) lookup semantics. Implementation options:
	- Dense array keyed by decoded TileId components (depth→face→localIndex → linear index). Fastest and simplest when TileId space is compact per-depth.
	- Sparse array + perfect-hash / flat dictionary when TileId space is sparse.
	- Int32 -> int mapping via an array or pooled dictionary as a fallback.

API contract (tiny, concrete)

- int TryGetIndexForTileId(int tileId, out int index)
	- Input: TileId (int, encoding undecided)
	- Output: node index (or -1)
	- Error modes: returns false if unmapped.

- ref readonly CellNode GetNode(int index)

- Span<int> ReadNeighbors(int index) or (int start, int count) GetNeighborSlice(int index)

- bool TryFindPath(int startIdx, int goalIdx, PathBuffer buffer, PathOptions opts)

- void BuildTopology(TopologyConfig cfg)
	- deterministic rebuild from config/seed.

Core algorithms (production-grade notes)

- Neighbor queries
	- O(1) neighbor iteration by reading nodes[index].firstNeigh..firstNeigh+neighCount from the flat neighbors array.

- A* / OpenSet
	- int indices everywhere; no object boxing.
	- Reusable binary heap with posInHeap[int nodeIndex] for decrease-key.
	- visitedSeenVersion[int nodeIndex] with globalSeenVersion++ to avoid clearing arrays.
	- Heuristic: use cached center directions and chord distance; optionally precompute approximate distance per LOD.

- Multi-source flood / Dijkstra
	- Push all sources into the heap initially.
	- For small integer costs, provide Dial’s bucketed queue variant.

- Hierarchical pathfinding
	- Build coarse LOD graph (precomputed parent links + adjacency at coarse level).
	- Run A* on coarse graph to get waypoint corridor.
	- Expand corridor to local high-resolution A* for final path.

Performance engineering

- Memory
	- Use NativeArray for hot arrays when running under Jobs/Burst. Provide managed fallback arrays for editor and tests.
	- Compress centers (float3) to int32/half when memory dominates; expose transform helpers.

- Allocation and GC
	- No per-query allocations in hot paths: preallocate PathBuffer, OpenSet heap, and scratch arrays.
	- Version tagging for seen/visited.

- Parallelism
	- Unity Jobs + Burst for large floods and batch pathfinding.
	- Design local scratch buffers per job instance; avoid shared locks via per-job version counters.

Quality and correctness

- Determinism
	- BuildTopology must be deterministic: sort adjacency/children lists by a stable key (tileId or index) before writing arrays.

- Tests
	- Unit tests: deterministic rebuild, neighbor reciprocity, small graph path correctness.
	- Microbenchmarks: heap throughput, neighbor iteration cost, TryGetIndexForTileId latency.
	- Integration: coarse→fine path matches fine-only path within position tolerance and is faster.

Edge cases and mitigations

- Sparse TileId spaces: fallback to a compact dictionary mapping for memory efficiency.
- Dynamic topology updates: allowed but expensive; require explicit RebuildTopology or incremental batch-updates with locks.

Implementation checklist (concrete steps)

1. Define blittable CellNode struct and core NativeArray-backed storage. (Done in code)
2. Implement BuildTopology that deterministically fills nodes, neighbors, children, centers. (Deterministic by sort keys)
2b. Implement compact `CellState` SOA arrays (population, allegiance, unit occupancy) and a deterministic migration path from existing per-cell MonoBehaviours.
3. Implement indexFromTileId abstraction with two concrete strategies: DensePerDepthArray and SparseMap fallback.
4. Implement reusable OpenSet heap with posInHeap and seenVersion arrays.
5. Implement TryFindPath (coarse LOD + refine mode) with versioned visited arrays.
5b. Wire simple unit move API and selection controller for the playtest (render units at depth 4 only).
6. Add unit tests: deterministic rebuild, neighbor correctness, path correctness, microbench heap.
6b. Add gameplay tests: population spawn, allegiance changes, unit move validation, and a deterministic playtest scenario.
7. Integrate optional Unity Jobs + Burst path for large workloads.

Next steps (practical)

- Pick TileId encoding (or provide two simple reference encodings) and wire the indexFromTileId implementation accordingly. This is intentionally left open so we can experiment with Morton vs hierarchical IDs.
- Convert existing per-cell classes/MonoBehaviours to a migration that populates the topology via BuildTopology.
- Add targeted tests and a microbenchmark harness under Tests/ to validate latency and determinism.

Requirements coverage

- Single production-grade plan: Done — this file prescribes one canonical implementation.
- Replace binary tree with lookup table: Done — indexFromTileId lookup table is the canonical approach.
- Leave TileId encoding undecided: Done — abstraction keeps encoding pluggable.

Completion summary

Production-focused implementation plan: flat, blittable arrays for topology, an O(1) lookup table for TileId → index, reusable heaps and version tagging to avoid GC.

Next: implement the storage types and BuildTopology, then the indexFromTileId concrete strategies and unit tests.