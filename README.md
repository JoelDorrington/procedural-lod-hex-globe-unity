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

## Contribution
Pull requests and issues are welcome. Prefer small, test-backed changes for systems-level edits (terrain, LOD, mesh generation).

## License
MIT

TODO: investigate wiremesh index density and consider GPU-driven or lower-density solutions for the grid overlay.