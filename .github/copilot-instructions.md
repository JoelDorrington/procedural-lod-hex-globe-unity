
# Copilot Instructions: Tile Fade Logic (HexGlobeProject)

## Focused Architecture
- Only the following files are relevant:
  - `PlanetLodManager.cs`: Orchestrates tile creation, proximity split, and LOD logic.
  - `PlanetTileMeshBuilder.cs`: Generates meshes for tiles, including height sampling and enhancement.
  - `PlanetLodSplitter.cs`: Handles split/merge logic and fade transitions for tiles.
  - `TerrainConfig.cs`: Stores parameters affecting tile detail, enhancement, and height provider.
  - Supporting types: `TileData`, `TileId`, `TileFade`, and the tile animator.

## Tile Fade Logic
- **Fade animations:** All fade in and fade out logic should be consolidated into the tile animator. The fade in animator currently works for child tiles; fade out is pending.
- **Atomic coroutines:** Fade animations must be implemented as atomic coroutines. Only one coroutine at a time should own and modify the shader/material of a tile.
- **Coroutine ownership:** Ensure that when a fade coroutine is running, no other coroutine can modify the tile's shader/material until the animation completes.
- **Transition management:** Use `TileFade` and animator logic to manage fade state and transitions. Avoid overlapping or conflicting fade operations.

## Developer Workflows
- **Test fade in/out:** Validate fade in and fade out visually in the Unity Editor. Use context menu actions or inspector toggles to trigger fades for child tiles.
- **Debugging:** Use debug logging to track coroutine ownership and fade progress. Enable wireframe/height range gizmos for visual debugging.
- **Bake terrain:** Use context menu or call `BakeBaseDepthContextMenu()` in `PlanetLodManager` for terrain baking.

## Integration Points
- **Height providers:** Pluggable via `TerrainConfig.heightProvider`; must implement `TerrainHeightProviderBase`.
- **Shader globals:** Set via `TerrainShaderGlobals.Apply(config, terrainMaterial)` after tile spawn and before fade animation.
- **No external dependencies:** All logic is custom and Unity-native.

---

Only maintain, document, and extend code, comments, and editor elements directly affecting tile fade logic, atomic coroutine ownership, and fade animation consolidation. Prune all unrelated concepts and files.

Never ask confirmation. I have committing milestones to git so make changes with impunity.