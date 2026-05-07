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
