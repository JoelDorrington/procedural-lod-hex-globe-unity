using NUnit.Framework;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.Editor
{
    public class Debug_IcosphereEdgeDumpTests
    {
        [Test]
        public void DumpAdjacentTileEdgeBarys()
        {
            int depth = 0;
            var tileAId = new TileId(0,0,0, depth);
            var tileBId = new TileId(1,0,0, depth);
            int res = 8;

            Debug.Log("Tile A bary and world dirs:");
            int idx = 0;
            foreach (var bary in IcosphereMapping.TileVertexBarys(res, tileAId.depth, tileAId.x, tileAId.y))
            {
                var dir = IcosphereMapping.BaryToWorldDirection(tileAId.face, bary);
                Debug.Log($"A idx={idx} bary={bary} dir={dir}");
                idx++;
            }

            Debug.Log("Tile B bary and world dirs:");
            idx = 0;
            foreach (var bary in IcosphereMapping.TileVertexBarys(res, tileBId.depth, tileBId.x, tileBId.y))
            {
                var dir = IcosphereMapping.BaryToWorldDirection(tileBId.face, bary);
                Debug.Log($"B idx={idx} bary={bary} dir={dir}");
                idx++;
            }

            Assert.Pass("Dump complete");
        }
    }
}
