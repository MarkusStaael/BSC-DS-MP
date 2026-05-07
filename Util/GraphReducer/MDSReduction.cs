using BSC_DS_MP.DataStructures.Graph;

namespace BSC_DS_MP.Util.Reduction;

public class MDSReduction : IReduction {

    // --- State (valid after Reduce) ---

    int n;
    bool[] removed;    // vertex eliminated from the reduced graph (original-ID space)
    bool[] forced;     // vertex forced into DS (subset of removed)
    bool[] dominated;  // vertex dominated at reduction time

    int[] originalToReduced; // original ID → reduced ID, -1 if removed
    int[] reducedToOriginal; // reduced ID → original ID

    public int OriginalSize { get; private set; }
    public int ReducedSize  { get; private set; }
    public List<int> ForcedVertices { get; private set; } = new();

    // Mutable adjacency in original-ID space — edges are removed as vertices are eliminated
    List<int>[] adj;

    // --- Reduce ---

    public AdjLstWithSolGraph Reduce(IGraph original) {
        n = original.getSize();
        OriginalSize = n;

        removed           = new bool[n];
        forced            = new bool[n];
        dominated         = new bool[n];
        originalToReduced = new int[n];
        reducedToOriginal = new int[n];
        ForcedVertices    = new();
        ForcedVertices    = new();

        // Build mutable adjacency copy
        adj = new List<int>[n];
        for (int v = 0; v < n; v++) adj[v] = new List<int>();
        foreach (int v in original.GetNodes())
            foreach (int u in original.GetEdges(v))
                if (u > v) { adj[v].Add(u); adj[u].Add(v); }

        // --- Reduction rule loop ---
        int[] mark = new int[n];
        int markStamp = 0;
        changed = true;
        while (changed) {
            changed = false;
            for (int v = 0; v < n; v++) {
                if (removed[v]) continue;

                // Rule 1: isolated vertex — must be in DS (unless already dominated)
                if (adj[v].Count == 0) {
                    if (!dominated[v]) ForceIntoDS(v);
                    else               RemoveVertex(v);
                    continue;
                }

                // Rule 2: degree-1, undominated — force sole neighbor into DS
                if (!dominated[v] && adj[v].Count == 1) {
                    ForceIntoDS(adj[v][0]);
                    RemoveVertex(v);
                    continue;
                }

                // Rule 3: v is dominated AND every neighbor of v is also dominated →
                // v cannot contribute new coverage, so remove without forcing into DS.
                if (dominated[v]) {
                    bool allNeighborsDominated = true;
                    foreach (int u in adj[v]) {
                        if (!dominated[u]) { allNeighborsDominated = false; break; }
                    }
                    if (allNeighborsDominated) {
                        RemoveVertex(v);
                        continue;
                    }
                }

                // Rule 5: degree-2 triangle — v undominated, deg=2, neighbors u and w adjacent.
                // One of {v,u,w} must be in the DS to dominate v; both u and w cover the whole
                // triangle, so force whichever subsumes the other (N[w]⊆N[u] → force u).
                if (!dominated[v] && adj[v].Count == 2) {
                    int u5 = adj[v][0], w5 = adj[v][1];
                    if (!removed[u5] && !removed[w5]) {
                        // Mark N[u5]; triangle exists iff w5 ∈ N[u5]
                        markStamp++;
                        mark[u5] = markStamp;
                        foreach (int nb in adj[u5]) mark[nb] = markStamp;

                        if (mark[w5] == markStamp) {
                            // Triangle confirmed. Check N[w5] ⊆ N[u5]
                            bool wSubsumedByU = true;
                            foreach (int nb in adj[w5])
                                if (mark[nb] != markStamp) { wSubsumedByU = false; break; }
                            if (wSubsumedByU) { ForceIntoDS(u5); continue; }

                            // Check N[u5] ⊆ N[w5]
                            markStamp++;
                            mark[w5] = markStamp;
                            foreach (int nb in adj[w5]) mark[nb] = markStamp;
                            bool uSubsumedByW = true;
                            foreach (int nb in adj[u5])
                                if (mark[nb] != markStamp) { uSubsumedByW = false; break; }
                            if (uSubsumedByW) { ForceIntoDS(w5); continue; }
                        }
                    }
                }



            }
        }

        // --- Compact: assign contiguous IDs to surviving vertices ---
        int k = 0;
        for (int v = 0; v < n; v++) {
            if (removed[v]) { originalToReduced[v] = -1; continue; }
            originalToReduced[v] = k;
            reducedToOriginal[k] = v;
            k++;
        }
        ReducedSize = k;

        // --- Build reduced AdjLstWithSolGraph ---
        var reduced = new AdjLstWithSolGraph(k);

        // Add edges (adj[v] now contains only surviving neighbors)
        for (int v = 0; v < n; v++) {
            if (removed[v]) continue;
            int rv = originalToReduced[v];
            foreach (int u in adj[v]) {
                int ru = originalToReduced[u];
                if (ru > rv) reduced.AddEdge(rv, ru);
            }
        }

        // --- Pre-populate coverage from forced vertices ---
        // adj[v] was already cleared when v was removed, so use originalNeighbors.
        // We only update CoveredCount and TotalDominatedVertices here; UncoveredVertices
        // is left empty and maintained entirely by AddVertexToSol/RemoveVertexFromSol
        // once GreedyDecreaseKey and CC2FS take over.
        for (int v = 0; v < n; v++) {
            if (!forced[v]) continue;
            foreach (int u in original.GetEdges(v)) {
                if (removed[u]) continue;
                int ru = originalToReduced[u];
                reduced.CoveredCount[ru]++;
                if (reduced.CoveredCount[ru] == 1) {
                    reduced.TotalDominatedVertices++;
                }
            }
        }

        return reduced;
    }

    // --- Reconstruct ---

    public ISolution Reconstruct(ISolution reducedSolution) {
        var result = new BitArraySolution(OriginalSize);

        // Map surviving solution vertices back to original IDs
        foreach (int r in reducedSolution.GetEnumerator())
            result.AddVertex(reducedToOriginal[r]);

        // Add forced vertices
        for (int v = 0; v < n; v++)
            if (forced[v]) result.AddVertex(v);

        return result;
    }

    // --- Private helpers ---

    bool changed;

    private void ForceIntoDS(int v) {
        forced[v]    = true;
        dominated[v] = true;
        ForcedVertices.Add(v);
        foreach (int u in adj[v])
            dominated[u] = true;
        RemoveVertex(v);
    }

    private void RemoveVertex(int v) {
        removed[v] = true;
        changed    = true;
        // Splice v out of each neighbor's adjacency list
        foreach (int u in adj[v])
            adj[u].Remove(v);
        adj[v].Clear();
    }
}
