using System;
using System.Collections.Generic;

namespace HexGlobeProject.HexMap.Model
{
    // Dense per-depth index. Uses ITileIdCodec to decode tileId into depth,face,localIndex.
    // Stores a per-depth array sized to max localIndex+1 for that depth.
    public class DensePerDepthIndex : ITileIdIndex
    {
        private readonly ITileIdCodec codec;
        private Dictionary<int, int[]> depthArrays; // depth -> per-depth linear map [faceOffset+local] = index
        private Dictionary<int,int> faceStrides; // face -> stride per face (for simple tiling, optional)

        public DensePerDepthIndex(ITileIdCodec codec)
        {
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            depthArrays = new Dictionary<int,int[]>();
            faceStrides = new Dictionary<int,int>();
        }

        public bool TryGetIndex(int tileId, out int index)
        {
            codec.Decode(tileId, out int depth, out int face, out int localIndex);
            if (!depthArrays.TryGetValue(depth, out var arr)) { index = -1; return false; }
            int pos = (face * faceStrides[depth]) + localIndex;
            if (pos < 0 || pos >= arr.Length) { index = -1; return false; }
            index = arr[pos];
            return index != -1;
        }

        public void Build(IEnumerable<KeyValuePair<int,int>> entries)
        {
            // entries: (tileId->nodeIndex)
            depthArrays.Clear();
            faceStrides.Clear();

            // First pass: decode and compute per-depth max face and localIndex
            var perDepth = new Dictionary<int, (int maxFace, int maxLocal)>();
            var decoded = new List<(int tileId,int depth,int face,int local,int node)>();
            foreach (var kv in entries)
            {
                codec.Decode(kv.Key, out int depth, out int face, out int local);
                decoded.Add((kv.Key, depth, face, local, kv.Value));
                if (!perDepth.TryGetValue(depth, out var tup)) tup = (0,0);
                tup.maxFace = Math.Max(tup.maxFace, face);
                tup.maxLocal = Math.Max(tup.maxLocal, local);
                perDepth[depth] = tup;
            }

            // allocate arrays per depth
            foreach (var kv in perDepth)
            {
                int depth = kv.Key;
                int faces = kv.Value.maxFace + 1;
                int localSize = kv.Value.maxLocal + 1;
                int stride = localSize; // simple stride: localIndex range
                faceStrides[depth] = stride;
                depthArrays[depth] = CreateFilledArray(faces * stride, -1);
            }

            // Fill entries
            foreach (var d in decoded)
            {
                var arr = depthArrays[d.depth];
                int pos = d.face * faceStrides[d.depth] + d.local;
                arr[pos] = d.node;
            }
        }

        private static int[] CreateFilledArray(int size, int fill)
        {
            var a = new int[size];
            for (int i=0;i<size;i++) a[i] = fill;
            return a;
        }
    }
}
