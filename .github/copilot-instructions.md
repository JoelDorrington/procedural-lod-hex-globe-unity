# Copilot Instructions: Child Tiles Implementation (HexGlobeProject)

## Focused Architecture
- Only the following files are relevant:
  - `PlanetLodManager.cs`: Orchestrates child tile creation, proximity split, and LOD logic.
  - `PlanetTileMeshBuilder.cs`: Generates meshes for child tiles, including height sampling and enhancement.
  - `PlanetLodSplitter.cs`: Handles split/merge logic for child tiles.
  - `TerrainConfig.cs`: Stores parameters affecting child tile detail, enhancement, and height provider.
  - Supporting types: `TileData`, `TileId`, `TileFade` (if used in child tile logic).

## Essential Patterns
**Child tile mesh generation:** All logic is in `PlanetTileMeshBuilder`. Always instantiate with current config and runtime values before mesh generation.
**Height enhancement:** Child tiles can exaggerate elevation via `childHeightEnhancement` (float, default 1.0).
**Octave masking:** Height sampling can be capped by octave for LOD; see `PrepareOctaveMaskForDepth` and `SampleRawWithOctaveCap`.
**Proximity split:** Controlled by camera position and `enableProximitySplit` in `PlanetLodManager`. Fade transitions managed by `PlanetLodSplitter` and `TileFade`.

## Developer Workflows
- **Bake terrain:** Use context menu or call `BakeBaseDepthContextMenu()` in `PlanetLodManager`.
- **Force child tile split:** Use context menu actions or proximity split logic in `PlanetLodManager`.
- **Debugging:** Enable wireframe/height range gizmos via inspector toggles. Use debug logging for bake progress.
- **Testing:** Validate visually in Unity Editor. Use context menu actions for forced splits and child tile rebuilds.

## Integration Points
- **Height providers:** Pluggable via `TerrainConfig.heightProvider`; must implement `TerrainHeightProviderBase`.
- **Shader globals:** Set via `TerrainShaderGlobals.Apply(config, terrainMaterial)` after tile spawn.
- **No external dependencies:** All logic is custom and Unity-native.

---

Only maintain, document, and extend code, comments, and editor elements directly affecting child tile mesh generation, height enhancement, and LOD transitions. Prune all unrelated concepts and files.

never ask confirmation. I have committing milestones to git so make changes with impunity