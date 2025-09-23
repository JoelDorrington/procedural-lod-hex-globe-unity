
# Copilot Instructions: Planet Tile Streaming System (HexGlobeProject)
“Perfection is achieved, not when there is nothing more to add, but when there is nothing left to take away.”
― Antoine de Saint-Exupéry, Airman's Odyssey

## Important Workflow Instructions
- Ensure all classes go in the appropriate namespace. No implicit namespacing.
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

- See `Docs/GLOSSARY.md` for project-specific terms and canonical definitions used across mapping, registry, and builder.

## System Overview
This project implements a Google Maps-style tile explorer for procedural planet terrain with seamless LOD transitions and fractal detail zoom. The runtime uses a 32Hz update loop to calculate a mathematical ring of visible tiles and to build/spawn tiles at appropriate detail levels for the camera distance. Recent development migrated the mapping math from the earlier cube-sphere approach to an icosphere-based mapping; the canonical implementation and formulas live in `Assets/Scripts/TerrainSystem/LOD/IcosphereMapping.cs`.

## Core Architecture
- **Primary Components:**
  - `PlanetTileVisibilityManager.cs`: Main terrain LOD manager. Computes visible tiles using spherical geometry and manages tile lifecycle.
  - `PlanetTileMeshBuilder.cs`: Procedural mesh generation with resolution-aware height sampling for icosphere faces by `TileId`.
  - `PlanetTerrainTile.cs`: MonoBehaviour for individual terrain tile GameObjects encapsulating visual mesh. Not to be confused with the future game model tiles. 
  - `CameraController.cs`: Orbit camera with zoom-based depth control. The zoom is currently logarithmically scaled to be slower at lower depths. Soon I want this logarithmic slowing to be applied to all move axes.
  - `TileCache.cs`: Manages pooling and lifecycle of individual tile GameObjects
  - `TerrainConfig.cs`: Configuration for heightmap/terrain generation parameters

- **Data Types:**
  - `TileId`: Unique identifier for icosphere tiles (face, x, y, depth)
  - `TileData`: Canonical tile data: corner direction normals(3), mesh, resolution, height bounds, and spatial metadata
  - `TileFade` & `TileFadeAnimator`: Experimental animation system for smooth tile transitions. Abandoned for now.

- **Height Providers (Resolution-Aware):**
  - `SimplePerlinHeightProvider.cs`: Multi-octave Perlin noise. Main development provider.
  - `3DPerlinHeightProvider.cs`: 3D Multi-octave Perlin noise. Planned upgrade to eliminate noise distortion.
  - `MountainRangeHeightProvider.cs`: Procedural continental + ridge-based terrain (abandoned)
  - `OctaveMaskHeightProvider.cs`: Wrapper for octave-limited sampling (abandoned)

## Terrain Consistency & Progressive Detail
**CRITICAL:** The system ensures seamless terrain consistency across all depth levels:
- **Global Coordinate System:** All tiles use consistent global normalized coordinates that map the same world positions to identical height samples regardless of tile depth.
- **Height Consistency:** Height providers must return IDENTICAL height values for the same world position (normalized direction) regardless of the mesh `resolution` parameter. This is enforced by the heightmap generator/sampler and is critical for seam-free LOD transitions.
- **Progressive Mesh Detail:** Higher depth tiles get exponentially more mesh resolution to show geometric detail of the SAME underlying terrain
- **Seamless Transitions:** No terrain popping or discontinuities when changing depth levels - only mesh density changes. No animations for now.

### Height Provider Requirements (CRITICAL)
- The `resolution` parameter must NOT affect the actual height values returned.
- Height providers must return identical height values for the same normalized world direction regardless of the mesh `resolution` parameter. This enforces topology determinism across LODs.
- Resolution is only used for mesh density and vertex placement; it must not change sampling semantics.
- Same world position = same height value, always.

## Important runtime behaviors (current implementation)
- Precomputed tile normals and canonical bary centers: `PlanetTileVisibilityManager` and `TerrainTileRegistry` maintain precomputed tile centers, corner directions, and normals for all depths to avoid recomputation. Use `IcosphereMapping.TileIndexToBaryCenter` and `IcosphereMapping.TileIndexToBaryOrigin` as the single source of truth for tile centers.
- Tile counts and reuse: The number of `PlanetTerrainTile` instances for a given depth must equal the number of tiles at that depth (20 * 4^depth). Tiles are created on demand and reused via lifecycle management.
- Tile caching: `TileCache` manages the lifecycle of tile GameObjects (creation, pooling, activation/deactivation). Mesh instance caching lives in `PlanetTileMeshBuilder` and may be cleared via `PlanetTileMeshBuilder.ClearCache()` (useful for editor tooling and tests).
- Tile lifecycle: `ManageTileLifecycle(HashSet<TileId> hitTiles, int depth)` handles spawning/refreshing hit tiles and deactivating tiles not hit this pass.

Developer guidance and common pitfalls
- SOLID principles apply.
- YAGNI: avoid overengineering. Implement only what is necessary.
- Keep files focused and prefer splitting files that grow beyond ~500 lines.
- Prefer small, single-responsibility classes over monoliths.
- Height providers: never modify topology based on mesh resolution. Write deterministic sampling code: same normalized direction → same height.
- Barycentric vs Tile index confusion: prefer the `Barycentric` ADT (`Assets/Scripts/TerrainSystem/LOD/Barycentric.cs`) across APIs to avoid mixing tile-local integer indices with normalized bary fractions. Use `IcosphereMapping.TileIndexToBaryOrigin` and `IcosphereMapping.BaryLocalToGlobal` for conversions. Historical bugs arose from mixing index vs bary expectations — be explicit and add tests when changing mapping code.
  - NOTE: As of recent changes and debugging, `IcosphereMapping.TileVertexBarys(int res)` yields tile-local integer indices encoded in the `Barycentric` ADT (i,j). Callers must convert these local indices into global normalized barycentric coordinates via `IcosphereMapping.BaryLocalToGlobal(tileId, localBary, res)` *or* `IcosphereMapping.BaryLocalToGlobalNoReflect(tileId, localBary, res)` before using them as UVs or passing to `BaryToWorldDirection`. This avoids semantic confusion and ensures canonical edge behavior.
  - Important: Treat `TileVertexBarys(int res)` as returning integer lattice coordinates packed in `Barycentric` (NOT normalized UVs). If you need normalized barycentric UVs for mesh UVs or for calling `BaryToWorldDirection`, always call:
    `IcosphereMapping.BaryLocalToGlobal(tileId, localBary, res)`
    or, when you want to prevent edge reflections caused by tiny lattice rounding overshoot, use:
    `IcosphereMapping.BaryLocalToGlobalNoReflect(tileId, localBary, res)`

Reflection insights (recent milestone):
- During debugging we discovered that certain tile-local lattice coordinates at lower depths (notably depth=2) can produce barycentric coordinates whose U+V slightly exceed 1.0 due to lattice arithmetic. The `Barycentric` constructor historically reflects these points across the U+V=1 diagonal when W becomes meaningfully negative. That reflection maps a point into the adjacent triangle and changes the canonical face direction — producing subtle, hard-to-find seams.
- To aid future maintainers, a mapping table documenting the observed prereflect vs postreflect bary centers for depth=2 has been recorded in `Docs/Brainstorm/barycentric-mapping.txt`. Use that table when debugging edge-case tiles; it documents which tile indices needed manual reflection corrections during troubleshooting.
- Recommended policy: prefer `BaryLocalToGlobalNoReflect` for mesh-building and UV storage to clamp tiny numeric overshoot to tile edges (renormalize) rather than reflect into the adjacent triangle. Reserve the original reflect behavior only for callers that intentionally want reflection semantics.

Quick checklist when touching barycentric code:
- Always be explicit about whether you are operating on tile-local lattice indices or normalized bary fractions. Use the `Barycentric` ADT constructor only for normalized bary fractions; treat values returned by `TileVertexBarys` as lattice indices.
- When converting local -> global barys for mesh UVs or sampling, prefer `BaryLocalToGlobalNoReflect` to avoid accidental reflections. Add unit tests that assert `IsReflected == false` for canonical tile corners.
- If you observe a visible seam at a tile corner, consult `Docs/Brainstorm/barycentric-mapping.txt` first to see if that tile is a known reflection case.

This document is the source of truth for the system architecture. Keep it updated with any changes to aid future development and maintenance. Do not change this document to match existing code. Only change this document to reflect intentional design changes made during the session.

Stay focused on the human engineer's goals. Ask questions if needed. Proceed only with consensus. Don't be afraid to tell the human engineer they are incorrect about their intuition. It's always the same guy this is a one man project. The human engineer will be dilligently resisting complexity creep. Do not allow them to veto necessary complexity. Always explain things in first principles before explaining the math and implementation.

The exchanges should be short and focused on one action item at a time.
Code edits must be made in a test driven manner. Add or update unit tests where appropriate (see `Assets/Tests/` folders). Editor-only workarounds are discouraged; unit or playmode tests should expose the issue before adding non-testable hacks.
When changing mapping or barycentric math, include small tests asserting that `IcosphereMapping.TileIndexToBaryCenter` <-> `WorldDirectionToTileIndex` are consistent for canonical centers.