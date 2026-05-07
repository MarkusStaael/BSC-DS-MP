using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using BSC_DS_MP.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Numerics;
using System.Text;
using System.Xml.Linq;

namespace BSC_DS_MP.Solvers;

public class GreedyDecreaseKey {
    public static AdjLstWithSolGraph Solve(AdjLstWithSolGraph graph) {
        int size = graph.getSize();
        int[] cov = graph.CoveredCount;

        // Build heap scores: number of uncovered neighbours + uncovered self
        int[] score = new int[size];
        foreach (int v in graph.GetNodes()) {
            int s = (cov[v] == 0) ? 1 : 0;
            foreach (int nb in graph.GetEdges(v))
                if (cov[nb] == 0) s++;
            score[v] = s;
        }
        var heap = new IndexedMaxHeap(size);
        heap.MakeHeap(score);

        while (!heap.IsEmpty() && graph.TotalDominatedVertices < size) {
            int v = heap.RemoveMax();

            if (cov[v] > 0) continue; // lazy skip: already covered

            // Record which vertices become newly covered
            var newlyCovered = new List<int>();
            graph.AddVertexToSol(v);
            // v itself
            if (cov[v] == 1) newlyCovered.Add(v);
            // neighbours (AddVertexToSol already incremented them)
            foreach (int nb in graph.GetEdges(v))
                if (cov[nb] == 1) newlyCovered.Add(nb);

            // Decrement scores of uncovered neighbours of newly covered vertices
            foreach (int c in newlyCovered) {
                foreach (int nbr in graph.GetEdges(c)) {
                    if (cov[nbr] == 0) {
                        score[nbr]--;
                        heap.DecreaseKey(nbr, score[nbr]);
                    }
                }
            }
        }

        return graph;
    }
}