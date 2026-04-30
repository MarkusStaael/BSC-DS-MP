using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using BSC_DS_MP.Solvers;
using BSC_DS_MP.Util;
using System;
using System.Collections.Generic;
using System.Text;
public class GreedyDecreaseKeySimpleSol {
    private int[] covered;
    IGraph graph;
    private bool IsCovered(int vertex) {
        return covered[vertex] >= 1;
    }
    private void IncreaseCoverage(int vertex) {
        covered[vertex]++;
    }

    public GreedyDecreaseKeySimpleSol(IGraph graph, int[]? covered) {
        this.graph = graph;
        if (covered != null) {
            this.covered = covered;
        } else {
            this.covered = new int[graph.getSize()];
        }
    }
    public SimpleSol Solve(IGraph graph, CancellationToken? token) {
        int size = graph.getSize();
        SimpleSol sol = new SimpleSol(graph);
        sol.coveredCount = covered;
        int coveredCount = 0;
        int[] coverage = new int[size];

        // store current coverage for each node (uncovered neighbours + self)
        var heap = new IndexedMaxHeap(size);

        // initialize heap with coverage values
        foreach (int node in graph.GetNodes()) {
            int cov = graph.GetEdges(node).Count() + 1;
            coverage[node] = cov;
            heap.Insert(node, cov);
        }
        // Console.WriteLine($"Initial heap size after insertion: {heap.Size()}");
        // optionally peek at coverage values of first few nodes
        /*for (int i = 0; i < Math.Min(10, coverage.Length); i++) {
            Console.Write(coverage[i] + ",");
        }
        Console.WriteLine();*/

        // Greedy selection until all vertices are covered
        int totalRemovals = 0;
        int skipCount = 0;
        while (!heap.IsEmpty() && coveredCount < size) {
            int selectedNode = heap.RemoveMax();
            totalRemovals++;

            // lazy skip (should not occur)
            if (IsCovered(selectedNode)) {
                skipCount++;
                continue;
            }

            // gather nodes that will become covered this round (selected + its neighbors)
            var newlyCovered = new List<int>();
            if (!IsCovered(selectedNode)) {
                IncreaseCoverage(selectedNode);
                coveredCount++;
                newlyCovered.Add(selectedNode);
                sol.AddVertex(selectedNode);
            }

            foreach (int neighbor in graph.GetEdges(selectedNode)) {
                if (!IsCovered(neighbor)) {
                    IncreaseCoverage(selectedNode);
                    coveredCount++;
                    newlyCovered.Add(neighbor);
                }
            }

            // for each node that just became covered, decrement coverage of its uncovered neighbours
            foreach (int c in newlyCovered) {
                foreach (int nbr in graph.GetEdges(c)) {
                    if (!IsCovered(nbr)) {
                        coverage[nbr]--;
                        heap.DecreaseKey(nbr, coverage[nbr]);
                    }
                }
            }
        }

        return sol;
    }
}

