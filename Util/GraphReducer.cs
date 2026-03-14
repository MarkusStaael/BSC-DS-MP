using System;
using System.Collections.Generic;
using BSC_DS_MP.DataStructures.Graph;

namespace BSC_DS_MP.Util;

/// <summary>
/// Preprocesses the graph with reduction rules before solving.
/// Applies pendant rule (degree-1) and isolated vertex rule (degree-0)
/// iteratively until no more reductions are possible.
/// Returns forced vertices + a smaller reduced graph with remapped IDs.
/// </summary>
public class GraphReducer {
    private readonly CsrGraph csr;
    private readonly int n;

    public int[] ForcedVertices { get; private set; }
    public int ReducedSize { get; private set; }
    public int[] OriginalId { get; private set; } // originalId[reducedId] = original vertex id
    public IGraph ReducedGraph { get; private set; }
    public int OriginalSize => n;

    public GraphReducer(IGraph graph) {
        n = graph.getSize();
        csr = new CsrGraph(graph);
        Reduce();
    }

    private void Reduce() {
        bool[] eliminated = new bool[n]; // removed from graph (forced into solution)
        bool[] dominated = new bool[n];  // covered by a forced vertex
        var forced = new List<int>();

        // Queue all degree-0 and degree-1 vertices
        var queue = new Queue<int>();
        for (int v = 0; v < n; v++) {
            if (csr.Degree(v) <= 1)
                queue.Enqueue(v);
        }

        while (queue.Count > 0) {
            int v = queue.Dequeue();
            if (eliminated[v] || dominated[v]) continue;

            // Compute effective degree (non-eliminated neighbors)
            int effDeg = 0;
            int pendant_neighbor = -1;
            var neighbors = csr.GetNeighbors(v);
            for (int i = 0; i < neighbors.Length; i++) {
                if (!eliminated[neighbors[i]]) {
                    effDeg++;
                    pendant_neighbor = neighbors[i];
                }
            }

            if (effDeg == 0) {
                // Isolated un-dominated vertex: must be in solution
                forced.Add(v);
                eliminated[v] = true;
                dominated[v] = true;
            } else if (effDeg == 1) {
                // Pendant: force the neighbor (always at least as good as forcing v)
                int u = pendant_neighbor;
                if (eliminated[u]) continue;

                forced.Add(u);
                eliminated[u] = true;
                dominated[u] = true;
                dominated[v] = true;

                // All neighbors of u become dominated
                var uNeighbors = csr.GetNeighbors(u);
                for (int i = 0; i < uNeighbors.Length; i++) {
                    int w = uNeighbors[i];
                    if (!eliminated[w])
                        dominated[w] = true;
                }

                // Eliminating u reduces effective degree of u's neighbors.
                // Re-queue un-dominated neighbors that might now be pendants.
                // Also re-queue u's neighbors' neighbors: their neighbor w lost
                // no edges (w isn't eliminated), but we need to check vertices
                // that had u as a neighbor — they are u's direct neighbors.
                for (int i = 0; i < uNeighbors.Length; i++) {
                    int w = uNeighbors[i];
                    if (!eliminated[w] && !dominated[w])
                        queue.Enqueue(w);
                }
            }
        }

        ForcedVertices = forced.ToArray();

        // Build reduced graph with only un-dominated vertices
        int[] newId = new int[n];
        Array.Fill(newId, -1);
        var origId = new List<int>();
        int newN = 0;

        for (int v = 0; v < n; v++) {
            if (!dominated[v]) {
                newId[v] = newN;
                origId.Add(v);
                newN++;
            }
        }

        ReducedSize = newN;
        OriginalId = origId.ToArray();

        // Build the reduced graph
        var reduced = new FlatCsrGraph(newN);
        for (int v = 0; v < n; v++) {
            if (newId[v] == -1) continue;
            var neighbors = csr.GetNeighbors(v);
            for (int i = 0; i < neighbors.Length; i++) {
                int u = neighbors[i];
                if (newId[u] != -1 && v < u)
                    reduced.AddEdge(newId[v], newId[u]);
            }
        }

        ReducedGraph = reduced;
    }

    /// <summary>
    /// Maps a solution on the reduced graph back to original vertex IDs,
    /// combining with forced vertices from preprocessing.
    /// </summary>
    public ISolution MapBack(ISolution reducedSolution, int originalSize) {
        var result = new BitArraySolution(originalSize);

        for (int i = 0; i < ForcedVertices.Length; i++)
            result.AddVertex(ForcedVertices[i]);

        foreach (int v in reducedSolution.GetEnumerator())
            result.AddVertex(OriginalId[v]);

        return result;
    }
}
