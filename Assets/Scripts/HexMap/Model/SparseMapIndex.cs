using System.Collections.Generic;

namespace HexGlobeProject.HexMap.Model
{
    // Simple sparse mapping for TileId -> index. Deterministic Build by iterating sorted keys.
    public class SparseMapIndex : ITileIdIndex
    {
        private Dictionary<int,int> map;

        public SparseMapIndex()
        {
            map = new Dictionary<int,int>();
        }

        public bool TryGetIndex(int tileId, out int index)
        {
            return map.TryGetValue(tileId, out index);
        }

        public void Build(IEnumerable<KeyValuePair<int,int>> entries)
        {
            // deterministic build: clear then insert keys in ascending order
            map.Clear();
            var list = new List<KeyValuePair<int,int>>(entries);
            list.Sort((a,b)=> a.Key.CompareTo(b.Key));
            foreach (var kv in list)
                map[kv.Key] = kv.Value;
        }
    }
}
