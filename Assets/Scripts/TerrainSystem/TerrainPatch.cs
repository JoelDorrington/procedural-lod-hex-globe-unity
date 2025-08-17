using UnityEngine;

namespace HexGlobeProject.TerrainSystem
{
    public enum PatchState { Uninitialized, Generating, Ready, Disposed }

    /// <summary>
    /// Represents a single cube-face quadtree patch (LOD tile).
    /// </summary>
    public class TerrainPatch
    {
    public int face;      // 0..5 cube face index
    public int lod;       // always 0 in simplified system
    public int x;         // 0
    public int y;         // 0
    public PatchState state; // lifecycle state
    public Mesh mesh; // generated mesh
    public GameObject gameObject; // GameObject holder

        public string Name => $"Patch_f{face}_l{lod}_{x}_{y}";
    }
}
