using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexGlobeProject.HexMap.Model
{
    [Serializable]
    public class TopologyConfig
    {
        // Each entry is a tileId and its neighbor tileIds (expected small array, typical 6)
        public List<TileEntry> entries = new List<TileEntry>();

        [Serializable]
        public struct TileEntry
        {
            public int tileId;
            public int[] neighbors;
            public Vector3 center;
        }
    }

    public class TopologyResult
    {
        public CellNode[] nodes;
        public int[] neighbors;
        public int[] children;
        public Vector3[] centers;
        public ITileIdIndex index;
    }

    public static class TopologyBuilder
    {
        // Deterministic builder: sorts input entries by tileId and assigns indices accordingly.
        public static TopologyResult Build(TopologyConfig cfg, ITileIdIndex indexOut = null)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            // Sort entries deterministically by tileId
            var list = new List<TopologyConfig.TileEntry>(cfg.entries);
            list.Sort((a,b)=> a.tileId.CompareTo(b.tileId));

            int n = list.Count;
            var nodes = new CellNode[n];
            var centers = new Vector3[n];

            // Prepare neighbor counts and flatten neighbors
            var neighborSlices = new List<int[]>();
            int totalNeighbors = 0;
            for (int i=0;i<n;i++)
            {
                var e = list[i];
                neighborSlices.Add(e.neighbors ?? Array.Empty<int>());
                totalNeighbors += (e.neighbors?.Length ?? 0);
                centers[i] = e.center;
            }

            var flatNeighbors = new int[totalNeighbors];
            int neighPos = 0;
            var tileIdToIndex = new List<KeyValuePair<int,int>>(n);

            for (int i=0;i<n;i++)
            {
                nodes[i] = CellNode.CreateEmpty(i);
                var neigh = neighborSlices[i];
                nodes[i].firstNeigh = neighPos;
                nodes[i].neighCount = (byte)neigh.Length;
                // store neighbor tileIds for now; we'll translate to node indices after index build
                for (int j=0;j<neigh.Length;j++)
                {
                    flatNeighbors[neighPos++] = neigh[j];
                }
                tileIdToIndex.Add(new KeyValuePair<int,int>(list[i].tileId, i));
            }

            var result = new TopologyResult();
            result.nodes = nodes;
            result.neighbors = flatNeighbors;
            result.children = Array.Empty<int>();
            result.centers = centers;

            // Build index
            ITileIdIndex idx = indexOut ?? new SparseMapIndex();
            idx.Build(tileIdToIndex);

            // Translate flatNeighbors which currently contain neighbor tileIds into node indices using idx.
            for (int k = 0; k < flatNeighbors.Length; k++)
            {
                int neighborTileId = flatNeighbors[k];
                if (idx.TryGetIndex(neighborTileId, out int mapped))
                    flatNeighbors[k] = mapped;
                else
                    flatNeighbors[k] = -1; // missing neighbor
            }

            result.index = idx;

            return result;
        }
    }
}
