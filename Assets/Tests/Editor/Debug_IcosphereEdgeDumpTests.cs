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
            foreach (var bary in IcosphereMapping.TileVertexBarys(res))
            {
                var global = IcosphereMapping.BaryLocalToGlobal(tileAId, bary, res);
                var dir = IcosphereMapping.BaryToWorldDirection(tileAId.face, global);
                Debug.Log($"A idx={idx} local={bary} global={global} dir={dir}");
                idx++;
            }

            Debug.Log("Tile B bary and world dirs:");
            idx = 0;
            foreach (var bary in IcosphereMapping.TileVertexBarys(res))
            {
                var global = IcosphereMapping.BaryLocalToGlobal(tileBId, bary, res);
                var dir = IcosphereMapping.BaryToWorldDirection(tileBId.face, global);
                Debug.Log($"B idx={idx} local={bary} global={global} dir={dir}");
                idx++;
            }

            Assert.Pass("Dump complete");
        }
    }
}
