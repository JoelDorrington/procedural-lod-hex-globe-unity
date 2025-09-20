using System.Collections.Generic;
using HexGlobeProject.TerrainSystem.LOD;
using UnityEngine;

namespace HexGlobeProject.Tests.Editor
{
    public static class TileIdIterator
    {
        public static IEnumerable<TileId> IterateTileIds()
        {

            Vector3 planetCenter = Vector3.zero;
            float planetRadius = 1f;

            var registry0 = new TerrainTileRegistry(0, planetRadius, planetCenter);
            var registry1 = new TerrainTileRegistry(1, planetRadius, planetCenter);
            var registry2 = new TerrainTileRegistry(2, planetRadius, planetCenter);

            var registries = new TerrainTileRegistry[] { registry0, registry1, registry2 };

            foreach (var registry in registries)
            {
                foreach (var tileId in registry.tiles.Keys)
                {
                    yield return tileId;
                }
            }
        }
    }
}