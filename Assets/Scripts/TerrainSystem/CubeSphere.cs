using UnityEngine;

namespace HexGlobeProject.TerrainSystem
{
    /// <summary>
    /// Static helpers for cube-sphere coordinate transforms.
    /// </summary>
    public static class CubeSphere
    {
        // Enumerate faces as +X,-X,+Y,-Y,+Z,-Z
        public static readonly Vector3Int[] FaceNormals =
        {
            new(1,0,0), new(-1,0,0), new(0,1,0), new(0,-1,0), new(0,0,1), new(0,0,-1)
        };

        public static Vector3 FaceLocalToUnit(int face, float u, float v)
        {
            // u,v in [-1,1]
            switch (face)
            {
                case 0: return new Vector3( 1,  v, -u).normalized; // +X
                case 1: return new Vector3(-1,  v,  u).normalized; // -X
                case 2: return new Vector3( u,  1, -v).normalized; // +Y
                case 3: return new Vector3( u, -1,  v).normalized; // -Y
                case 4: return new Vector3( u,  v,  1).normalized; // +Z
                case 5: return new Vector3( u,  v, -1).normalized; // -Z
                default: return Vector3.zero;
            }
        }
    }
}
