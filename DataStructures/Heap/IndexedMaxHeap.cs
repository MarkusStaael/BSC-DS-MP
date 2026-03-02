namespace BSC_DS_MP.DataStructures.Heap
{
    /// <summary>
    /// Simple indexed max-heap for vertices with integer keys.
    /// Provides O(log n) remove-max and decrease-key operations.
    /// The heap stores node identifiers (int) and keeps an array of their
    /// current keys.  A "position" array maps a node to its location in the
    /// binary-heap array so we can update the key efficiently.
    /// </summary>
    internal class IndexedMaxHeap
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
