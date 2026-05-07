namespace BSC_DS_MP.DataStructures.Heap
{
    /// <summary>
    /// Simple indexed max-heap for vertices with integer keys.
    /// Provides O(log n) remove-max and decrease-key operations.
    /// The heap stores node identifiers (int) and keeps an array of their
    /// current keys.  A "position" array maps a node to its location in the
    /// binary-heap array so we can update the key efficiently.
    /// </summary>
    public class IndexedMaxHeap
    {
        private readonly int[] _heap;      // 1-based binary heap of node ids
        private readonly int[] _key;       // _key[node] = current key value
        private readonly uint[] _age;      // _age[node] = insertion timestamp (lower = older = preferred on tie)
        private readonly int[] _position;  // _position[node] = index in _heap (1..size) or -1 if absent
        private int _size;

        public IndexedMaxHeap(int capacity)
        {
            _heap = new int[capacity + 1];
            _key = new int[capacity];
            _age = new uint[capacity];
            _position = new int[capacity];
            for (int i = 0; i < capacity; i++)
                _position[i] = -1;
            _size = 0;
        }

        public bool IsEmpty() => _size == 0;
        public int Size() => _size;

        public void MakeHeap(int[] keys)
        {
            int n = keys.Length;
            if (n > _key.Length)
                throw new ArgumentException("keys array too large for heap capacity");
            _size = n;
            for (int node = 0; node < n; node++)
            {
                _key[node] = keys[node];
                _heap[node + 1] = node;
                _position[node] = node + 1;
            }
            for (int i = n / 2; i >= 1; i--)
                HeapifyDown(i);
        }

        public void Insert(int node, int key, uint age = 0)
        {
            if (_position[node] != -1)
                throw new InvalidOperationException("node already in heap");
            _size++;
            _heap[_size] = node;
            _key[node] = key;
            _age[node] = age;
            _position[node] = _size;
            HeapifyUp(_size);
        }

        public int RemoveMax()
        {
            if (_size == 0)
                throw new InvalidOperationException("heap is empty");
            int maxNode = _heap[1];
            _position[maxNode] = -1;
            if (_size > 1)
            {
                _heap[1] = _heap[_size];
                _position[_heap[1]] = 1;
            }
            _size--;
            if (_size > 0)
                HeapifyDown(1);
            return maxNode;
        }

        public void Remove(int node)
        {
            int pos = _position[node];
            if (pos == -1) return;
            _position[node] = -1;
            if (pos == _size)
            {
                _size--;
                return;
            }
            _heap[pos] = _heap[_size];
            _position[_heap[pos]] = pos;
            _size--;
            HeapifyUp(pos);
            HeapifyDown(pos);
        }

        public bool Contains(int node) => _position[node] != -1;

        /// <summary>Returns the node stored at zero-based <paramref name="index"/> in the heap array.</summary>
        public int GetNodeAt(int index) => _heap[index + 1];

        public void DecreaseKey(int node, int newKey)
        {
            int pos = _position[node];
            if (pos == -1)
                return; // not present
            if (newKey > _key[node])
                throw new ArgumentException("new key is larger than current key");
            _key[node] = newKey;
            HeapifyDown(pos);
        }

        public void UpdateKey(int node, int newKey) {
            int pos = _position[node];
            if (pos == -1)
                return;

            int oldKey = _key[node];
            _key[node] = newKey;

            if (newKey > oldKey)
                HeapifyUp(pos);
            else if (newKey < oldKey)
                HeapifyDown(pos);
        }

        // Returns true if nodeA should be above nodeB in the max-heap:
        // higher score wins; on a tie, smaller age wins (older node preferred).
        private bool IsGreater(int nodeA, int nodeB)
        {
            int ka = _key[nodeA], kb = _key[nodeB];
            if (ka != kb) return ka > kb;
            return _age[nodeA] < _age[nodeB];
        }

        private void HeapifyUp(int idx)
        {
            while (idx > 1)
            {
                int parent = idx / 2;
                if (!IsGreater(_heap[idx], _heap[parent]))
                    break;
                Swap(idx, parent);
                idx = parent;
            }
        }

        public void IncreaseKey(int node, int newKey) {
            int pos = _position[node];
            if (pos == -1)
                return; // not present
            if (newKey < _key[node])
                throw new ArgumentException("new key is smaller than current key");

            _key[node] = newKey;
            HeapifyUp(pos);
        }

        public void AdjustKey(int node, int delta) {
            int pos = _position[node];
            if (pos == -1) return;
            _key[node] += delta;
            if (delta > 0) HeapifyUp(pos);
            else if (delta < 0) HeapifyDown(pos);
        }

        private void HeapifyDown(int idx)
        {
            while (true)
            {
                int left = idx * 2;
                int right = left + 1;
                int largest = idx;
                if (left <= _size && IsGreater(_heap[left], _heap[largest]))
                    largest = left;
                if (right <= _size && IsGreater(_heap[right], _heap[largest]))
                    largest = right;
                if (largest == idx) break;
                Swap(idx, largest);
                idx = largest;
            }
        }

        private void Swap(int i, int j)
        {
            int ni = _heap[i];
            int nj = _heap[j];
            _heap[i] = nj;
            _heap[j] = ni;
            _position[ni] = j;
            _position[nj] = i;
        }

        /// <summary>
        /// Fills <paramref name="output"/> with up to <paramref name="k"/> node IDs
        /// that have the largest keys (ties broken by smallest age), without modifying
        /// the heap. Uses a BFS over heap positions so the result is the true top-k.
        /// Returns the actual number of entries written.
        /// </summary>
        public int PeekTopK(int k, int[] output)
        {
            if (_size == 0 || k <= 0) return 0;
            // Auxiliary max-heap of heap *positions*, ordered by the key/age of the
            // node sitting at each position. Max size reached during BFS is k+1.
            Span<int> aux = stackalloc int[k + 3]; // 1-based
            int auxSz = 0;
            PosHeapPush(aux, ref auxSz, 1);
            int filled = 0;
            while (filled < k && auxSz > 0)
            {
                int pos = PosHeapPop(aux, ref auxSz);
                output[filled++] = _heap[pos];
                int left = pos * 2, right = left + 1;
                if (left  <= _size) PosHeapPush(aux, ref auxSz, left);
                if (right <= _size) PosHeapPush(aux, ref auxSz, right);
            }
            return filled;
        }

        private void PosHeapPush(Span<int> h, ref int sz, int pos)
        {
            h[++sz] = pos;
            int i = sz;
            while (i > 1)
            {
                int parent = i / 2;
                if (!IsGreater(_heap[h[i]], _heap[h[parent]])) break;
                (h[i], h[parent]) = (h[parent], h[i]);
                i = parent;
            }
        }

        private int PosHeapPop(Span<int> h, ref int sz)
        {
            int top = h[1];
            h[1] = h[sz--];
            int i = 1;
            while (true)
            {
                int l = i * 2, r = l + 1, best = i;
                if (l <= sz && IsGreater(_heap[h[l]], _heap[h[best]])) best = l;
                if (r <= sz && IsGreater(_heap[h[r]], _heap[h[best]])) best = r;
                if (best == i) break;
                (h[i], h[best]) = (h[best], h[i]);
                i = best;
            }
            return top;
        }
    }
}
