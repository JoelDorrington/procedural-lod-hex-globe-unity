using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{
    /// <summary>
    /// Small math-only selector to map camera direction to a canonical TileId and k-ring neighbors.
    /// This is a deterministic, fast selector used to replace the old raycast heuristic.
    /// </summary>
    public static class MathVisibilitySelector
    {
        // Return the canonical tile (face,x,y) for a world direction at the given depth
        public static TileId TileFromDirection(Vector3 direction, int depth)
        {
            IcosphereMapping.WorldDirectionToTileFaceIndex(direction, out int face);
            IcosphereMapping.BarycentricFromWorldDirection(face, direction, out float u, out float v);

            int tilesPerEdge = 1 << depth;
            int x = Mathf.Clamp(Mathf.FloorToInt(u * tilesPerEdge), 0, tilesPerEdge - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(v * tilesPerEdge), 0, tilesPerEdge - 1);

            // Correct for triangle layout where u+v>1 mapping might fold
            if (!IcosphereMapping.IsValidTileIndex(x, y, depth))
            {
                // Mirror index across the face diagonal (canonical folding)
                x = Mathf.Clamp(tilesPerEdge - 1 - x, 0, tilesPerEdge - 1);
                y = Mathf.Clamp(tilesPerEdge - 1 - y, 0, tilesPerEdge - 1);
            }

            return new TileId(face, x, y, depth);
        }

    // Generate k-ring neighbors (including center) on the same face only.
    // If an optional focus direction is supplied, prioritize and sort the
    // returned tiles by angular distance to that direction (nearest first).
    // This helps the prefetch buffer choose nearby tiles first.
    public static List<TileId> GetKRing(TileId center, int k, Vector3? focusDirection = null)
        {
            var set = new HashSet<TileId>();
            int tilesPerEdge = 1 << center.depth;

            // Add same-face neighbors within the square k-window
            for (int dx = -k; dx <= k; dx++)
            {
                for (int dy = -k; dy <= k; dy++)
                {
                    int nx = center.x + dx;
                    int ny = center.y + dy;
                    if (IcosphereMapping.IsValidTileIndex(nx, ny, center.depth))
                    {
                        set.Add(new TileId(center.face, nx, ny, center.depth));
                    }
                }
            }

            // For each tile in the current set, also include tiles implied by its corner directions.
            // This maps corner world directions back into canonical TileIds and ensures the k-ring
            // expands across face boundaries when tiles lie on edges.
            var snapshot = set.ToList();
            foreach (var t in snapshot)
            {
                int tilesPE = 1 << t.depth;
                float u0 = (float)t.x / tilesPE;
                float v0 = (float)t.y / tilesPE;
                float u1 = (float)(t.x + 1) / tilesPE;
                float v1 = v0;
                float u2 = u0;
                float v2 = (float)(t.y + 1) / tilesPE;

                // Sample a set of points within and around the tile: the three corners,
                // the tile center, and mid-edge points. This increases robustness when
                // tiles lie near face boundaries so the k-ring expands across faces.
                float eps = 0.01f;
                // Build a list of samples: allow corner/mid-edge samples to go slightly
                // outside the triangle so that directions crossing face boundaries are
                // detected. Keep the tile center clamped inside the triangle.
                var samplesList = new List<(float u, float v)>();
                // corners: perturb both directions slightly outward
                samplesList.Add((u0 - eps, v0 - eps));
                samplesList.Add((u1 + eps, v1 - eps));
                samplesList.Add((u2 - eps, v2 + eps));
                // additional near-corner perturbations to catch edge-crossing
                samplesList.Add((u0 + eps, v0 + eps));
                samplesList.Add((u1 - eps, v1 + eps));
                samplesList.Add((u2 + eps, v2 - eps));
                // center (clamped)
                samplesList.Add((Mathf.Clamp01((u0 + u1 + u2) / 3f), Mathf.Clamp01((v0 + v1 + v2) / 3f)));
                // mid-edges: allow slight outward perturbation along normal edge direction
                samplesList.Add(((u0 + u1) * 0.5f - eps, (v0 + v1) * 0.5f));
                samplesList.Add(((u1 + u2) * 0.5f + eps, (v1 + v2) * 0.5f));
                samplesList.Add(((u2 + u0) * 0.5f, (v2 + v0) * 0.5f - eps));

                foreach (var s in samplesList)
                {
                    var dir = IcosphereMapping.BarycentricToWorldDirection(t.face, s.u, s.v).normalized;
                    var neighbor = TileFromDirection(dir, t.depth);
                    if (!set.Contains(neighbor)) set.Add(neighbor);
                }
            }

            var list = set.ToList();

            // If a focusDirection is provided, sort by angular distance between
            // tile centroid direction and the focus. Otherwise return arbitrary order.
            if (focusDirection.HasValue)
            {
                var fd = focusDirection.Value.normalized;
                list.Sort((a, b) =>
                {
                    // compute centroid direction for tile a and b using canonical barycentric center
                    IcosphereMapping.GetTileBarycentricCenter(a.x, a.y, a.depth, out float auc, out float avc);
                    var adir = IcosphereMapping.BarycentricToWorldDirection(a.face, auc, avc).normalized;

                    IcosphereMapping.GetTileBarycentricCenter(b.x, b.y, b.depth, out float buc, out float bvc);
                    var bdir = IcosphereMapping.BarycentricToWorldDirection(b.face, buc, bvc).normalized;

                    float adot = Vector3.Dot(adir, fd);
                    float bdot = Vector3.Dot(bdir, fd);
                    // higher dot == closer angular distance, so sort descending
                    return bdot.CompareTo(adot);
                });
            }

            return list;
        }
    }
}
