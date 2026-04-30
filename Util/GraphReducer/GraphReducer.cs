using BSC_DS_MP.DataStructures.Graph;
using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Util; 
public class GraphReducer : IGraphReducer {

    private int[] _newToOld = Array.Empty<int>();
    private List<int> _forcedDS = new();

    public GraphReducer() {
    }

    public (IGraph, int[],int) Reduce(IGraph graph) {
        int n = graph.getSize();
        _forcedDS = new List<int>();

        // Mutable adjacency copy (clean: only active nodes appear)
        HashSet<int>[] adj = new HashSet<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new HashSet<int>();
        foreach (int v in graph.GetNodes())
            foreach (int u in graph.GetEdges(v))
                if (u > v) { adj[v].Add(u); adj[u].Add(v); }

        bool[] removed   = new bool[n];
        bool[] dominated = new bool[n];
        int forcedCount  = 0;

        bool changed = true;
        while (changed) {
            changed = false;

            for (int v = 0; v < n; v++) {
                if (removed[v]) continue;

                //// Rule 1: dominated node — safe to remove from graph
                //if (dominated[v]) {
                //    removed[v] = true;
                //    foreach (int u in adj[v]) adj[u].Remove(v);
                //    adj[v].Clear();
                //    changed = true;
                //    continue;
                //}

                // Rule 2: isolated undominated node — must cover itself
                if (adj[v].Count == 0) {
                    forcedCount++;
                    _forcedDS.Add(v);
                    removed[v] = true;
                    dominated[v] = true;
                    changed = true;
                    continue;
                }

                // Rule 3: pendant (deg 1) — force its sole neighbour into DS
                if (adj[v].Count == 1) {
                    int u = -1;
                    foreach (int x in adj[v]) { u = x; break; }

                    // Force u into DS: u dominates itself and all its neighbours
                    forcedCount++;
                    _forcedDS.Add(u);
                    foreach (int w in adj[u]) {
                        dominated[w] = true;
                        adj[w].Remove(u);
                    }
                    adj[u].Clear();
                    dominated[u] = true;
                    removed[u]   = true;

                    // v is now dominated — mark as removed too
                    dominated[v] = true;
                    removed[v]   = true;
                    changed = true;
                }
            }
        }

        // Remap surviving nodes to contiguous IDs
        int[] oldToNew = new int[n];
        int newSize = 0;
        for (int v = 0; v < n; v++)
            oldToNew[v] = removed[v] ? -1 : newSize++;

        _newToOld = new int[newSize];
        for (int v = 0; v < n; v++)
            if (!removed[v]) _newToOld[oldToNew[v]] = v;

        IGraph reduced = new AdjLstGraph(newSize);
        for (int v = 0; v < n; v++) {
            if (removed[v]) continue;
            foreach (int u in adj[v])
                if (u > v)
                    reduced.AddEdge(oldToNew[v], oldToNew[u]);
        }

        return (reduced, new int[newSize], n - newSize);
    }

    public ISolution Reconstruct(ISolution reducedSolution) {
        var full = new HashSetSolution(_newToOld.Length + _forcedDS.Count);

        // Map reduced-graph IDs back to original IDs
        foreach (int newId in reducedSolution.GetEnumerator())
            full.AddVertex(_newToOld[newId]);

        // Add nodes forced into DS during reduction
        foreach (int v in _forcedDS)
            full.AddVertex(v);

        return full;
    }
}
