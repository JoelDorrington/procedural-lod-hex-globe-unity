# HexGlobeProject
### Important note for AI! I'm a coder, I want to use the unity editor only for parameterised tuning and play testing. Game objects should be self constructing in terms of internal dependencies.

## Overview
HexGlobeProject is a Unity-based project that implements a hexagonal and pentagonal map on a globe. The project focuses on creating a low-level and performant graphical data structure to represent the map, allowing for efficient rendering and interaction.

## Features
- Spherical grid with hexagonal and pentagonal cell representation.
- Exactly 12 pentagonal cells, positioned in the negative space left by the hexagons, as required by spherical geometry.
- Efficient grid management with the `GlobeGrid` class.
- Dynamic mesh generation for visual representation using the `MeshGenerator` class.
- Utility functions for mathematical calculations in the `MathHelpers` class.

## Project Structure
```
HexGlobeProject
├── Assets
│   ├── Scripts
│   │   ├── HexMap
# HexGlobeProject

## Quick developer note
This README was updated to reflect recent implementation and testing changes. After pulling changes, reimport scripts in the Unity Editor so new inspector fields (for example the `hideOceanRenderer` checkbox on `TerrainRoot`) appear.

## Overview
HexGlobeProject is a Unity project that implements procedural terrain on isophere tiles, LOD-driven tile streaming, and supporting utilities for mesh generation and sampling.

## Project layout (important parts)
```
Assets/
	Scripts/        # runtime code: terrain, LOD, mapping
	Tests/          # Editor and unit tests
	Shaders/        # terrain shaders
	Prefabs/        # sample prefabs
README.md
```

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

TODO: investigate wiremesh index density and consider GPU-driven or lower-density solutions for the grid overlay.