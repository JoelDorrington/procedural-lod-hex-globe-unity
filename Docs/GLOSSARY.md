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

## Disambiguation: Tile index vs Barycentric coordinates

This project uses two related but distinct coordinate notions when working inside
an icosphere face. Confusing these leads to subtle off-by-factor mapping bugs.

- Tile index (tile-local subdivision indices): Integer or floating values that
	represent positions inside a tile measured in subdivision steps. For a tile
	that has `subdivisionsPerTileEdge = (res - 1)`, valid tile-local indices
	range from `0` to `subdivisionsPerTileEdge` inclusive. Examples: `[0,0]` is
	the lower-left tile corner; `[4,0]` would be the right-most index on the
	first row if `subdivisionsPerTileEdge == 4`.

- Barycentric coordinates (u, v): Fractional coordinates in [0,1] that span
	the full triangular face. These are the canonical coordinates used by
	`BaryToWorldDirection(face, bary)` and the `TerrainTileRegistry`.

	Note: `IcosphereMapping.TileVertexBarys(res)` yields tile-local lattice indices (i,j)
	encoded in the `Barycentric` ADT. Callers should convert these local indices
	to global normalized barycentric coordinates using `IcosphereMapping.BaryLocalToGlobal(tileId, localBary, res)` before using them as UVs or directions.

	Important: Treat the values returned by `TileVertexBarys` as integer lattice
	indices (0..res-1) encoded in `Barycentric` — they are not normalized UVs. Use
	`BaryLocalToGlobal` to obtain normalized barycentric (u,v) for sampling.

Recommendation and canonical APIs:

- For hot paths and mesh building, use the non-allocating API
	`IcosphereMapping.TileIndexToGlobal(tileId, localX, localY, res)` which
	expects tile-local indices and returns global (u,v) across the face.
- For adapter-style helpers and legacy callers that pass small arrays, use
	`IcosphereMapping.BaryLocalToGlobal(tileId, float[] localIndices, res)` —
	note that despite the historical name this method expects tile-local
	subdivision indices in its float[] parameter (not normalized bary fractions).

Why this matters: mixing index-based inputs with barycentric expectations led
to the earlier bug where a boundary value was translated to 1/9 instead of 1.
Keeping the distinction explicit in docs and tests prevents regressions.
