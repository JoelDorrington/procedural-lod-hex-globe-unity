using System;
using System.Collections.Generic;
using UnityEngine;
using HexGlobeProject.HexMap.Model;
namespace HexGlobeProject.Pathfinding
{
    public struct PathBuffer
    {
        public int[] nodes;
        public int Count;

        public PathBuffer(int capacity)
        {
            nodes = new int[capacity];
            Count = 0;
        }
    }

    public static class Pathfinder
    {
        public static bool TryFindPath(CellNode[] nodes, int[] neighbors, Vector3[] centers, int start, int goal, ref PathBuffer outPath)
        {
            if (start < 0 || goal < 0) return false;
            int n = nodes.Length;
            var open = new OpenSetHeap(n*2, n);
            var gscore = new float[n];
            var fscore = new float[n];
            var cameFrom = new int[n];
            for (int i=0;i<n;i++) { gscore[i] = float.PositiveInfinity; fscore[i] = float.PositiveInfinity; cameFrom[i] = -1; }

            gscore[start] = 0;
            fscore[start] = Heuristic(centers[start], centers[goal]);
            open.Push(start, fscore[start]);

            while (open.TryPop(out int current, out float _))
            {
                if (current == goal)
                {
                    // reconstruct
                    var stack = new List<int>();
                    int cur = goal;
                    while (cur != -1)
                    {
                        stack.Add(cur);
                        cur = cameFrom[cur];
                    }
                    stack.Reverse();
                    outPath.Count = stack.Count;
                    for (int i=0;i<stack.Count;i++) outPath.nodes[i] = stack[i];
                    return true;
                }

                int startNei = nodes[current].firstNeigh;
                int cnt = nodes[current].neighCount;
                for (int i=0;i<cnt;i++)
                {
                    int neiIndex = neighbors[startNei + i];
                    if (neiIndex < 0) continue;
                    float tentative = gscore[current] + Vector3.Distance(centers[current], centers[neiIndex]);
                    if (tentative < gscore[neiIndex])
                    {
                        cameFrom[neiIndex] = current;
                        gscore[neiIndex] = tentative;
                        fscore[neiIndex] = tentative + Heuristic(centers[neiIndex], centers[goal]);
                        // naive push; OpenSetHeap doesn't check duplicates for brevity
                        open.Push(neiIndex, fscore[neiIndex]);
                    }
                }
            }

            return false;
        }

        private static float Heuristic(Vector3 a, Vector3 b)
        {
            return Vector3.Distance(a, b);
        }
    }
}
