using System;

namespace HexGlobeProject.HexMap.Model
{
    // Reference codec: [localIndex:21][face:5][depth:6] (32-bit). This is a simple example.
    public class SimplePackedTileIdCodec : ITileIdCodec
    {
        private const int DepthBits = 6;
        private const int FaceBits = 5;
        private const int LocalBits = 32 - DepthBits - FaceBits;

        public int Encode(int depth, int face, int localIndex)
        {
            if (depth < 0 || depth >= (1<<DepthBits)) throw new ArgumentOutOfRangeException(nameof(depth));
            if (face < 0 || face >= (1<<FaceBits)) throw new ArgumentOutOfRangeException(nameof(face));
            if (localIndex < 0 || localIndex >= (1<<LocalBits)) throw new ArgumentOutOfRangeException(nameof(localIndex));
            return (localIndex << (DepthBits + FaceBits)) | (face << DepthBits) | depth;
        }

        public void Decode(int tileId, out int depth, out int face, out int localIndex)
        {
            depth = tileId & ((1<<DepthBits)-1);
            face = (tileId >> DepthBits) & ((1<<FaceBits)-1);
            localIndex = tileId >> (DepthBits + FaceBits);
        }
    }
}
