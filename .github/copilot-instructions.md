
# Copilot Instructions: Planet Tile Streaming System (HexGlobeProject)

## Important Workflow Instructions
- Regex for find and replace to strip logs: `^ *Debug\.Log\(.*\r\n$`
- Maintain these instructions as high priority guidelines.
- You are allowed to violate these instructions only if explicitly permitted by the user.
- Do not run Unity commands. Unity is always open in another window.
- Request the human engineer to run the tests on your behalf.
- Analyse the request for logical flaws or contradictions before answering.
- If the request is ambiguous, ask for clarification.
- If the request is impossible, explain why.
- If the request is incomplete, ask for more details.
- Respond with questions if needed.
- Ask for debugger breakpoints, prefer over logging.
- Resist adding complexity to the concept tree.
- Avoid suggesting code that has been deleted in recent edits.
- Suggest elegant mathematical solutions where applicable.
- Only proceed with edits when the user and copilot have reached a consensus.
- Explain things in first principles before explaining the math and implementation.

## System Overview
This project implements a Google Maps-style tile explorer for procedural planet terrain with seamless LOD transitions and fractal detail zoom. The system uses camera-driven raycast heuristics to spawn tiles at appropriate detail levels.

## Core Architecture
- **Primary Components:**
  - `PlanetTileVisibilityManager.cs`: Main camera-driven tile lifecycle controller with raycast heuristics
  - `PlanetTileMeshBuilder.cs`: Procedural mesh generation with resolution-aware height sampling for icosphere faces by TileId
  - `PlanetTerrainTile.cs`: MonoBehaviour for individual terrain tile GameObjects encapsulating visual mesh and collider mesh. Not to be confused with the future game model tiles. 
  - `CameraController.cs`: Orbit camera with zoom-based depth control. The zoom is currently logarithmically scaled to be slower at lower depths. Soon I want this logarithmic slowing to be applied to all move axes.
  - `TileCache.cs`: Manages pooling and lifecycle of individual tile GameObjects
  - `TerrainConfig.cs`: Configuration for heightmap/terrain generation parameters

- **Data Types:**
  - `TileId`: Unique identifier for cube-sphere tiles
  - `TileData`: Canonical tile data: corner direction normals(3), mesh, resolution, height bounds, and spatial metadata
  - `TileFade` & `TileFadeAnimator`: Experimental animation system for smooth tile transitions. Abandoned for now.

- **Height Providers (Resolution-Aware):**
  - `SimplePerlinHeightProvider.cs`: Multi-octave Perlin noise. Main development provider.
  - `MountainRangeHeightProvider.cs`: Procedural continental + ridge-based  terrain (abandoned)
  - `OctaveMaskHeightProvider.cs`: Wrapper for octave-limited sampling (abandoned)

## Terrain Consistency & Progressive Detail
**CRITICAL:** The system ensures seamless terrain consistency across all depth levels:
- **Global Coordinate System:** All tiles use consistent global normalized coordinates that map the same world positions to identical height samples regardless of tile depth
- **Height Consistency:** Height providers must return IDENTICAL height values for the same world position regardless of the resolution parameter. This should be enforced by the heightmap generator/sampler.
- **Progressive Mesh Detail:** Higher depth tiles get exponentially more mesh resolution to show geometric detail of the SAME underlying terrain
- **Seamless Transitions:** No terrain popping or discontinuities when changing depth levels - only mesh density changes. No animations for now.

### Height Provider Requirements (CRITICAL)
- The `resolution` parameter should **NOT** affect the actual height values returned
- Height providers must return consistent terrain topology regardless of resolution
- Resolution is only used internally for mesh density calculations, not for terrain generation
- Same world position = same height value, always

## Important runtime behaviors (current implementation)
- Raycast heuristic: runs as a single coroutine at ≈30Hz. Samples a viewport grid (configurable _maxRays), computes mathematical ray-sphere intersections projected to a curved icosphere radius, and maps hit directions to the nearest precomputed tile center for the active depth.
- Precomputed tile normals: `PlanetTileVisibilityManager` builds a per-depth list of `PlanetTerrainTile` game objects with colliders for the raycast heuristic to snap to. This avoids expensive per-ray intersection tests with all tiles.
- The number of `PlanetTerrainTile` must always be equal to the number of icosphere faces at the current depth (20 * 4^depth). These are created on depth transitions and reused. 
- Tile caching: `TileCache` manages the lifecycle of individual tile GameObjects, including creation, pooling, and destruction.
- Tile lifecycle: a single `ManageTileLifecycle(HashSet<TileId> hitTiles, int depth)` call handles spawning/refreshing hit tiles and deactivating tiles not hit this pass.
- Layer handling: spawned tiles are placed on a dedicated `TerrainTiles` layer to prevent them from occluding the heuristic. The camera component stores the inspector `raycastLayerMask` and computes an effective mask for Physics checks at runtime.

Debug & editor helpers
- The camera component includes scene Gizmos for rays, hit points, and an optional persistent icosphere-outline mesh to visualize current tile tessellation per-depth. Toggleable options are exposed in the inspector for fast debugging.
- Throttled logging and sample caching are used to avoid console spam and flicker when the heuristic updates frequently.

Developer guidance and common pitfalls
- Height providers: never modify topology based on mesh resolution. Write deterministic sampling code: same direction → same height.

Testing checklist (quick)
- Enable `debugDrawRays` and `debugDrawIntersectionMarkers` in `PlanetTileExplorerCam` and run the scene in the Editor to confirm:
  - Each visible heuristic ray has exactly one magenta intersection marker.
  - Tiles are spawned and registered in the hierarchy under the configured parent transform.
- Toggle the `TerrainTiles` layer exclusion in the `raycastLayerMask` and validate the heuristic still sees the planet surface (tiles should not occlude the detection).

Notes and migration
- The persistent icosphere debug object is optional; remove or ignore when profiling rendering or investigating mesh builder behavior.

This document is the source of truth for the system architecture. Keep it updated with any changes to aid future development and maintenance. Do not change this document to match existing code. Only change this document to reflect intentional design changes made during the session.

Stay focused on the human engineer's goals. Ask questions if needed. Proceed only with consensus. Don't be afraid to tell the human engineer they are incorrect about their intuition. It's always the same guy this is a one man project. The human engineer will be dilligently resisting complexity creep. Do not allow them to veto necessary complexity. Always explain things in first principles before explaining the math and implementation.

The exchanges should be short and focused on one action item at a time.
Code edits must be made in a test driven manner. Always suggest new tests if needed.
NO EDITOR ONLY WORKAROUNDS ALLOWED. TESTS MUST EXPOSE PROBLEM CAUSES. CODE MUST FULFIL TESTS IN GOOD FAITH.