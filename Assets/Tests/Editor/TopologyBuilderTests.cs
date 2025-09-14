using NUnit.Framework;
using HexGlobeProject.HexMap.Model;
using System.Linq;
using UnityEngine;

namespace HexGlobeProject.Tests.Editor
{
    public class TopologyBuilderTests
    {
        [Test]
        public void BuildTopology_IsDeterministic_And_IndexResolves()
        {
            var cfg = new TopologyConfig();
            // create a small 4-tile test with cross links
            cfg.entries.Add(new TopologyConfig.TileEntry{ tileId = 100, neighbors = new[]{101,102}, center = Vector3.right});
            cfg.entries.Add(new TopologyConfig.TileEntry{ tileId = 101, neighbors = new[]{100,103}, center = Vector3.up});
            cfg.entries.Add(new TopologyConfig.TileEntry{ tileId = 102, neighbors = new[]{100,103}, center = Vector3.left});
            cfg.entries.Add(new TopologyConfig.TileEntry{ tileId = 103, neighbors = new[]{101,102}, center = Vector3.down});

            var resultA = TopologyBuilder.Build(cfg, new SparseMapIndex());
            var resultB = TopologyBuilder.Build(cfg, new SparseMapIndex());

            // Node counts
            Assert.AreEqual(resultA.nodes.Length, resultB.nodes.Length);
            // Node indices and neighbor slices must match
            for (int i=0;i<resultA.nodes.Length;i++)
            {
                var a = resultA.nodes[i];
                var b = resultB.nodes[i];
                Assert.AreEqual(a.index, b.index);
                Assert.AreEqual(a.firstNeigh, b.firstNeigh);
                Assert.AreEqual(a.neighCount, b.neighCount);
            }

            // Flat neighbors must be identical
            Assert.IsTrue(resultA.neighbors.SequenceEqual(resultB.neighbors));

            // Index must resolve mapped TileIds deterministically
            foreach (var e in cfg.entries)
            {
                Assert.IsTrue(resultA.index.TryGetIndex(e.tileId, out int ia));
                Assert.IsTrue(resultB.index.TryGetIndex(e.tileId, out int ib));
                Assert.AreEqual(ia, ib);
            }
        }
    }
}
