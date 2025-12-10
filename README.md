# Procedural 3D Planet Explorer - Spherical Hex-Grid Enabled

## Overview
Procedural 3D Planet Explorer takes user-provided seeds, generates 3D Perlin-based terrain across an isosphere, and optimizes rendering with positional LOD plus fractal detail emergence tuned to stable noise spaces.

## Camera controls (playtest scene)
- Move: `W/A/S/D`
- Rotate: `Q/E`
- Reset rotation: `R`
- Zoom: Scroll wheel; depth is logarithmically scaled so movement slows near the surface.

## Project layout (important parts)
```
Assets/
	Scripts/                # runtime code (terrain, LOD, mapping, bootstrapper, UI configs)
	Tests/                  # editor/unit tests
	Shaders/                # terrain and overlay shaders
	Materials/              # materials (e.g., Land.mat)
	Configs/                # TerrainConfig.asset, playtest_scene_config.json
	AddressableAssetsData/  # addressables settings/groups (versioned)
Docs/                     # public docs (glossary, barycentric mapping table)
README.md
```

## Unity setup
- Unity version: use the same major/minor as the project branch (e.g., 6000.x). Opening with an older editor will force an upgrade; avoid that for publishable branches.
- Packages are tracked via `Packages/manifest.json` and `packages-lock.json`. When you open the project, Unity will auto-install them. If Package Manager stalls, open **Window → Package Manager** and click **Install/Resolve** for any missing packages.
- If the editor reports script compilation errors on first import, let the Package Manager finish restoring; then **Assets → Reimport All** once.

## Scene bootstrapper (playtest setup)
- Entry point: `SceneBootstrapper` loads `PlaytestSceneConfig` (ScriptableObject) or its JSON twin (`Assets/Configs/playtest_scene_config.json`, addressable) to configure camera clear flags/background, starfields, sun flare, and optional planet spawn.
- Starfields: `universal*` and `galactic*` fields define radius/arc/rates; `burstCount` on each starfield freezes an initial burst (static stars). If `burstCount` is zero, runtime uses rate-over-time instead.
- Sun flare: `sunFlareName` defaults to `sunburst` (addressable/Resources). Brightness is scaled by `sunFlareBrightness` and can be toggled with `sunFlareEnabled`.
- Planet: `spawnPlanet` controls whether the planet + `PlanetTileVisibilityManager` are instantiated in space-only boots; `planetRadius` feeds placement and tile LOD math.

## Addressables basics
- Addressables package: should auto-install from `manifest.json` (`com.unity.addressables`). If missing, add via **Window → Package Manager → + → Add package by name... → com.unity.addressables**.
- Initial setup: open **Window → Asset Management → Addressables → Groups**, then **Build → New Build → Default Build Script** to generate the local addressables content.
- Play Mode script: in the Addressables Groups window, set **Play Mode Script** to **Use Existing Build (requires built groups)** for deterministic testing, or **Fast Mode** for rapid iteration.
- Content updates: when you add or change addressable assets, rebuild via **Build → New Build → Default Build Script** so playmode and builds pick up the changes.
- Cache cleanup: if you hit stale data, use **Build → Clean Build → All** in the Addressables window, then rebuild.

## Config files (keep these in source control)
- `Assets/Configs/TerrainConfig.asset`: canonical terrain settings. Create via `Create → HexGlobe → Terrain Config` if missing.
- Material linkage: the terrain material (e.g., `Assets/Materials/Land.mat`) should reference the `HexGlobe/PlanetTerrain` shader. Use the Terrain Tuning window (see below) to sync values into the material.
- Private docs and prototype notes live in `Docs/Private/` and are git-ignored. The public docs you can rely on are `Docs/GLOSSARY.md` and `.github/copilot-instructions.md`.

### Barycentric mapping caution
- The icosphere barycentric math is sensitive: mixing tile-local lattice indices with normalized bary coords can create seams. Always convert `TileVertexBarys` results via `IcosphereMapping.BaryLocalToGlobalNoReflect` before using them as UVs or sampling directions.
- A detailed prereflect/postreflect mapping table for tricky tiles lives in `Docs/barycentric-mapping.txt`. If you touch barycentric code, consult that table and prefer the non-reflect conversion helper to avoid edge flips.

### TerrainConfig schema (key fields)
- `baseRadius` (float): planet base radius.
- `baseResolution` (int): vertices per edge at depth 0; deeper LODs derive automatically.
- `heightProvider` (polymorphic): height generator implementation.
- `heightScale` (float): global multiplier on sampled heights.
- `seaLevel` (float): height offset; >0 raises oceans, <0 lowers.
- `shallowWaterBand` (float): band height above sea for shallow coloration.
- `debugElevationMultiplier` (float): multiplies final elevation for exaggeration.
- `icosphereSubdivisions` (int 0-6): helper sphere subdivision for overlays/tests.
- `recalcNormals` (bool): recompute normals from geometry (true shows slopes).
- Overlay: `overlayEnabled`, `overlayColor`, `overlayOpacity`, `overlayLineThickness`, `overlayEdgeExtrusion`.
- Tiered colors/heights (all floats unless color): `coastMax`, `lowlandsMax`, `highlandsMax`, `mountainsMax`, `snowcapsMax`; and matching colors `waterColor`, `coastColor`, `lowlandsColor`, `highlandsColor`, `mountainsColor`, `snowcapsColor`.

## Unity packages and Addressables setup
- Packages: Unity will auto-install packages from `Packages/manifest.json` on first open. If prompted, allow it to resolve/update. Addressables is already listed in the manifest.
- Addressables data: `Assets/AddressableAssetsData` is versioned. It contains the Default Local Group entries used by the scene bootstrapper: `PlanetTerrain.shader`, `Land.mat`, `TerrainConfig.asset`, `playtest_scene_config.json`, `TestOverlayCube_Large.cubemap`, and the star texture (`sunburst`).
- After cloning: open `Window → Asset Management → Addressables → Groups` to verify the Default Local Group entries, then run `Addressables → Build → New Build → Default Build Script` to regenerate local `ServerData/` (ignored in git). Rebuild Addressables if you change any of the grouped assets.
- If a grouped asset is missing in your clone (e.g., `TerrainConfig.asset`), create it at the same path so the addressable entry resolves, then rebuild Addressables.

## Scene assets that remain tracked
- Stars/planet shaders and supporting materials stay in repo (see `Assets/Shaders/`, `Assets/Materials/`).

## Running tests
Tests must only be run manually in the Unity test runer.

## Editor notes
- Reimport or restart Unity after pulling script changes so the new `hideOceanRenderer` checkbox is visible on `TerrainRoot`.
- Toggling `hideOceanRenderer` will leave the `Ocean` GameObject and its transform active but hide its `MeshRenderer`.

## Config windows — simple guide (for non-technical users)

There are a few small editor tools you can use to change how the planet looks without touching code. This guide explains the most common controls in plain language.

1) Open the Terrain Tuning window
- Menu: Tools → HexGlobe → Terrain Tuning...
- What it does: A simple control panel for colors and height bands (coast, lowlands, highlands, mountains, snow).

2) The TerrainConfig asset (the "source of truth")
- The window tries to find an asset called `TerrainConfig` (normally at `Assets/Configs/TerrainConfig.asset`).
- This asset stores the canonical colors and the height numbers used by the terrain shader. Think of it as the master settings file for terrain appearance.
- Buttons you will see:
	- Locate TerrainConfig Asset: find and select the asset in the project.
	- Ping TerrainConfig: highlights the asset in the Project window.
	- Save TerrainConfig: writes any changes you made back into the asset file so they are preserved.

3) The color and height fields (what they mean)
- Water Color: the base color used for ocean areas.
- For each terrain band (Coastline, Lowlands, Highlands, Mountains, Snowcaps) there are two things:
	- Max Height (above sea): a single number that defines where this band stops and the next band starts. These are absolute heights measured in the same units the project uses for planet size.
	- Color: the color used for that band.
- Planet Radius: how big the planet is. Usually you don't need to change this unless you know what you're doing.
- Sea Level: the base height of the sea. Values above this are treated as land.

4) Buttons that change materials or update the scene
- Apply TerrainConfig to Land.mat: writes the TerrainConfig values directly into the project's `Assets/Materials/Land.mat` material. This persists the look in the material file.
- Apply to Selected Material: if you select any Material in the Project window, this button writes the current values into that Material.
- Apply Shader Fields to Selected Material / to Land.mat: alternative UI for applying the currently shown shader fields to the selected material or to the default Land.mat.
- Update Live (Set Global Shader Vars): this does a quick, temporary update so you can preview changes immediately in the scene. It does not change asset files — it's great for fast tuning.

5) Typical workflows (step-by-step)
- Fast preview: open the window, change colors or height numbers, then click "Update Live (Set Global Shader Vars)". The Scene view will refresh so you can see immediate results.
- Make changes permanent: edit values, click "Save TerrainConfig", then click "Apply TerrainConfig to Land.mat" so the material asset stores those values. The window will try to update active tiles so what you see in the scene matches the saved settings.
- Edit a single material: select a Material in the Project window, change shader fields in the bottom panel, then click "Apply Shader Fields to Selected Material".

6) Why sometimes changes don't show immediately
- If you used "Update Live" you should see changes right away. If not:
	- Make sure the Scene view is visible (the window refreshes the Scene view after update).
	- If the TerrainConfig asset was edited, click "Save TerrainConfig" and then "Apply TerrainConfig to Land.mat" so the material and running tiles get the new values.
	- If the editor still shows old values, reimport scripts or restart Unity — this refreshes the editor UI and serialized fields.

7) How the changes reach the terrain tiles
- The tool writes values into the material asset (Land.mat) or into global shader variables used for preview. The runtime tile manager also has a small helper that updates already-spawned tiles when you apply changes to the main material.
- New terrain tiles created after a live update will read the global preview values so they match what you saw during tuning.

8) If you can't find the TerrainConfig asset
- The window will show a warning if no `TerrainConfig` asset exists. Create one in `Assets/Configs/` and name it `TerrainConfig.asset`, or place an existing `TerrainConfig` asset there so the window can find it.

9) Quick troubleshooting checklist
- No material selected for "Apply to Selected Material": select a material in the Project window first.
- No visual change after update: try "Update Live" then Scene → Repaint (or restart Unity if needed).
- Material not updating saved file: after applying to a material, be sure to Save Assets (the window normally calls SaveAssets but saving manually is safe).

If anything here is unclear or you'd like screenshots and a short video-style walkthrough for designers, tell me which part you want expanded and I'll add it.

## Contribution
Pull requests and issues are welcome. Prefer small, test-backed changes for systems-level edits (terrain, LOD, mesh generation).

## License
MIT
