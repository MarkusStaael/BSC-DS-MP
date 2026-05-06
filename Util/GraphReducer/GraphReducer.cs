using BSC_DS_MP.DataStructures.Graph;
using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Util; 
public class GraphReducer : IGraphReducer {

    private int[] _newToOld = Array.Empty<int>();
    private List<int> _forcedDS = new();
    // Rule 4: for each node v removed by subsumption, store the dominator u
    // so Reconstruct can cover v if the reduced solution doesn't.
    private List<(int v, int dominator)> _rule4Pairs = new();
    // Original adjacency snapshot (needed for coverage check in Reconstruct).
    private HashSet<int>[] _origAdj = Array.Empty<HashSet<int>>();

    public GraphReducer() {
    }

    public (IGraph, int[],int) Reduce(IGraph graph) {
        int n = graph.getSize();
        _forcedDS = new List<int>();
        _rule4Pairs = new List<(int, int)>();

        // Mutable adjacency copy (clean: only active nodes appear)
        HashSet<int>[] adj = new HashSet<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new HashSet<int>();
        foreach (int v in graph.GetNodes())
            foreach (int u in graph.GetEdges(v))
                if (u > v) { adj[v].Add(u); adj[u].Add(v); }

        // Snapshot of original adjacency for Reconstruct coverage checks
        _origAdj = new HashSet<int>[n];
        for (int i = 0; i < n; i++) _origAdj[i] = new HashSet<int>(adj[i]);

        bool[] removed   = new bool[n];
        bool[] dominated = new bool[n];
        int forcedCount  = 0;

        bool changed = true;
        while (changed) {
            changed = false;

            for (int v = 0; v < n; v++) {
                if (removed[v]) continue;

                // Rule 1: dominated node whose every neighbour is also dominated
                // — v covers nothing new, safe to exclude from DS
                if (dominated[v]) {
                    bool allNeighboursDominated = true;
                    foreach (int u in adj[v]) {
                        if (!dominated[u]) { allNeighboursDominated = false; break; }
                    }
                    if (allNeighboursDominated) {
                        removed[v] = true;
                        foreach (int u in adj[v]) adj[u].Remove(v);
                        adj[v].Clear();
                        changed = true;
                        continue;
                    }
                }

                // Rule 4: closed-neighbourhood subsumption
                // If N[v] ⊆ N[u] for some active neighbour u, there is an
                // optimal DS not containing v (swap v→u dominates everything v did).
                // Skip degree-1 nodes: Rule 3 is strictly better for pendants.
                if (adj[v].Count >= 2) {
                    bool subsumed = false;
                    int witness = -1;
                    // Build N[v] = adj[v] ∪ {v}
                    foreach (int u in adj[v]) {
                        // Check N[v] ⊆ N[u]: every member of N[v] must be in N[u]
                        // N[v] = adj[v] ∪ {v}, N[u] = adj[u] ∪ {u}
                        // u is in N[u] (closed), so we need adj[v] ⊆ N[u] and v ∈ N[u]
                        // v ∈ N[u] iff v == u (false, neighbours) or v ∈ adj[u] — true since edge exists
                        bool ok = true;
                        foreach (int w in adj[v]) {
                            if (w == u) continue; // w = u is in N[u]
                            if (!adj[u].Contains(w)) { ok = false; break; }
                        }
                        // also need v itself ∈ N[u]: v ∈ adj[u] which is true (edge v-u exists)
                        if (ok) { subsumed = true; witness = u; break; }
                    }
                    if (subsumed) {
                        _rule4Pairs.Add((v, witness));
                        removed[v] = true;
                        foreach (int u in adj[v]) adj[u].Remove(v);
                        adj[v].Clear();
                        changed = true;
                        continue;
                    }
                }

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

        // Pre-coverage: surviving nodes already dominated by a forced neighbour
        // don't need to be covered by the reduced-graph solver.
        int[] reducedCoverage = new int[newSize];
        for (int v = 0; v < n; v++)
            if (!removed[v] && dominated[v])
                reducedCoverage[oldToNew[v]] = 1;

        return (reduced, reducedCoverage, n - newSize);
    }

    public ISolution Reconstruct(ISolution reducedSolution) {
        var full = new HashSetSolution(_newToOld.Length + _forcedDS.Count);

        // Map reduced-graph IDs back to original IDs
        foreach (int newId in reducedSolution.GetEnumerator())
            full.AddVertex(_newToOld[newId]);

        // Add nodes forced into DS during reduction
        foreach (int v in _forcedDS)
            full.AddVertex(v);

        // Rule 4 pairs: v was removed because N[v] ⊆ N[dominator].
        // If v is not covered by the current solution, add dominator to cover it.
        // Process in reverse order so later removals (which may depend on earlier
        // ones being resolved) are handled first.
        for (int i = _rule4Pairs.Count - 1; i >= 0; i--) {
            var (v, dom) = _rule4Pairs[i];
            // v is covered if v itself or any original neighbour of v is in full
            bool covered = full.Contains(v);
            if (!covered) {
                foreach (int nb in _origAdj[v]) {
                    if (full.Contains(nb)) { covered = true; break; }
                }
            }
            if (!covered)
                full.AddVertex(dom);
        }

        // Cleanup: remove any DS member that is now redundant.
        // Rule 4 cascades can cause the solver to return a solution S where
        // reconstruction adds witness u to cover removed node v, but S already
        // contained another node that makes u's contribution redundant (or vice
        // versa).  Removing redundant nodes is always sound.
        bool cleanupChanged = true;
        while (cleanupChanged) {
            cleanupChanged = false;
            var snapshot = new List<int>(full.GetEnumerator());
            foreach (int v in snapshot) {
                if (!full.Contains(v)) continue; // removed earlier in this pass

                // After hypothetically removing v, check every vertex in N_orig[v] ∪ {v}
                // is still covered by another DS member.
                bool redundant = true;

                // v itself must remain covered: some original neighbour of v is in full
                bool selfCovered = false;
                foreach (int nb in _origAdj[v])
                    if (full.Contains(nb)) { selfCovered = true; break; }
                if (!selfCovered) { redundant = false; }

                if (redundant) {
                    foreach (int nb in _origAdj[v]) {
                        // nb is covered if nb ∈ full, or some nb2 ∈ N_orig[nb] \ {v} is in full
                        bool nbCovered = full.Contains(nb);
                        if (!nbCovered) {
                            foreach (int nb2 in _origAdj[nb]) {
                                if (nb2 != v && full.Contains(nb2)) { nbCovered = true; break; }
                            }
                        }
                        if (!nbCovered) { redundant = false; break; }
                    }
                }

                if (redundant) {
                    full.RemoveVertex(v);
                    cleanupChanged = true;
                }
            }
        }

        return full;
    }
}
