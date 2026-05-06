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
        private readonly int[] _position;  // _position[node] = index in _heap (1..size) or -1 if absent
        private int _size;

        public IndexedMaxHeap(int capacity)
        {
            _heap = new int[capacity + 1];
            _key = new int[capacity];
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

        public void Insert(int node, int key)
        {
            if (_position[node] != -1)
                throw new InvalidOperationException("node already in heap");
            _size++;
            _heap[_size] = node;
            _key[node] = key;
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

        private void HeapifyUp(int idx)
        {
            while (idx > 1)
            {
                int parent = idx / 2;
                if (_key[_heap[idx]] <= _key[_heap[parent]])
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
                if (left <= _size && _key[_heap[left]] > _key[_heap[largest]])
                    largest = left;
                if (right <= _size && _key[_heap[right]] > _key[_heap[largest]])
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
    }
}
