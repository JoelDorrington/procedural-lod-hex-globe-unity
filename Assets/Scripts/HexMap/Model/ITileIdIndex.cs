using System.Collections.Generic;

namespace HexGlobeProject.HexMap.Model
{
    // Abstraction for TileId -> node index mapping.
    public interface ITileIdIndex
    {
        // Try resolve raw int tileId to node index. Encoding is intentionally opaque.
        bool TryGetIndex(int tileId, out int index);

        // Bulk populate mapping. Implementations should be deterministic.
        void Build(IEnumerable<KeyValuePair<int,int>> entries);
    }
}
