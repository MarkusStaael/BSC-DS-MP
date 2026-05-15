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

            int actualScore = (cov[v] == 0) ? 1 : 0;
            foreach (int nb in graph.GetEdges(v))
                if (cov[nb] == 0) actualScore++;
            if (actualScore == 0) continue;

            // Record which vertices become newly covered
            var newlyCovered = new List<int>();
            graph.AddVertexToSol(v);

            
            if (cov[v] == 1) newlyCovered.Add(v);

            foreach (int nb in graph.GetEdges(v))
                if (cov[nb] == 1) newlyCovered.Add(nb);


            foreach (int c in newlyCovered) {
                score[c]--;
                heap.UpdateKey(c, score[c]);
                foreach (int nbr in graph.GetEdges(c)) {
                    score[nbr]--;
                    heap.UpdateKey(nbr, score[nbr]);
                }
            }
        }

        return graph;
    }
}