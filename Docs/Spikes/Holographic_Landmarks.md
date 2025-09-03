# Spike: Holographic / Interferometric Heightmap Encoding

Date: 2025-09-02  
Author: GitHub Copilot

Summary
- Investigate encoding recognizable shapes / landmarks into procedural heightmaps by using interference / holography principles.
- Produce a deterministic, resolution-independent height contribution that blends with existing noise providers.
- Provide both runtime procedural options (few emitters / wavelets) and a bake option (FFT hologram textures per cube-face) for highest fidelity.

Goals
- Maintain the project's invariant: same world position -> identical height regardless of tile resolution.
- Avoid introducing seams at cube→sphere boundaries.
- Control spatial frequency to avoid aliasing at target mesh resolutions.
- Provide API hooks so existing height providers can blend holographic term with perlin/octave noise.

Approaches (pros/cons)
1. Procedural Wave-Sum (CPU / GPU)
   - Sum a small set (N=2..16) of complex plane or spherical waves:
     intensity(r) = |Σ_j A_j * exp(i*(k_j·r + φ_j)) / f(dist_j)|^2
   - Pros: parametric, runtime adjustable, compact memory
   - Cons: must band-limit (k) to avoid aliasing; CPU cost if many samples; better on GPU

2. Fresnel / Hologram Bake (FFT)
   - Bake a hologram texture per cube face (or 3D volume) via Fourier methods and sample at runtime.
   - Pros: highest fidelity, cheap runtime sampling (texture lookup), easier anti-alias control
   - Cons: bake cost, storage for textures, seam care (bake per face)

3. Distance‑field → Phase Encode
   - Create SDF of target silhouette, convert to phase map, forward propagate (Fresnel) to produce interference intensity.
   - Pros: can encode arbitrary silhouettes; good control over landmark shape
   - Cons: heavier bake pipeline

Design constraints (must-haves)
- Deterministic sampling by unitDirection (world-space) — no dependence on tile resolution.
- Band-limited output: limit max spatial frequency to Nyquist for highest resolution we expect.
- Seam strategy: prefer per-cube-face baking or continuous 3D texture sampling in world-space.
- Blend control (mask + amplitude + scale) so holographic features can be localized.

API / Integration plan
- Height provider extension interface:
  - Add IHeightModifier or extend TerrainHeightProviderBase to support:
    - float SampleHolographicIntensity(Vector3 unitDirection)
  - Composition:
    - finalHeight = baseNoise.Sample(unitDirection, res) + holography.Sample(unitDirection) * holographyScale * mask(unitDirection)

- New config fields (TerrainConfig):
  - holography.enabled : bool
  - holography.mode : enum { Procedural, BakedFFT }
  - holography.params : (emitters[], k, amplitude, mask settings)
  - holography.bakedTextures[] (if baked): one texture per cube face + LOD mipmaps

Method responsibilities (single-method-per-step)
- GenerateHolographyParams(depthOrSeed) -> HolographyParams
- SampleHolographicIntensity(unitDirection, HolographyParams) -> float
- BakeHologramForFace(faceIndex, resolution, silhouetteMask) -> Texture2D (FFT)
- BlendHolographyWithNoise(baseHeight, intensity, mask) -> float
- ValidateBandLimit(maxK, targetMeshResolution) -> void

Pseudocode (procedural wave-sum):
```csharp
HolographyParams SetupEmitters(seed){
  // deterministic emitter positions/phases based on seed
}
float SampleHolographicIntensity(Vector3 dir, HolographyParams p){
  Complex sum = 0;
  foreach(emitter in p.emitters){
    float d = Dot(dir, emitter.direction); // plane wave approx
    float phase = p.k * d + emitter.phase;
    sum += emitter.amp * Complex.Exp(I * phase);
  }
  float intensity = sum.MagnitudeSquared();
  return Mathf.Clamp01( LowPassFilter(intensity) );
}