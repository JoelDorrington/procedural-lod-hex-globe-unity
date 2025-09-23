Runtime asset rules: Planet material, overlay cubemap, and shader

Purpose

This document records the minimal production rules and keys for the planet material, overlay cubemap, and shader so runtime behavior is identical in Editor and Player builds and safe when assets are missing.

Goals

- Ship a default material and shader that produce no magenta fallback in production.
- Provide both Addressables (preferred) and Resources fallback paths.
- Provide a small, deterministic fallback material creation path when assets are unavailable.
- Add shader-side guards so a missing cubemap cannot trigger NaN/Inf colours.

Addressables keys / Resources paths

Preferred (Addressables):
- Material: key = "Materials/Land" (Addressable entry for the `Assets/Materials/Land.mat` material)
- Cubemap: key = "DualOverlayCube_Playtest" (Addressable entry for generated/checked-in overlay cubemap)

Fallback (Resources):
- Material: `Assets/Resources/Materials/Land.mat` -> load with `Resources.Load<Material>("Materials/Land")`
- Cubemap: `Assets/Resources/DualOverlayCube_Playtest.cubemap` -> load with `Resources.Load<Cubemap>("DualOverlayCube_Playtest")`

Shader properties and keywords (contract)

- Property `_DualOverlayCube` (Cubemap): optional overlay cubemap.
- Property `_UseDualOverlay` (Float, 0 or 1): runtime toggle; when 0 the shader must not sample `_DualOverlayCube`.
- Keyword `DUAL_OVERLAY_ON`: optional shader keyword. When set, overlay path enabled; otherwise the shader should use a simplified path.

Shader defensive requirements

- Do not assume `_DualOverlayCube` is always present. Use `_UseDualOverlay` (float) or a keyword to select the sampling path.
- Clamp or saturate final colour before returning. Example end-of-fragment snippet:

  finalCol.rgb = saturate(finalCol.rgb);
  finalCol.a = 1.0;

- Guard any division/square-root/normalize operations that could receive zero/invalid inputs. Use `max(eps, denom)` for denominators and small eps like `1e-5`.

SceneBootstrapper runtime behavior

- Use Addressables when available; otherwise fall back to Resources.
- If both Addressables and Resources fail to provide the material, create a fallback `Material(Shader.Find("Standard"))` and set a neutral base colour. This prevents Unity's magenta missing-shader fallback.
- If overlay cubemap is missing, set `_UseDualOverlay` to 0 and disable the `DUAL_OVERLAY_ON` keyword on the material.
- Ensure `PlanetTileVisibilityManager.config` is assigned a runtime `TerrainConfig` before any terrain mesh generation happens. (If generation happens in Start/Awake, delay until config assignment completes or add an explicit init call.)

Build-time steps

- If using Addressables:
  1. Mark `Assets/Materials/Land.mat` and overlay cubemap as Addressable with keys above.
  2. Include required shader variants in a `ShaderVariantCollection` and add it to the build to avoid runtime shader variant stripping.

- If using Resources:
  1. Place default `Land.mat` under `Assets/Resources/Materials/Land.mat`.
  2. Place `DualOverlayCube_Playtest` under `Assets/Resources/`.

Testing checklist (quick local checks)

1. Editor Play:
   - Remove `Assets/Resources/Materials/Land.mat` temporarily, run Play; confirm fallback material is used and planet is visible (no magenta).
   - Add the Resources material back and ensure overlay appears (if cubemap present).

2. Standalone Player build (Windows):
   - Build with Addressables or Resources configuration. Run the player and confirm the planet renders and overlay behaves the same as in Editor Play.

3. Variant testing:
   - Toggle `_UseDualOverlay` via material inspector at runtime to ensure shader responds and does not crash when toggled off.

Notes and Rationale

- Using Addressables decouples editor-only asset references from the player build and supports remote/content-update scenarios. For small single-scene playtests, Resources is acceptable and simpler.
- Explicitly creating a neutral fallback material is preferable to allowing Unity to bind a magenta "shader not found" material which often hides the real cause of missing assets.
- Clamping final color in shader prevents NaN/Inf output that causes magenta rendering.

If you'd like, I can:
- Add the shader guards to `Assets/Shaders/PlanetTerrain.shader` now (small patch).
- Add an async Addressables version of `LoadPlanetMaterialAndCubemap` and wire it into coroutine flow.

Document created by the bootstrapper runtime changes.
