using System;
namespace BSC_DS_MP.DataStructures.Graph;

/// <summary>
/// Compressed Sparse Row graph representation for cache-friendly neighbor iteration.
/// Built once from an IGraph, provides ReadOnlySpan access to neighbor arrays.
/// This representation is immutable after construction.
/// </summary>
/// 
internal class CsrGraph : IGraph {
    private readonly int[] _offsets; // offsets[v] = start; offsets[n] = total edges
    private readonly int[] _edges;   // contiguous neighbor IDs
    private readonly int _n;

    public int NodeCount => _n;

    public CsrGraph(IGraph source) {
        _n = source.getSize();
        _offsets = new int[_n + 1];

        // First pass: count degrees
        for (int v = 0; v < _n; v++) {
            int deg = 0;
            foreach (int _ in source.GetEdges(v)) deg++;
            _offsets[v + 1] = deg;
        }

        // Prefix sum
        for (int v = 0; v < _n; v++) {
            _offsets[v + 1] += _offsets[v];
        }

        _edges = new int[_offsets[_n]];

        // Second pass: fill edges
        int[] pos = new int[_n];
        Array.Copy(_offsets, pos, _n);
        for (int v = 0; v < _n; v++) {
            foreach (int u in source.GetEdges(v)) {
                _edges[pos[v]++] = u;
            }
        }
    }

    public ReadOnlySpan<int> GetNeighbors(int v) {
        return new ReadOnlySpan<int>(_edges, _offsets[v], _offsets[v + 1] - _offsets[v]);
    }

    public int Degree(int v) {
        return _offsets[v + 1] - _offsets[v];
    }

    public IGraph CloneInto(IGraphFactory fac) {
        IGraph clone = fac.Create(_n);
        foreach (int node in GetNodes()) {
            foreach (int neighbor in GetEdges(node)) {
                clone.AddEdge(node, neighbor);
            }
        }

        return clone;
    }

    public int getSize() {
        return _n;
    }

    public void AddNode(int id) {
        throw new NotSupportedException("CsrGraph is immutable.");
    }

    public void AddEdge(int from, int to) {
        throw new NotSupportedException("CsrGraph is immutable.");
    }

    public IEnumerable<int> GetEdges(int node) {
        int start = _offsets[node];
        int end = _offsets[node + 1];
        for (int index = start; index < end; index++) {
            yield return _edges[index];
        }
    }

    public void RemoveNode(int id) {
        throw new NotSupportedException("CsrGraph is immutable.");
    }

    public IEnumerable<int> GetNodes() {
        // the previous implementation excluded the last index; include full range
        return Enumerable.Range(0, NodeCount);
    }
}

/// <summary>
/// CSR representation for two-level neighborhoods, built once.
/// </summary>
internal class CsrTwoLevel {
    private readonly int[] _offsets;
    private readonly int[] _neighbors;

    public CsrTwoLevel(IGraph graph, CsrGraph csr) {
        int n = csr.NodeCount;
        _offsets = new int[n + 1];

        // Build two-level neighborhoods using stamp-marking
        int[] mark = new int[n];
        int currentMark = 1;
        var tempList = new System.Collections.Generic.List<int>();

        for (int v = 0; v < n; v++) {
            int stamp = currentMark++;
            mark[v] = stamp; // exclude self

            var n1 = csr.GetNeighbors(v);
            for (int i = 0; i < n1.Length; i++) {
                int u = n1[i];
                if (mark[u] != stamp) {
                    mark[u] = stamp;
                    tempList.Add(u);
                }
                var n2 = csr.GetNeighbors(u);
                for (int j = 0; j < n2.Length; j++) {
                    int w = n2[j];
                    if (mark[w] != stamp) {
                        mark[w] = stamp;
                        tempList.Add(w);
                    }
                }
            }

            _offsets[v + 1] = tempList.Count;
        }

        _neighbors = tempList.ToArray();
    }

    public ReadOnlySpan<int> GetNeighbors(int v) {
        return new ReadOnlySpan<int>(_neighbors, _offsets[v], _offsets[v + 1] - _offsets[v]);
    }
}
