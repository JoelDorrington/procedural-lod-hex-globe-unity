
# Copilot Instructions: Planet Tile Streaming System (HexGlobeProject)

## Important Workflow Instructions
NEVER WASTE TOKENS ON ASKING FOR PERMISSION. MOVE FAST AND BREAK THINGS.
I have milestones committed to git so don't worry.

## System Overview
This project implements a Google Maps-style tile explorer for procedural planet terrain with seamless LOD transitions and fractal detail zoom. The system uses camera-driven raycast heuristics to spawn tiles at appropriate detail levels.

## Core Architecture
- **Primary Components:**
  - `PlanetTileExplorerCam.cs`: Main camera-driven tile streaming controller with raycast heuristics
  - `PlanetTileMeshBuilder.cs`: Procedural mesh generation with resolution-aware height sampling
  - `PlanetTileSpawner.cs`: GameObject creation and management for tiles
  - `CameraController.cs`: Orbit camera with zoom-based depth control
  - `TerrainConfig.cs`: Configuration for terrain generation parameters

- **Data Types:**
  - `TileId`: Unique identifier for cube-sphere tiles (face, depth, x, y coordinates)
  - `TileData`: Mesh data, resolution, height bounds, and spatial metadata
  - `TileFade` & `TileFadeAnimator`: Animation system for smooth tile transitions

- **Height Providers (Resolution-Aware):**
  - `MountainRangeHeightProvider.cs`: Procedural continental + ridge-based terrain
  - `SimplePerlinHeightProvider.cs`: Multi-octave Perlin noise
  - `OctaveMaskHeightProvider.cs`: Wrapper for octave-limited sampling

## Terrain Consistency & Progressive Detail
**CRITICAL:** The system ensures seamless terrain consistency across all depth levels:
- **Global Coordinate System:** All tiles use consistent global normalized coordinates that map the same world positions to identical height samples regardless of tile depth
- **Height Consistency:** Height providers must return IDENTICAL height values for the same world position regardless of the resolution parameter
- **Progressive Mesh Detail:** Higher depth tiles get exponentially more mesh resolution to show geometric detail of the SAME underlying terrain
- **Seamless Transitions:** No terrain popping or discontinuities when changing depth levels - only mesh density changes

### Height Provider Requirements (CRITICAL)
- The `resolution` parameter should **NOT** affect the actual height values returned
- Height providers must return consistent terrain topology regardless of resolution
- Resolution is only used internally for mesh density calculations, not for terrain generation
- Same world position = same height value, always

## Key Implementation Details

### Tile Coordinate Calculation (CRITICAL)
```csharp
// CORRECT: Global coordinates ensure consistency across depths
float globalU = (float)(data.id.x * res + i) / (float)((1 << data.id.depth) * res);
float globalV = (float)(data.id.y * res + j) / (float)((1 << data.id.depth) * res);

// INCORRECT: Would cause inconsistent terrain between depths
float u = (i * inv + data.id.x) / (1 << data.id.depth); // DON'T USE
```

### Tile Stitching System
- **Purpose:** Eliminates visible gaps between adjacent tiles at higher depths
- **Implementation:** Generates skirt geometry around tile perimeters with small overlap
- **Activation:** Automatically enabled for tiles with depth > 0 when `enableTileStitching` is true
- **Method:** Creates additional vertices slightly outside tile boundaries and connects them with triangles
- **Consistency:** Uses same height sampling as main tile vertices to ensure seamless integration

### Resolution Scaling Strategy
- Base resolution from `TerrainConfig.baseResolution` (typically 32)
- Scale down by depth to maintain world-space density: `baseRes >> depth`  
- Add progressive detail for fractal zoom: `+ depth * 16`
- Minimum resolution clamped to 16 for stability

### Height Provider Integration
- All providers must implement `Sample(Vector3 unitDirection, int resolution)`
- **CRITICAL:** Resolution parameter must NOT change height values - only used for internal mesh calculations
- Height values must be consistent across all resolution levels for the same world position
- Providers should ignore resolution for terrain generation and use fixed parameters

## Raycast Heuristic System
- **Frequency:** ~30Hz coroutine with non-stacking execution
- **Method:** Cast rays in viewport grid pattern to ocean sphere
- **Mapping:** Convert hit points to cube face coordinates and tile IDs
- **Lifecycle:** Track spawn timestamps to prevent immediate culling
- **Cleanup:** Remove tiles not hit in current pass or with mismatched depth

## Camera Integration
- `CameraController.ProportionalDistance`: 0 (closest) to 1 (farthest)
- Depth calculation: `Mathf.RoundToInt(Mathf.Lerp(0, maxDepth, 1-ProportionalDistance))`
- Smooth zoom with logarithmic scaling for intuitive control
- Automatic depth transitions trigger tile cleanup and respawn

## Deprecation & Migration (IMPORTANT)
`PlanetLodManager` is **DEPRECATED**. Use modern camera-driven approach:

**Migration Steps:**
1. Attach `PlanetTileExplorerCam` to camera GameObject
2. Assign `TerrainConfig` and `Material` via inspector
3. Optionally provide `PlanetTileMeshBuilder` override
4. Set `planetTransform` for sphere center reference
5. Remove references to `PlanetLodManager.TileObjects` and similar

**Legacy Baking:** `BakeBaseDepthContextMenu()` remains for offline preprocessing but is discouraged for runtime use.

## Developer Workflows

### Testing Terrain Consistency
- Zoom in/out and verify no terrain popping or discontinuities  
- Check that features (mountains, coastlines) remain coherent across all depth levels
- Enable wireframe mode to verify mesh density increases with depth
- Test at tile boundaries to ensure seamless stitching
- Verify tile stitching eliminates gaps at higher depths without affecting terrain topology

### Debugging Tools
- Depth change logging: Track when camera zoom triggers new tile depths
- Tile spawn/destroy logging: Monitor tile lifecycle during camera movement
- Enable gizmos for tile bounds visualization
- Use wireframe rendering to visualize mesh detail progression

### Performance Optimization
- Monitor tile object count in hierarchy during camera movement
- Check raycast heuristic execution frequency (should be ~30Hz)
- Verify tiles outside view are properly cleaned up
- Profile height provider sampling performance at high resolutions

### Height Provider Development
- Always implement resolution-aware sampling but **NEVER** use resolution to change height values
- Height providers must return identical values for the same world position regardless of resolution
- Resolution parameter is for internal mesh calculations only, not terrain generation
- Test consistency: same world position should give same height at all resolutions, always

## Integration Points
- **Height providers:** Via `TerrainConfig.heightProvider` - must implement `TerrainHeightProviderBase`
- **Materials:** Applied via `PlanetTileSpawner.SpawnOrUpdateTileGO()`
- **Shader globals:** Set via `TerrainShaderGlobals.Apply(config, terrainMaterial)`
- **Fade animations:** Use `TileFade` and `TileFadeAnimator` for smooth transitions
- **No external dependencies:** Pure Unity + C# implementation

## Common Pitfalls to Avoid
1. **Coordinate Inconsistency:** Never use tile-relative coordinates for height sampling
2. **Resolution Affecting Heights:** Height providers must NOT change height values based on resolution parameter  
3. **Manager Dependencies:** Avoid coupling new code to deprecated `PlanetLodManager`
4. **Blocking Operations:** Keep mesh generation and height sampling performant
5. **Memory Leaks:** Ensure proper tile cleanup when camera moves or depth changes

## Overlay & Debug Visuals (Important)
- The project includes a separate spherical hex wireframe overlay used as a game-board visualization (hex grid) and as a debug aid. This overlay is rendered on top of the terrain and may look like gaps or mesh artifacts when enabled.
- Important: the hex wireframe is NOT produced by the tile mesh generation or underwater culling — it is an intentional overlay that sits above the planet surface and will be modeled/meshed later as a gameplay layer.
- When debugging tile stitching or mesh gaps, disable the hex wireframe overlay (or switch to a solid-shaded view) to ensure you are seeing the raw generated meshes. Confusing the overlay with missing geometry is a common source of false positives.

### Debugging stitching vs overlay
- If you see thin seams or regular wire patterns (hex grid) on the planet surface:
  - First, toggle the hex wireframe overlay off and re-check the terrain.
  - If seams persist, enable the mesh-builder stitching debug logs (see `PlanetTileMeshBuilder.cs`) and verify stitching vertex/triangle counts in the Unity console.
  - If seams disappear with the overlay off, the issue is visual-only — either adjust overlay render order or temporarily hide it during development.


---

Focus on maintaining terrain consistency, optimizing camera-driven streaming performance, and ensuring seamless fractal detail progression. All changes should preserve the coherent, continuous planet surface experience.

Never ask confirmation. Make changes with impunity - git milestones provide safety.