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
        for (int i = 0; i < covered.Length; i++) {
            if (covered[i] > 0) {
                sol.coveredSum += 1;
            }
        }

        // score[v] = number of undominated vertices in closed neighbourhood N[v]
        int[] score = new int[size];
        bool[] inHeap = new bool[size];
        var heap = new IndexedMaxHeap(size);

        foreach (int node in graph.GetNodes()) {
            int s = IsCovered(node) ? 0 : 1; // self
            foreach (int nbr in graph.GetEdges(node))
                if (!IsCovered(nbr)) s++;
            score[node] = s;
            heap.Insert(node, s);
            inHeap[node] = true;
        }

        while (!heap.IsEmpty() && !sol.IsSolutionValid()) {
            int v = heap.RemoveMax();
            inHeap[v] = false;

            if (IsCovered(v)) continue; // stale — already dominated by a prior selection

            // Collect newly dominated vertices BEFORE AddVertex updates covered[]
            var newlyDominated = new List<int>();
            if (!IsCovered(v)) newlyDominated.Add(v);
            foreach (int nbr in graph.GetEdges(v))
                if (!IsCovered(nbr)) newlyDominated.Add(nbr);

            sol.AddVertex(v);

            // For each vertex that just became dominated, decrement score of its neighbours
            foreach (int d in newlyDominated) {
                foreach (int nbr in graph.GetEdges(d)) {
                    score[nbr]--;
                    if (inHeap[nbr])
                        heap.UpdateKey(nbr, score[nbr]);
                }
            }
        }

        return sol;
    }
}

