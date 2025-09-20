using System;
using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Stores baked mesh / height metadata for a tile (baked levels) or runtime extreme tiles.
    /// Mesh can be null until built.
    /// </summary>
    public class TileData
    {
        public TileId id;
        public int resolution; // verts per edge - 1 quads per edge
        public float error; // geometric error vs parent (screen-space later)
        public Vector3[] tileSlotCornerBounds; // corner positions of the tile slots on the ocean surface for pre-computed reasoning
        public Mesh mesh;
        public bool isBaked; // baked (Low/Medium/High) vs streamed (Extreme)
        public Vector3[] tempVerts; // transient during build (can be cleared)
        public Vector3[] normals;   // optional baked normals
        // Runtime spatial helpers
        public Vector3 center; // approximate tile center (world)

    }
}
