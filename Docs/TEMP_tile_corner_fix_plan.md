# TEMP: Tile Corner Fix Plan (test-driven)

Goal
- Deterministically detect and fix incorrect tile corner vertices that produce a visible gap at depth 2 and above.
- Do the minimal change: O(3) checks of the 3 corner extremities per tile at build-time, and correct any wrong vertex positions/triangle indices so the mesh matches canonical mapping.

Tiny contract (what success looks like)
- Input: a built tile mesh for a given `TileId` and `resolution`/`res` used to generate the mesh.
- Output: mesh remains unchanged if corners are consistent; otherwise exactly the incorrect corner vertices (<=3) are replaced with canonical positions and mesh normals/bounds are recalculated.
- Error modes: mapping lookup failure or NaN positions should log and bail out safely (do not throw), but tests must fail if positions are NaN.
- Acceptance: PlayMode test for `depth=2` passes and visual gap is no longer present (automated assertion uses epsilon).

Quick approach summary
1. Reproduce the bug manually at depth=2 and note the `TileId`/face/indices (manual capture step). Marked as the current in-progress todo.
2. Add a small PlayMode test that constructs a tile at depth=2 and checks corner consistency programmatically (initially this test should fail).
3. Implement a tiny O(3) helper to verify and correct the 3 corner vertices of a mesh during/before/after mesh construction (non-allocating, low-risk).
4. Re-run the PlayMode test to confirm the fix; add a unit test for mapping center<->tile index if missing.
5. Cleanup temporary plan and helpers.

Detailed implementation notes (how to check & fix)
- Use the project's canonical mapping helpers rather than ad-hoc math: `IcosphereMapping.TileIndexToBaryCenter` or `IcosphereMapping.BaryLocalToGlobal` and then `BaryToWorldDirection` to compute the expected world direction for each corner.
- For a tile with `res` vertices per edge, the three canonical corner local indices are:
  - A: (0, 0)
  - B: (res-1, 0)
  - C: (0, res-1)
  (These correspond to tile-local lattice indices from `IcosphereMapping.TileVertexBarys(res)` semantics.)
- Convert local lattice indices to global barycentric using `IcosphereMapping.BaryLocalToGlobal(tileId, localBary, res)` (or `TileIndexToGlobal` helper) to obtain normalized barycentrics, then call `IcosphereMapping.BaryToWorldDirection(face, bary)` to get direction vector.
- Multiply by planet radius + height (from the height provider used during mesh build) to get expected vertex world position. If mesh stores vertices in local space relative to planet/parent, compare in same space (apply inverse transforms if necessary).

Edge checks
- Use a small epsilon (e.g., 1e-4 to 1e-3 world units depending on planet scale) to detect mismatch. If |meshVertex - expected| > epsilon, consider corner incorrect.
- For each incorrect corner, replace the mesh vertex position with the expected position.
- Update any triangle indices that referenced an old vertex index only if you changed the vertex's index (in-place replacement is preferred to avoid re-indexing). If you must split shared vertices, be conservative — prefer duplicating the corrected vertex and updating the triangles that reference the corner to reference the new index.

Mesh update sequence (Unity-safe)
1. mesh.vertices = updatedVertices; // modify only the necessary entries
2. mesh.RecalculateNormals();
3. mesh.RecalculateBounds();
4. mesh.UploadMeshData(false); // keep readable for tests or set true if you want to free memory later

Testing plan
- PlayMode test (Assets/Tests/PlayMode/TileCornerFixTests.cs)
  - Create a test that uses `PlanetTileMeshBuilder.BuildMesh(tileId, res)` or the same builder entry the runtime uses.
  - Await one frame if the builder uses coroutines/asynchronous work.
  - Inspect mesh vertex positions for the 3 corners using mapping helpers and assert max corner error <= epsilon.
  - Optionally snapshot the mesh bounds and confirm no NaNs.
- Quick unit test for mapping
  - Verify `IcosphereMapping.TileIndexToBaryCenter(tileId)` -> `BaryToWorldDirection` -> `WorldDirectionToTileIndex` round-trip for canonical centers at multiple depths (including depth=2).

Edge cases and gotchas
- Shared vertex maps: if builder deduplicates boundary vertices between tiles, fixing a vertex that is shared may be visible across adjacent tiles. Decide whether to fix by updating the shared vertex pool (preferred) or by duplicating the corner vertex for that tile. For a safe minimal fix, duplicate the corner vertex in the tile's mesh and update that triangle's index to point to the duplicate. Document this behavior and schedule follow-up if needed.
- Floating precision: at higher depths the vertex differences may be very small; choose epsilon relative to planet scale.
- Height providers: ensure the height provider used to compute expected positions is the same as the one used during mesh build (use dependency injection or the same settings object).

Minimal code locations to touch (low-risk)
- `PlanetTileMeshBuilder.cs` — add an internal helper `VerifyAndFixCorners(Mesh mesh, TileId tileId, int res, float epsilon)` and call it just after mesh construction but before the mesh is assigned to the `MeshFilter`/`MeshCollider`.
- `Assets/Tests/PlayMode/TileCornerFixTests.cs` — new PlayMode test that initially asserts the buggy behavior, then after fix asserts success.

Rollout and cleanup
- Keep the corner-fix helper behind a #if UNITY_EDITOR || DEBUG flag or a configuration toggle so it can be disabled in production if desired.
- Once tests pass and the visual gap is resolved, remove or harden any temporary debug logs and convert the temporary plan file into a permanent task in your issue tracker (or delete).

Next steps (manual steps for you)
1. Run the editor and reproduce the visible gap at depth=2; capture the `TileId` and screenshot.
2. I'll implement the small helper and tests after you confirm the captured `TileId` (or I can proceed with inferred ids if you prefer).


-- End of TEMP plan
