using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexGlobeProject.HexMap.Model
{
    // Lightweight migration record used by tests/editor tooling to seed topology + state
    public struct MigrationRecord
    {
        public int tileId;
        public int[] neighbors;
        public Vector3 center;
        public float population;
        public int maxPopulation;
        public int allegiance;
        public bool hasUnit;
        public int unitId;
    }

    // Authoritative game model holding per-cell mutable state (SOA) and exposing gameplay APIs.
    public class GameModel
    {
        // topology reference
        private TopologyResult topo;

        // SOA per-cell state
        public float[] population;
        public int[] maxPopulation;
        public int[] allegiance;
        public byte[] hasUnit;
        public int[] unitId;

        public int CellCount => topo?.nodes?.Length ?? 0;

        public GameModel() { }

        public void Initialize(TopologyResult topology)
        {
            topo = topology ?? throw new ArgumentNullException(nameof(topology));
            int n = topo.nodes.Length;
            population = new float[n];
            maxPopulation = new int[n];
            allegiance = new int[n];
            hasUnit = new byte[n];
            unitId = new int[n];
            for (int i=0;i<n;i++) { population[i]=0f; maxPopulation[i]=1; allegiance[i]=-1; hasUnit[i]=0; unitId[i]=-1; }
        }

        // Deterministic migration from records: builds topology and initial state.
        public static GameModel BuildFromRecords(IEnumerable<MigrationRecord> records, ITileIdIndex indexOut = null)
        {
            var cfg = new TopologyConfig();
            var recs = new List<MigrationRecord>(records);
            foreach (var r in recs)
            {
                cfg.entries.Add(new TopologyConfig.TileEntry{ tileId = r.tileId, neighbors = r.neighbors, center = r.center });
            }

            var topo = TopologyBuilder.Build(cfg, indexOut ?? new SparseMapIndex());
            var model = new GameModel();
            model.Initialize(topo);

            // apply initial state deterministically: map each record tileId to index and populate arrays
            foreach (var r in recs)
            {
                if (!topo.index.TryGetIndex(r.tileId, out int idx)) continue;
                model.population[idx] = r.population;
                model.maxPopulation[idx] = r.maxPopulation;
                model.allegiance[idx] = r.allegiance;
                model.hasUnit[idx] = r.hasUnit ? (byte)1 : (byte)0;
                model.unitId[idx] = r.hasUnit ? r.unitId : -1;
            }

            return model;
        }

        public bool TrySpawnPopulation(int nodeIndex, float amount)
        {
            if (!Valid(nodeIndex) || amount <= 0) return false;
            population[nodeIndex] += amount;
            if (population[nodeIndex] > maxPopulation[nodeIndex]) population[nodeIndex] = maxPopulation[nodeIndex];
            return true;
        }

        public bool TryPlaceUnit(int nodeIndex, int uId)
        {
            if (!Valid(nodeIndex)) return false;
            if (hasUnit[nodeIndex] != 0) return false;
            hasUnit[nodeIndex] = 1;
            unitId[nodeIndex] = uId;
            return true;
        }

        public bool TryMoveUnit(int fromIndex, int toIndex, int uId)
        {
            if (!Valid(fromIndex) || !Valid(toIndex)) return false;
            if (hasUnit[fromIndex] == 0) return false;
            if (unitId[fromIndex] != uId) return false;
            if (hasUnit[toIndex] != 0) return false;
            // move
            hasUnit[fromIndex] = 0; unitId[fromIndex] = -1;
            hasUnit[toIndex] = 1; unitId[toIndex] = uId;
            return true;
        }

        public bool TryChangeAllegiance(int nodeIndex, int playerId)
        {
            if (!Valid(nodeIndex)) return false;
            allegiance[nodeIndex] = playerId;
            return true;
        }

        public float SamplePopulation(int nodeIndex)
        {
            if (!Valid(nodeIndex)) return 0f;
            return population[nodeIndex];
        }

        private bool Valid(int idx) => topo != null && idx >= 0 && idx < topo.nodes.Length;
    }
}
