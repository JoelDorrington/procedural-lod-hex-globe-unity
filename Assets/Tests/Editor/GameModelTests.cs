using NUnit.Framework;
using HexGlobeProject.HexMap.Model;
using System.Collections.Generic;
using UnityEngine;

namespace HexGlobeProject.Tests.Editor
{
    public class GameModelTests
    {
        [Test]
        public void SpawnPopulation_PlaceAndMoveUnit_ChangeAllegiance()
        {
            var recs = new List<MigrationRecord>();
            // create two adjacent tiles: A(100) <-> B(101)
            recs.Add(new MigrationRecord{ tileId = 100, neighbors = new[]{101}, center = Vector3.zero, population = 0f, maxPopulation = 5, allegiance = -1, hasUnit = false, unitId = -1 });
            recs.Add(new MigrationRecord{ tileId = 101, neighbors = new[]{100}, center = Vector3.right, population = 0f, maxPopulation = 5, allegiance = -1, hasUnit = false, unitId = -1 });

            var model = GameModel.BuildFromRecords(recs);
            Assert.AreEqual(2, model.CellCount);

            // spawn population on index 0
            Assert.IsTrue(model.TrySpawnPopulation(0, 1f));
            Assert.AreEqual(1f, model.SamplePopulation(0));

            // place unit id 42 at index 0
            Assert.IsTrue(model.TryPlaceUnit(0, 42));
            Assert.AreEqual(1, model.hasUnit[0]);

            // try move unit from 0 to 1
            Assert.IsTrue(model.TryMoveUnit(0, 1, 42));
            Assert.AreEqual(0, model.hasUnit[0]);
            Assert.AreEqual(1, model.hasUnit[1]);

            // change allegiance
            Assert.IsTrue(model.TryChangeAllegiance(1, 7));
            Assert.AreEqual(7, model.allegiance[1]);
        }
    }
}
