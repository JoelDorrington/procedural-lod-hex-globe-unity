# HexGlobeProject
### Important note for AI! I'm a coder, I want to use the unity editor as little as possible.

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
│   │   │   ├── HexCell.cs
│   │   │   ├── GlobeGrid.cs
│   │   │   └── PentagonCell.cs
│   │   ├── Graphics
│   │   │   ├── MeshGenerator.cs
│   │   │   └── DataStructures
│   │   │       └── VertexData.cs
│   │   └── Utils
│   │       └── MathHelpers.cs
│   ├── Prefabs
│   │   └── Globe.prefab
│   ├── Materials
│   │   └── GlobeMaterial.mat
│   └── Scenes
│       └── MainScene.unity
├── ProjectSettings
│   └── [Unity project settings files]
└── README.md
```

## Setup Instructions
1. Clone the repository or download the project files.
2. Open the project in Unity.
3. Ensure all necessary packages are installed via the Package Manager.
4. Open the `MainScene.unity` file to view and interact with the globe.

## Usage Guidelines
- Use the `GlobeGrid` class to manage and access hexagonal and pentagonal cells.
- Modify the `MeshGenerator` class to customize the visual representation of the cells.
- Utilize the `MathHelpers` class for any mathematical operations needed in your scripts.

## Implementation Notes
- The grid is generated using a geodesic sphere algorithm, typically by subdividing an icosahedron, resulting in a closed surface with 12 pentagons and the rest hexagons.
- The 12 pentagons are always present, filling the negative space left by the hexagons, which is a geometric necessity for a seamless spherical grid.

## Initial Rendering

Our first goal is to render a blue sphere on the scene for display only. This blue sphere is independent of the cell grid, which will be integrated later for terrain generation.

### Steps to Render the Blue Sphere in Unity
1. Open the scene in Unity (e.g., MainScene.unity).
2. Create a new GameObject and add a Sphere Mesh (GameObject > 3D Object > Sphere).
3. Create a new Material in the Project window and set its color to blue.
4. Assign the blue material to the Sphere's MeshRenderer.
5. Position the sphere as desired in the scene. This sphere serves as a placeholder for the globe's visual representation.

## Contribution
Contributions are welcome! Please feel free to submit issues or pull requests for enhancements and bug fixes.

## License
This project is licensed under the MIT License. See the LICENSE file for more details.

## Requirements
The hex cell grid will be points-up relative to the globe, meaning the hexes will be wrapped flat-side to flat-side around the equator.

TODO the wiremesh has way too many triangles but the concept is proven. later I can find a more performant way to draw the grid