using NUnit.Framework;
using HexGlobeProject.HexMap.Model;
using System.Collections.Generic;
using UnityEngine;

namespace HexGlobeProject.Tests.Editor
{
    public class DirectionalLookupTests
    {
        [Test]
        public void DirectionalLookup_Basic()
        {
            var cfg = new TopologyConfig();
            cfg.entries.Add(new TopologyConfig.TileEntry{ tileId = 1, neighbors = new int[0], center = Vector3.right });
            cfg.entries.Add(new TopologyConfig.TileEntry{ tileId = 2, neighbors = new int[0], center = Vector3.up });
            cfg.entries.Add(new TopologyConfig.TileEntry{ tileId = 3, neighbors = new int[0], center = Vector3.left });
            cfg.entries.Add(new TopologyConfig.TileEntry{ tileId = 4, neighbors = new int[0], center = Vector3.down });

            var topo = TopologyBuilder.Build(cfg, new SparseMapIndex());
            var lookup = new DirectionalCellLookup(topo, 8);

            int r = lookup.Lookup(Vector3.right);
            int u = lookup.Lookup(Vector3.up);
            int l = lookup.Lookup(Vector3.left);
            int d = lookup.Lookup(Vector3.down);

            Assert.AreEqual(0, r);
            Assert.AreEqual(1, u);
            Assert.AreEqual(2, l);
            Assert.AreEqual(3, d);
        }
    }
}
