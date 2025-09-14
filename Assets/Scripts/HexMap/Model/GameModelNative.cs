using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace HexGlobeProject.HexMap.Model
{
    // NativeArray-backed game model for Jobs-friendly access.
    public class GameModelNative : IDisposable
    {
        private TopologyResult topo;

        public NativeArray<float> population;
        public NativeArray<int> maxPopulation;
        public NativeArray<int> allegiance;
        public NativeArray<byte> hasUnit;
        public NativeArray<int> unitId;

        public int CellCount => topo?.nodes?.Length ?? 0;

        public void Initialize(TopologyResult topology)
        {
            topo = topology ?? throw new ArgumentNullException(nameof(topology));
            int n = topo.nodes.Length;
            population = new NativeArray<float>(n, Allocator.Persistent);
            maxPopulation = new NativeArray<int>(n, Allocator.Persistent);
            allegiance = new NativeArray<int>(n, Allocator.Persistent);
            hasUnit = new NativeArray<byte>(n, Allocator.Persistent);
            unitId = new NativeArray<int>(n, Allocator.Persistent);

            for (int i = 0; i < n; i++)
            {
                population[i] = 0f;
                maxPopulation[i] = 1;
                allegiance[i] = -1;
                hasUnit[i] = 0;
                unitId[i] = -1;
            }
        }

        public static GameModelNative BuildFromRecords(IEnumerable<MigrationRecord> records, ITileIdIndex indexOut = null)
        {
            var cfg = new TopologyConfig();
            var recs = new List<MigrationRecord>(records);
            foreach (var r in recs)
            {
                cfg.entries.Add(new TopologyConfig.TileEntry { tileId = r.tileId, neighbors = r.neighbors, center = r.center });
            }

            var topo = TopologyBuilder.Build(cfg, indexOut ?? new SparseMapIndex());
            var model = new GameModelNative();
            model.Initialize(topo);

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
            var val = population[nodeIndex] + amount;
            if (val > maxPopulation[nodeIndex]) val = maxPopulation[nodeIndex];
            population[nodeIndex] = val;
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

        public void Dispose()
        {
            if (population.IsCreated) population.Dispose();
            if (maxPopulation.IsCreated) maxPopulation.Dispose();
            if (allegiance.IsCreated) allegiance.Dispose();
            if (hasUnit.IsCreated) hasUnit.Dispose();
            if (unitId.IsCreated) unitId.Dispose();
        }
    }
}
