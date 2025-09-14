using NUnit.Framework;
using HexGlobeProject.HexMap.Model;
using HexGlobeProject.Pathfinding;
using UnityEngine;

namespace HexGlobeProject.Tests.Editor
{
    public class PathfinderTests
    {
        [Test]
        public void Pathfinder_FindsPath_OnSimpleGraph()
        {
            var cfg = new TopologyConfig();
            cfg.entries.Add(new TopologyConfig.TileEntry{ tileId = 100, neighbors = new[]{101,102}, center = Vector3.right});
            cfg.entries.Add(new TopologyConfig.TileEntry{ tileId = 101, neighbors = new[]{100,103}, center = Vector3.up});
            cfg.entries.Add(new TopologyConfig.TileEntry{ tileId = 102, neighbors = new[]{100,103}, center = Vector3.left});
            cfg.entries.Add(new TopologyConfig.TileEntry{ tileId = 103, neighbors = new[]{101,102}, center = Vector3.down});

            var topo = TopologyBuilder.Build(cfg, new SparseMapIndex());

            var pathBuf = new PathBuffer(10);
            bool found = Pathfinder.TryFindPath(topo.nodes, topo.neighbors, topo.centers, 0, 3, ref pathBuf);
            Assert.IsTrue(found);
            // Expected path: start -> neighbor -> goal (3 nodes)
            Assert.AreEqual(3, pathBuf.Count);
            // verify path is 0->1->3 or 0->2->3 depending on neighbor ordering; first node must be start and last must be goal
            Assert.AreEqual(0, pathBuf.nodes[0]);
            Assert.AreEqual(3, pathBuf.nodes[pathBuf.Count-1]);
        }
    }
}
