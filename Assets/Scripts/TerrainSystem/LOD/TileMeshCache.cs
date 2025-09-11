using System.Collections.Generic;
using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Minimal TileMeshCache used by tests. Provides a simple in-memory cache
    /// with a singleton Instance, EnsureScheduled stub, Add, and Get methods.
    /// This is intentionally small and synchronous for TDD interface tests.
    /// </summary>
    public class TileMeshCache
    {
        public static TileMeshCache Instance { get; } = new TileMeshCache();

        private readonly Dictionary<TileId, Mesh> _cache = new Dictionary<TileId, Mesh>();

        // Public parameterless ctor kept private to encourage singleton usage,
        // but tests will still be able to instantiate via reflection if needed.
        private TileMeshCache() { }

        /// <summary>
        /// Ensure that a mesh build is scheduled for the given tile.
        /// Returns a handle object (nullable) - tests only assert this can be invoked.
        /// </summary>
        public object EnsureScheduled(TileId tile, int priority = 0)
        {
            // No scheduling implemented yet; return a trivial non-throwing handle.
            return null;
        }

        /// <summary>
        /// Add a built mesh to the cache for the given tile.
        /// </summary>
        public void Add(TileId tile, Mesh mesh)
        {
            if (mesh == null) return;
            _cache[tile] = mesh;
        }

        /// <summary>
        /// Retrieve a cached mesh for the given tile, or null if missing.
        /// </summary>
        public Mesh Get(TileId tile)
        {
            _cache.TryGetValue(tile, out var mesh);
            return mesh;
        }
    }
}
