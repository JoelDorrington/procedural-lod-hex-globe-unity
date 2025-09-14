using NUnit.Framework;
using HexGlobeProject.HexMap.Model;
using System.Collections.Generic;
using UnityEngine;

namespace HexGlobeProject.Tests.Editor
{
    public class GameModelNativeTests
    {
        [Test]
        public void NativeModel_SpawnPlaceMoveChange_Dispose()
        {
            var recs = new List<MigrationRecord>();
            recs.Add(new MigrationRecord{ tileId = 200, neighbors = new[]{201}, center = Vector3.zero, population = 0f, maxPopulation = 5, allegiance = -1, hasUnit = false, unitId = -1 });
            recs.Add(new MigrationRecord{ tileId = 201, neighbors = new[]{200}, center = Vector3.right, population = 0f, maxPopulation = 5, allegiance = -1, hasUnit = false, unitId = -1 });

            var model = GameModelNative.BuildFromRecords(recs);
            Assert.AreEqual(2, model.CellCount);

            Assert.IsTrue(model.TrySpawnPopulation(0, 2f));
            Assert.AreEqual(2f, model.SamplePopulation(0));

            Assert.IsTrue(model.TryPlaceUnit(0, 99));
            Assert.AreEqual(1, model.hasUnit[0]);

            Assert.IsTrue(model.TryMoveUnit(0,1,99));
            Assert.AreEqual(0, model.hasUnit[0]);
            Assert.AreEqual(1, model.hasUnit[1]);

            Assert.IsTrue(model.TryChangeAllegiance(1, 3));
            Assert.AreEqual(3, model.allegiance[1]);

            model.Dispose();
        }
    }
}
