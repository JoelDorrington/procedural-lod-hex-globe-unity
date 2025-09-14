namespace HexGlobeProject.HexMap.Model
{
    // Abstract codec for TileId encoding/decoding so index implementations stay agnostic.
    public interface ITileIdCodec
    {
        void Decode(int tileId, out int depth, out int face, out int localIndex);
        int Encode(int depth, int face, int localIndex);
    }
}
