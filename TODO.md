# TODO (Next Session)

## High Priority
- Terrain material parameter polish: tune shallow water & mountain/snow thresholds; expose push-only sea level button (editor) without auto sync.
- Add editor button in `TerrainConfigEditor` to "Push Shader Globals" (calls `TerrainRoot.PushShaderGlobals()` or similar static helper).
- Camera improvements: optional smoothing for WASD spherical movement; clamp roll range; add zoom inertia toggle.
- Performance pass: cache height samples for mountain provider (currently multiple Perlin evaluations); consider Burst/jobifying later.
- Fix visual seams between cube faces (normal smoothing across edges if visible).

## Medium Priority
- Snow refinement: add separate specular/roughness for snow (simple metallic/smoothness material properties or second pass).
- Add optional texture or gradient ramp (1D texture) for biome coloration instead of hard-coded lerps.
- Implement shoreline foam mask (based on height & slope) with subtle animated panning.
- Expose percentile-based auto sea level estimator (sample random directions; compute sea suggestion) as an editor utility (not automatic overwrite).
- Script to regenerate terrain automatically when config height provider values change in edit mode (with debounce).

## Low Priority / Stretch
- Reintroduce simplified dynamic LOD (quadtree) in a branch; design minimal data required.
- Mesh pooling (if LOD reintroduced) to reduce GC allocations.
- Procedural clouds layer (simple sphere with noise scrolling in shader).
- Atmosphere scattering placeholder (single-pass approximated gradient).
- Add unit tests for math helpers (CubeSphere.FaceLocalToUnit correctness, height provider ranges).
- Export planet mesh (combined) as asset for baking lightmaps.

## Tech Debt / Cleanup
- Remove unused legacy comments referencing morphing/skirt systems.
- Consolidate repeated height formula code; centralize sampling utilities.
- Add XML summaries to all public fields for clarity.
- Namespace consistency: ensure all new scripts reside under `HexGlobeProject` (non-terrain ones currently root-level like `CameraController`).

## Possible Research Items
- Evaluate using compute shader for height evaluation + normal recalculation.
- Consider dual-mesh data (hex grid) overlay integration with terrain height for gameplay.
- Memory footprint measurement (vertex counts per face) and possible compression.

## Quick Wins Tomorrow
1. Add Push Shader Globals button.
2. Add roll clamp and smoothing toggle in `CameraController`.
3. Implement shoreline foam (simple additive band in shader).
4. Add snow specular tweak (whiter and slightly higher smoothness).
5. Clean old comments & commit.

> Feel free to reorder based on fresh priorities tomorrow.
