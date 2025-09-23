Spike: Making planet radius configurable via playtest JSON

Goal
- Allow designers to set `planetRadius` in `Assets/Configs/playtest_scene_config.json` via the `PlaytestJsonEditorWindow` and have `SceneBootstrapper` apply that radius when creating the Planet and related systems at runtime.

Findings
- `PlaytestSceneConfig` (ScriptableObject) is the canonical in-editor representation used by `PlaytestJsonEditorWindow`.
- `playtest_scene_config.json` currently contains a basic set of keys (starfields, sun, spawnPlanet) but no `planetRadius` key.
- `PlaytestJsonEditorWindow` loads the JSON into a temporary `PlaytestSceneConfig` ScriptableObject and exposes fields via `SerializedObject`. This makes it easy to add `planetRadius` to the class and the window will display it automatically.
- `SceneBootstrapper.RootConfig` reads the JSON file directly at runtime and maps it into a simple runtime-friendly `RootConfig` type. The code already follows the pattern of mapping `PlaytestSceneConfigFlat` -> `RootConfig` and then uses `RootConfig` for camera/starfield/sun setup.
- Current bootstrapper contains many `#if UNITY_EDITOR` branches relying on `UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainConfig>(...)` to find `TerrainConfig` and `Material` assets. This breaks runtime/containerized usage and prevents the bootstrapper from being production-ready.
- Planet visuals and tile LOD code use `TerrainConfig.baseRadius` as authoritative radius for tile generation. `Planet.GeneratePlanet()` prefers `TerrainRoot.config.baseRadius` if a `TerrainRoot` exists; otherwise it uses the internal `sphereRadius` (default 30f). `PlanetTileVisibilityManager` reads `config.baseRadius` for its radius when present.

Recommended plan
1. Add a `public float planetRadius = 30f;` field to `PlaytestSceneConfig` so the editor window and JSON both support it.
2. Add `"planetRadius": 30.0` to `playtest_scene_config.json` (and keep the other existing keys unchanged).
3. Remove editor-only AssetDatabase calls in `SceneBootstrapper.CreatePlanetUnderCameraTarget` and instead rely on the already-parsed `RootConfig` to configure runtime systems.
   - Create a runtime `TerrainConfig` ScriptableObject instance via `ScriptableObject.CreateInstance<TerrainConfig>()`, set `baseRadius = root.planetRadius` and assign it to the `PlanetTileVisibilityManager.config` field.
   - Set `Planet.sphereRadius` to `root.planetRadius * visualScale` (1.01f as used in `GeneratePlanet`) via direct property or reflection if necessary.
   - Set `SunFlareOccluder.planetRadius` directly from `root.planetRadius`.
4. Clean up bootstrapper helper code to remove `#if UNITY_EDITOR` guarded AssetDatabase calls used only for playtest convenience.
5. Keep `PlaytestJsonEditorWindow` behavior (Save as ScriptableObject) intact.

Edge cases / Notes
- If `spawnPlanet` is false, bootstrapper should not create Planet nor assign radius.
- When creating a runtime `TerrainConfig` instance, it is not saved as an asset; it's used purely at runtime and will not persist to disk.
- Some editor-only code that relied on AssetDatabase (finding `Material` assets) can be optionally preserved under `#if UNITY_EDITOR` for editor convenience, but the bootstrapper should work without them in runtime builds.
- Tests referencing AssetDatabase may need adjustment or should remain in editor tests.

Next steps (after confirmation):
- Implement the changes: modify `PlaytestSceneConfig`, update JSON, update `PlaytestJsonEditorWindow` fields, and refactor `SceneBootstrapper` to use runtime config and remove AssetDatabase usage where possible.
- Run compile checks and basic playtest bootstrap (developer to run Unity) to validate behavior.

