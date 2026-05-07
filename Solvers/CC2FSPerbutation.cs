using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using BSC_DS_MP.Util;
using BSC_DS_MP.Util.Solution;
using Microsoft.VisualBasic;
using ScottPlot;
using ScottPlot.Panels;
using ScottPlot.Plottables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Transactions;


namespace BSC_DS_MP.Solvers;
// https://jair.org/index.php/jair/article/view/11044/26218
public class CC2FSPerbutation : CC2FS {

    public int GetRemovePerbCount() {
        return 3;//(int)(0.05 * graph.getSize());
    }
    public int GetAddPerbCount() {
        return 3;//(int)(0.05 * graph.getSize());
    }
    public double GetPerturbationProbability() {
        return 0.0001;
    }
    Random _random = new Random(0);
    public CC2FSPerbutation(AdjLstWithSolGraph graph, String printname, CancellationToken token) : base(graph, printname, token) {
    }
    protected void Perturb() {
        // Step 9: randomly select one vertex from RemoveHeap (all entries are in S)
        int randomVertex = RemoveHeap.GetNodeAt(_random.Next(RemoveHeap.Size()));
        RemoveHeap.Remove(randomVertex);
        InRemoveHeap[randomVertex] = false;
        RemoveVertex(randomVertex);

        // Steps 11-13: randomly remove floor(0.05 * |V|) more (in S, not in forbidlist)
        int perturbCount = GetRemovePerbCount(); //(int)(0.05 * graph.getSize());
        for (int i = 0; i < perturbCount; i++) {
            int heapSize = RemoveHeap.Size();
            if (heapSize == 0) break;
            int v = -1;
            for (int attempt = 0; attempt < heapSize; attempt++) {
                int candidate = RemoveHeap.GetNodeAt(_random.Next(heapSize));
                if (!forbidlist.Contains(candidate)) { v = candidate; break; }
            }
            if (v == -1) break;
            RemoveHeap.Remove(v);
            InRemoveHeap[v] = false;
            RemoveVertex(v);
        }
    }
    protected void AddPerturb() {
        // Randomly add one vertex from AddHeap (all entries are not in S)
        int randomVertex = AddHeap.GetNodeAt(_random.Next(AddHeap.Size()));
        AddHeap.Remove(randomVertex);
        InAddHeap[randomVertex] = false;
        AddVertex(randomVertex);

        // Randomly add floor(0.05 * |V|) more (not in S, not in forbidlist)
        int perturbCount = GetAddPerbCount(); //(int)(0.05 * graph.getSize());
        for (int i = 0; i < perturbCount; i++) {
            int heapSize = AddHeap.Size();
            if (heapSize == 0) break;
            int v = -1;
            for (int attempt = 0; attempt < heapSize; attempt++) {
                int candidate = AddHeap.GetNodeAt(_random.Next(heapSize));
                if (!forbidlist.Contains(candidate)) { v = candidate; break; }
            }
            if (v == -1) break;
            AddHeap.Remove(v);
            InAddHeap[v] = false;
            AddVertex(v);
        }
    }
    protected override void DoLoopLogic() {
        if (graph.IsSolutionValid()) {
            if (graph.GetSolutionCount() < BestSolution.Count()) {
                BestSolution = graph.GetAsRetSol();
            }
            int v = GetBestRemove(forbidList: true);
            RemoveVertex(v);
        } else {
            int v = GetBestRemove(forbidList: true);
            RemoveVertex(v);
            if (_random.NextDouble() < GetPerturbationProbability()) { 
                AddPerturb();
                Perturb();
            }
            forbidlist.Clear();

            while (!graph.IsSolutionValid()) {
                v = GetBestAdd();
                AddVertex(v);
                forbidlist.Add(v);
                IncreaseFreq();
            }
        }
    }
}

public class CC2FSPerturbationFactory : ISolverFactory {
    public ISolver Create(AdjLstWithSolGraph graph, CancellationToken token, string name) =>
        new CC2FSPerbutation(graph, name, token);
}
