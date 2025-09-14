using NUnit.Framework;
using HexGlobeProject.HexMap.Model;
using System.Collections.Generic;

namespace HexGlobeProject.Tests.Editor
{
    public class DensePerDepthIndexTests
    {
        [Test]
        public void DenseIndex_BuildsAndResolves()
        {
            var codec = new SimplePackedTileIdCodec();
            var idx = new DensePerDepthIndex(codec);

            var entries = new List<KeyValuePair<int,int>>();
            // create two tileIds at depth 1, faces 0 and 1, local indices 0..1
            int t00 = codec.Encode(1,0,0);
            int t01 = codec.Encode(1,0,1);
            int t10 = codec.Encode(1,1,0);

            entries.Add(new KeyValuePair<int,int>(t00, 5));
            entries.Add(new KeyValuePair<int,int>(t01, 6));
            entries.Add(new KeyValuePair<int,int>(t10, 7));

            idx.Build(entries);

            Assert.IsTrue(idx.TryGetIndex(t00, out int i0));
            Assert.AreEqual(5, i0);
            Assert.IsTrue(idx.TryGetIndex(t01, out int i1));
            Assert.AreEqual(6, i1);
            Assert.IsTrue(idx.TryGetIndex(t10, out int i2));
            Assert.AreEqual(7, i2);
        }
    }
}
