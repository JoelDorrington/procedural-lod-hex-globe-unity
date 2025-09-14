using System;

namespace HexGlobeProject.Pathfinding
{
    // Minimal binary heap for indices with associated float keys and position map.
    public class OpenSetHeap
    {
        private int[] heap; // node indices
        private float[] keys;
        private int[] pos; // pos[nodeIndex] = position in heap or -1
        private int count;

        public OpenSetHeap(int capacity, int nodeCount)
        {
            heap = new int[capacity];
            keys = new float[capacity];
            pos = new int[nodeCount];
            for (int i=0;i<pos.Length;i++) pos[i] = -1;
            count = 0;
        }

        public void Clear()
        {
            for (int i=0;i<count;i++) pos[heap[i]] = -1;
            count = 0;
        }

        public void Push(int node, float key)
        {
            int i = count++;
            heap[i] = node;
            keys[i] = key;
            pos[node] = i;
            HeapifyUp(i);
        }

        public bool TryPop(out int node, out float key)
        {
            if (count == 0) { node = -1; key = 0; return false; }
            node = heap[0];
            key = keys[0];
            count--;
            if (count > 0)
            {
                heap[0] = heap[count];
                keys[0] = keys[count];
                pos[heap[0]] = 0;
                HeapifyDown(0);
            }
            pos[node] = -1;
            return true;
        }

        public void DecreaseKey(int node, float newKey)
        {
            int i = pos[node];
            if (i < 0) return;
            if (newKey >= keys[i]) return;
            keys[i] = newKey;
            HeapifyUp(i);
        }

        private void HeapifyUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (keys[i] >= keys[p]) break;
                Swap(i, p);
                i = p;
            }
        }

        private void HeapifyDown(int i)
        {
            while (true)
            {
                int l = (i << 1) + 1;
                int r = l + 1;
                int smallest = i;
                if (l < count && keys[l] < keys[smallest]) smallest = l;
                if (r < count && keys[r] < keys[smallest]) smallest = r;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            var ta = heap[a]; var tb = heap[b];
            heap[a] = tb; heap[b] = ta;
            var ka = keys[a]; var kb = keys[b];
            keys[a] = kb; keys[b] = ka;
            pos[heap[a]] = a; pos[heap[b]] = b;
        }
    }
}
