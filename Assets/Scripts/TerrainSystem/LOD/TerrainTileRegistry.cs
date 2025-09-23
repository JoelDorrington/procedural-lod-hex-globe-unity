using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Registry of all terrain tiles at a given depth, used to support visibility selection strategies.
    /// This is populated by the TerrainTileProvider when tiles are created.
    /// </summary>
    public class TerrainTileRegistry
    {
    public readonly int depth;
    // Keyed by discrete TileId to avoid floating-point key collisions and ensure
    // one entry per discrete (face,x,y,depth) tile.
    public readonly Dictionary<TileId, PrecomputedTileEntry> tiles = new Dictionary<TileId, PrecomputedTileEntry>();

        public TerrainTileRegistry(int depth, float planetRadius, Vector3 planetCenter)
        {
            this.depth = depth;
            tiles = ComputeTilesForDepth(depth, planetCenter, planetRadius);
        }


        public IEnumerable<PrecomputedTileEntry> GetAllTiles()
        {
            return tiles.Values.AsEnumerable();
        }

        public void Clear()
        {
            tiles.Clear();
        }

        private Dictionary<TileId, PrecomputedTileEntry> ComputeTilesForDepth(int depth, Vector3 planetCenter, float planetRadius)
        {
            var _tiles = new Dictionary<TileId, PrecomputedTileEntry>();

            int tilesPerEdge = 1 << depth;
            for (int face = 0; face < 20; face++) // each face
            {
                for (int x = 0; x < tilesPerEdge; x++)
                {
                    for (int y = 0; y < tilesPerEdge; y++)
                    {
                        // Enumerate the full square grid per face. Tests expect exactly
                        // 20 * (tilesPerEdge^2) entries (no triangular filtering here).
                        var center = IcosphereMapping.TileIndexToBaryCenter(depth, x, y);
                        var normal = IcosphereMapping.BaryToWorldDirection(face, center);

                        var key = new TileId(face, x, y, depth);
                        var entry = new PrecomputedTileEntry();
                        
                        entry.normal = normal;
                        entry.centerWorld = IcosphereMapping.BaryToWorldDirection(face, center) * planetRadius + planetCenter;

                        // Populate metadata fields for callers
                        entry.depth = depth;
                        entry.face = face;
                        entry.x = x;
                        entry.y = y;
                        entry.cornerWorldPositions = IcosphereMapping.GetCorners(key, planetRadius, planetCenter);

                        _tiles.Add(key, entry);
                    }
                }
            }

            return _tiles;
        }
    }

    public struct PrecomputedTileEntry
    {
        public int depth; // LOD value, 0=root
        public int face; // Canonical face index 0..19
        public int x; // x index of subdivision
        public int y; // y index of subdivision
        public Vector3 normal;
        public Vector3 centerWorld;
        public Vector3[] cornerWorldPositions;
    }
}