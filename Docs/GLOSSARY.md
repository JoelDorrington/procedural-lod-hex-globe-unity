# Project Glossary

Note: Only include terms here that carry multiple meanings in the project context or require explicit disambiguation. Prefer short, focused entries — don't replicate full design docs.

This glossary lists key terms used in the HexGlobeProject to help contributors and tooling (including Copilot) stay consistent.

- icosphere: A tessellated sphere derived from an icosahedron. Used for planet tiling in this project.
- face: One of the 20 triangular faces of the base icosahedron.
- tile: A subdivided triangular patch on an icosphere face at a given depth.
- depth: Subdivision level of an icosphere face; each increment subdivides each triangular tile into 4 smaller tiles.
- resolution: Number of mesh vertices along a tile edge when building a `PlanetTerrainTile` mesh.
- TileId: Lightweight struct identifying a tile by face, x, y, depth.
- Global coordinate: The normalized direction vector from the planetary center; used for consistent height sampling.
- Barycentric Coordinate: (u, v, implicit w) coordinates used to enumerate positions inside a triangular face.
- Bary: Abbreviation for "Barycentric" or "Barycentric Coordinate"
- Canonical center: The agreed-upon bary center used across registry, mapping, and builder (ensures deterministic face selection).
- Registry (TerrainTileRegistry): Precomputed metadata for tiles at a depth (centers, corner directions, etc.).
- Seam/shared vertex map: Builder-level cache used to ensure vertices on tile boundaries are shared and identical.
- Height provider: A component exposing a sampling function that returns terrain height for a normalized direction.


For more details, see the `Assets/Scripts/TerrainSystem/LOD` folder and the `Docs/` directory.
