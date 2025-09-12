
# Copilot Instructions: Planet Tile Streaming System (HexGlobeProject)
“Perfection is achieved, not when there is nothing more to add, but when there is nothing left to take away.”
― Antoine de Saint-Exupéry, Airman's Odyssey

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
This project implements a Google Maps-style tile explorer for procedural planet terrain with seamless LOD transitions and fractal detail zoom. The system uses a 32hz update loop to calculate a mathematical ring of visibile tiles to build and spawn tiles at appropriate detail levels for the camera distance.

## Core Architecture
- **Primary Components:**
  - `PlanetTileVisibilityManager.cs`: Main terrain LOD manager. Computes visible tiles using spherical geometry and manages tile lifecycle.
  - `PlanetTileMeshBuilder.cs`: Procedural mesh generation with resolution-aware height sampling for icosphere faces by TileId
  - `PlanetTerrainTile.cs`: MonoBehaviour for individual terrain tile GameObjects encapsulating visual mesh. Not to be confused with the future game model tiles. 
  - `CameraController.cs`: Orbit camera with zoom-based depth control. The zoom is currently logarithmically scaled to be slower at lower depths. Soon I want this logarithmic slowing to be applied to all move axes.
  - `TileCache.cs`: Manages pooling and lifecycle of individual tile GameObjects
  - `TerrainConfig.cs`: Configuration for heightmap/terrain generation parameters

- **Data Types:**
  - `TileId`: Unique identifier for cube-sphere tiles
  - `TileData`: Canonical tile data: corner direction normals(3), mesh, resolution, height bounds, and spatial metadata
  - `TileFade` & `TileFadeAnimator`: Experimental animation system for smooth tile transitions. Abandoned for now.

- **Height Providers (Resolution-Aware):**
  - `SimplePerlinHeightProvider.cs`: Multi-octave Perlin noise. Main development provider.
  - `3DPerlinHeightProvider.cs`: 3D Multi-octave Perlin noise. Planned upgrade to eliminate noise distortion.
  - `MountainRangeHeightProvider.cs`: Procedural continental + ridge-based terrain (abandoned)
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
- Precomputed tile normals: `PlanetTileVisibilityManager` maintains a static registry of precomputed tile normals and barycentric centers for all depths to avoid recomputation
- The number of `PlanetTerrainTile` must always be equal to the number of icosphere faces at the current depth (20 * 4^depth). These are created on demand and reused via lifecycle management method. 
- Tile caching: `TileCache` manages the lifecycle of individual tile GameObjects, including creation, pooling, and destruction.
- Tile lifecycle: a single `ManageTileLifecycle(HashSet<TileId> hitTiles, int depth)` call handles spawning/refreshing hit tiles and deactivating tiles not hit this pass.

Developer guidance and common pitfalls
- SOLID principles apply.
- Also YAGNI: avoid overengineering. Implement only what is necessary.
- Dry code... bla bla bla all the clean code buzzwords. We're professionals here but we wont take ourselves too seriously.
- This is not legacy code. Refactor fearlessly. Everything is under version control and automated unit and integration tests.
- Avoid letting files get much larger than 500 lines. Split into multiple files if needed.
- Avoid monolithic classes. Each class should have a single responsibility.
- Height providers: never modify topology based on mesh resolution. Write deterministic sampling code: same direction → same height.

This document is the source of truth for the system architecture. Keep it updated with any changes to aid future development and maintenance. Do not change this document to match existing code. Only change this document to reflect intentional design changes made during the session.

Stay focused on the human engineer's goals. Ask questions if needed. Proceed only with consensus. Don't be afraid to tell the human engineer they are incorrect about their intuition. It's always the same guy this is a one man project. The human engineer will be dilligently resisting complexity creep. Do not allow them to veto necessary complexity. Always explain things in first principles before explaining the math and implementation.

The exchanges should be short and focused on one action item at a time.
Code edits must be made in a test driven manner. Always suggest new tests if needed.
NO EDITOR ONLY WORKAROUNDS ALLOWED. TESTS MUST EXPOSE PROBLEM CAUSES. CODE MUST FULFIL TESTS IN GOOD FAITH.