using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Minimal cube-sphere utilities for face indexing and parameterization.
    /// Face order assumed: +X, -X, +Y, -Y, +Z, -Z (can adjust to match existing system).
    /// </summary>
    public static class CubeSphere
    {
        public static Vector3 FaceLocalToUnit(int face, float x, float y)
        {
            // x,y assumed in [-1,1]
            Vector3 v;
            switch (face)
            {
                case 0: v = new Vector3(1, y, -x); break; // +X
                case 1: v = new Vector3(-1, y, x); break; // -X
                case 2: v = new Vector3(x, 1, -y); break; // +Y
                case 3: v = new Vector3(x, -1, y); break; // -Y
                case 4: v = new Vector3(x, y, 1); break; // +Z
                case 5: v = new Vector3(-x, y, -1); break; // -Z
                default: v = Vector3.up; break;
            }
            // project to sphere (cube -> sphere approximation, simple normalization good enough at these resolutions)
            return v.normalized;
        }
    }
}
