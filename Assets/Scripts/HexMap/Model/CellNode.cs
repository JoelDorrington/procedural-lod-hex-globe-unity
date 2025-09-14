using System.Runtime.InteropServices;
using UnityEngine;

namespace HexGlobeProject.HexMap.Model
{
    // Blittable cell node used for contiguous storage.
    [StructLayout(LayoutKind.Sequential)]
    public struct CellNode
    {
        public int index;       // stable index in arrays
        public int firstNeigh;  // start index into flat neighbors[]
        public byte neighCount; // number of neighbors
        public int parent;      // parent node index for hierarchy (-1 if none)
        public int childStart;  // start index into children[]
        public byte childCount; // number of children
        public ushort flags;    // region/attribute bitflags

        public static CellNode CreateEmpty(int idx)
        {
            return new CellNode
            {
                index = idx,
                firstNeigh = -1,
                neighCount = 0,
                parent = -1,
                childStart = -1,
                childCount = 0,
                flags = 0
            };
        }
    }
}
