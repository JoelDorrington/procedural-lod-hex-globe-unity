Sun flare import workflow

1. Source image (GIMP):
   - Remove any white background. Use an alpha channel and erase or layer-mask the background so only the flare remains opaque.
   - Export as PNG with alpha (File → Export As → PNG; ensure "Save color values from transparent pixels" if asked).

2. Put the exported PNG into `Assets/Resources/` or another folder in the project.

3. In Unity Editor: select the imported texture asset, then from the menu `Assets → HexGlobe → Fix Sun Flare Import (Selected)`.
   - This will set: alpha from input, clamp wrap, bilinear filter, no mipmaps, and reasonable max size. It will then reimport the texture.

4. If using a Flare asset (LensFlare), ensure the Flare references the texture (or create a Material/Particle setup that uses the texture with Additive blending).

5. Tune `LensFlare.brightness` and color on the Light's LensFlare component or via your JSON (`sunFlareBrightness`).

Notes:
- If the flare still has white halo artifacts, ensure the PNG's transparent pixels have RGB=0 or use "Remove background color" in GIMP before exporting to avoid color bleed.
- For best results, store the high-resolution (2048) source in a separate folder and use a smaller copy for runtime if needed.
