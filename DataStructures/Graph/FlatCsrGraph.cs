using System;
using System.Collections.Generic;

namespace BSC_DS_MP.DataStructures.Graph;

public class FlatCsrGraphFactory : IGraphFactory {
    public IGraph Create(int size) {
        return new FlatCsrGraph(size);
    }
}

/// <summary>
/// Memory-efficient graph that buffers edges during construction, then freezes
/// into CSR format on first query. Avoids per-vertex HashSet/List allocations.
///
/// Construction: O(1) per AddEdge (append to flat buffer)
/// Freeze: O(n + m) one-time conversion to CSR
/// Queries: O(degree) via contiguous array segment — cache-friendly
///
/// For a 966K vertex graph, this avoids ~1M HashSet allocations that
/// AdjSetLstGraph creates, dramatically reducing GC pressure.
/// </summary>
public class FlatCsrGraph : IGraph {
    private readonly int size;

    // --- Construction phase (mutable) ---
    private int[] edgeFrom;
    private int[] edgeTo;
    private int edgeCount;
    private bool frozen;

    // --- Frozen phase (CSR, immutable) ---
    private int[] offsets;  // offsets[v] = start index in neighbors; offsets[size] = total
    private int[] neighbors; // contiguous neighbor IDs

    public FlatCsrGraph(int size) {
        this.size = size;
        int initialCapacity = Math.Max(size, 1024);
        edgeFrom = new int[initialCapacity];
        edgeTo = new int[initialCapacity];
        edgeCount = 0;
        frozen = false;
    }

    public int getSize() => size;

    public void AddNode(int id) {
        // No-op: all nodes 0..size-1 exist implicitly
    }

    public void AddEdge(int from, int to) {
        if (frozen) throw new InvalidOperationException("Graph is frozen after first query");

        // Ensure capacity (store both directions)
        if (edgeCount + 2 > edgeFrom.Length) {
            int newCap = edgeFrom.Length * 2;
            Array.Resize(ref edgeFrom, newCap);
            Array.Resize(ref edgeTo, newCap);
        }

        // Store both directions of undirected edge
        edgeFrom[edgeCount] = from;
        edgeTo[edgeCount] = to;
        edgeCount++;
        edgeFrom[edgeCount] = to;
        edgeTo[edgeCount] = from;
        edgeCount++;
    }

    private void Freeze() {
        if (frozen) return;
        frozen = true;

        // Count degrees
        offsets = new int[size + 1];
        for (int i = 0; i < edgeCount; i++)
            offsets[edgeFrom[i] + 1]++;

        // Prefix sum
        for (int v = 0; v < size; v++)
            offsets[v + 1] += offsets[v];

        // Fill neighbor array
        neighbors = new int[offsets[size]];
        int[] pos = new int[size];
        Array.Copy(offsets, pos, size);
        for (int i = 0; i < edgeCount; i++) {
            int from = edgeFrom[i];
            neighbors[pos[from]++] = edgeTo[i];
        }

        // Free construction buffers
        edgeFrom = Array.Empty<int>();
        edgeTo = Array.Empty<int>();
    }

    public IEnumerable<int> GetEdges(int node) {
        if (!frozen) Freeze();
        return new ArraySegment<int>(neighbors, offsets[node], offsets[node + 1] - offsets[node]);
    }

    /// <summary>
    /// Span-based neighbor access for zero-allocation iteration (same as CsrGraph).
    /// Call after construction is complete.
    /// </summary>
    public ReadOnlySpan<int> GetNeighborSpan(int node) {
        if (!frozen) Freeze();
        return new ReadOnlySpan<int>(neighbors, offsets[node], offsets[node + 1] - offsets[node]);
    }

    public int Degree(int node) {
        if (!frozen) Freeze();
        return offsets[node + 1] - offsets[node];
    }

    public IEnumerable<int> GetNodes() {
        return System.Linq.Enumerable.Range(0, size);
    }

    public void RemoveNode(int id) {
        throw new NotSupportedException("FlatCsrGraph is immutable after construction");
    }

    public IGraph CloneInto(IGraphFactory fac) {
        if (!frozen) Freeze();
        IGraph ret = fac.Create(size);
        for (int v = 0; v < size; v++) {
            int start = offsets[v];
            int end = offsets[v + 1];
            for (int i = start; i < end; i++) {
                int u = neighbors[i];
                if (v < u) // avoid duplicate edges
                    ret.AddEdge(v, u);
            }
        }
        return ret;
    }
}
