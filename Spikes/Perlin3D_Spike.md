# Spike: 3D Perlin Noise for HexGlobeProject

Goal
------
Provide a compact, actionable spike that explains why we want a proper 3D noise implementation, how it integrates with the existing `TerrainHeightProviderBase` API, and include a reference C# implementation of 3D Perlin noise (seeded permutation table, fade, grad/dot) suitable for quick experimentation and benchmarking.

Context and motivation
----------------------
- Current `SimplePerlinHeightProvider` uses Unity's 2D `Mathf.PerlinNoise` and rotates components between octaves. That is deterministic and cheap but:  
  - It samples 2D noise on projected components and permutes them per-octave, which can introduce directional bias.  
  - Mesh extrema depend on sampling grid density: higher mesh resolution can reveal maxima/minima missed by coarse sampling. This is expected but can be reduced by using coherent 3D noise.  
- A true 3D noise function (Perlin or Simplex) removes the need for component permutations and yields isotropic noise (directionally unbiased). It also allows multi-octave spectral composition without awkward coordinate shuffling.

Design goals / constraints
--------------------------
- API: Keep the existing `TerrainHeightProviderBase.Sample(in Vector3 unitDirection, int resolution)` signature. The new provider must ignore `resolution` for height value determinism.  
- Determinism: seeded, repeatable results across editor/test runs.  
- Performance: reasonably fast for many samples (tiles have resolution^2 vertices). Prioritize a correct, simple implementation first; optimize later (SIMD, lookup caches, 3D simplex if faster).  
- Range/scale: provide a consistent amplitude and frequency API like current provider (baseFrequency, octaves, lacunarity, gain, amplitude).  

Approach summary
----------------
1. Implement a self-contained `Perlin3D` static helper with:
   - seeded permutation table (size 512: p[512] repeating 0..255) seeded via an LCG or System.Random.
   - grad function for 3D gradients (hash-> gradient vector lookups).
   - fade (s-curve), lerp, dot product.
   - Noise3D(Vector3 p) -> float in [-1,1] (or scaled to [0,1] depending on choice).
2. Implement `Perlin3DHeightProvider : TerrainHeightProviderBase` that composes octaves using Noise3D, similar controls to `SimplePerlinHeightProvider`.
3. Wire it into `TerrainConfig` (optionally) so tests can swap providers.
4. Add unit tests verifying determinism, seed behaviour, and resolution-agnostic sampling (provider sampled directly at identical world directions returns identical values for different `resolution` arguments).

Reference C# implementation (starter)
-------------------------------------
Below is a compact, readable Perlin3D implementation suitable for a spike. It prefers clarity over micro-optimizations.

```csharp
// Place this in Assets/Scripts/Util/Perlin3D.cs or similar for experimentation.
using UnityEngine;
using System;

public static class Perlin3D
{
    private static int[] p = null; // permutation table (size 512)

    public static void Init(int seed)
    {
        p = new int[512];
        var rnd = new System.Random(seed);
        var perm = new int[256];
        for (int i = 0; i < 256; i++) perm[i] = i;
        // Fisher-Yates shuffle
        for (int i = 255; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            int tmp = perm[i]; perm[i] = perm[j]; perm[j] = tmp;
        }
        for (int i = 0; i < 512; i++) p[i] = perm[i & 255];
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static float Lerp(float a, float b, float t) => a + t * (b - a);

    // Grad converts hash to gradient and returns dot(grad, x,y,z)
    private static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15; // 16 gradients
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        float res = ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        return res;
    }

    // Returns Perlin noise in range approximately [-1,1]
    public static float Noise(Vector3 v)
    {
        if (p == null) Init(1337); // default seed

        float x = v.x, y = v.y, z = v.z;
        int X = Mathf.FloorToInt(x) & 255;
        int Y = Mathf.FloorToInt(y) & 255;
        int Z = Mathf.FloorToInt(z) & 255;

        x -= Mathf.Floor(x);
        y -= Mathf.Floor(y);
        z -= Mathf.Floor(z);

        float u = Fade(x);
        float v2 = Fade(y);
        float w = Fade(z);

        int A  = p[X] + Y;
        int AA = p[A] + Z;
        int AB = p[A + 1] + Z;
        int B  = p[X + 1] + Y;
        int BA = p[B] + Z;
        int BB = p[B + 1] + Z;

        float res = Lerp(
            Lerp(
                Lerp(Grad(p[AA], x, y, z), Grad(p[BA], x - 1, y, z), u),
                Lerp(Grad(p[AB], x, y - 1, z), Grad(p[BB], x - 1, y - 1, z), u), v2),
            Lerp(
                Lerp(Grad(p[AA + 1], x, y, z - 1), Grad(p[BA + 1], x - 1, y, z - 1), u),
                Lerp(Grad(p[AB + 1], x, y - 1, z - 1), Grad(p[BB + 1], x - 1, y - 1, z - 1), u), v2),
            w);

        // res is roughly in [-1,1] - do not re-normalize aggressively here
        return res;
    }
}
```

Example HeightProvider skeleton
--------------------------------
Create `Perlin3DHeightProvider : TerrainHeightProviderBase` with the same public fields as `SimplePerlinHeightProvider` and implement `Sample(in Vector3 unitDirection, int resolution)` like:

```csharp
public override float Sample(in Vector3 unitDirection, int resolution)
{
    // resolution ignored by contract
    Vector3 p = unitDirection * baseFrequency;
    float sum = 0f; float amp = 1f; float freq = 1f;
    for (int i = 0; i < octaves; i++)
    {
        sum += Perlin3D.Noise(p * freq) * amp;
        freq *= lacunarity;
        amp *= gain;
    }
    return sum * amplitude;
}
```

Testing suggestions
-------------------
- Unit tests (editor):
  - Determinism: same seed + same direction => identical values across runs.
  - Resolution agnostic: for N random unit directions, assert Sample(dir, r1) == Sample(dir, r2) for multiple resolutions.
  - Spectral sanity: sampled values are within expected amplitude bounds.
  - Performance microbenchmark: sample performance for e.g. 1024x1024 grid to measure ms/sample and estimate cost on larger tiles.

Integration notes
------------------
- Replace `SimplePerlinHeightProvider` with the new `Perlin3DHeightProvider` in `TerrainConfig` during experiments.  
- `PlanetTileMeshBuilder` will continue calling `provider.Sample(in dir, res)` and will get isotropic 3D noise.  
- If you want backward compatibility, implement `Perlin3DHeightProvider` alongside `SimplePerlinHeightProvider` and expose a `providerType` setting in `TerrainConfig` or change the inspector reference.

Optimizations and follow-ups
---------------------------
- Use 3D Simplex noise (faster/scales better) if profiling shows Perlin3D too slow.  
- Cache band-limited precomputation for each octave if you need to anti-alias high-frequency content when sampling sparse grids.  
- Consider adding a small analytic prefilter or mipmapping for noise to reduce extrema differences across resolutions (more complex).

Spike tasks (priority)
-----------------------
1. Implement `Perlin3D` helper (this spike includes a simple implementation).  
2. Implement `Perlin3DHeightProvider` and wire into `TerrainConfig` for experiments.  
3. Add editor unit tests for determinism & resolution-agnostic behavior.  
4. Run mesh generation tests and visually inspect tiles at different resolutions to confirm fractal detail emerges properly.

References
----------
- Ken Perlin's original paper and reference implementations.  
- Stefan Gustavson's Simplex noise notes for a possibly faster alternative.  
- Unity documentation for Mathf.PerlinNoise (2D) for behavior comparison.

End of spike.
