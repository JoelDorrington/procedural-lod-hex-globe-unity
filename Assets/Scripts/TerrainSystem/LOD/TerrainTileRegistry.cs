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
                        IcosphereMapping.GetTileBaryCenter(depth, x, y, out float u, out float v);

                        var entry = new PrecomputedTileEntry();
                        var normal = IcosphereMapping.BaryToWorldDirection(face, u, v).normalized;
                        entry.normal = normal;
                        entry.centerWorld = entry.normal * planetRadius + planetCenter;

                        // Populate metadata fields for callers
                        entry.face = face;
                        entry.x = x;
                        entry.y = y;
                        entry.uCenter = u;
                        entry.vCenter = v;
                        entry.tilesPerEdge = tilesPerEdge;

                        float u0 = (float)x / tilesPerEdge;
                        float v0 = (float)y / tilesPerEdge;

                        entry.tileOffsetU = u0;
                        entry.tileOffsetV = v0;

                        var key = new TileId(face, x, y, depth);
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
        public Vector3 normal;
        public int face;
        public int x;
        public int y;
        public float uCenter;
        public float vCenter;
        public int tilesPerEdge;
        public float tileOffsetU;
        public float tileOffsetV;
        public Vector3 centerWorld;
        public Vector3[] cornerWorldPositions;
    }
}