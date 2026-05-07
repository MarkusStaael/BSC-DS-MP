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
public class CC2FS : ISolver {
    protected bool[] ConfChange;        // CC2 configuration change flags
    protected IGraph graph;
    protected SimpleSol CandidateSol;
    protected RetSol BestSolution;
    protected uint[] freq;
    protected HashSet<int> forbidlist;
    protected List<int>[] TwoLevelNeighborhood;
    protected IndexedMaxHeap AddHeap;
    protected IndexedMaxHeap RemoveHeap;
    protected BitArray InHeap;
    protected BitArray InRemoveHeap;
    protected uint[] _timestamp;
    protected uint _stepCount = 0;
    protected PlotterHelper plotterHelper;
    protected class PlotterHelper {
        public double[] SelectionCounts;
        private String PrintName;
        private List<int> size_plot;
        private List<long> time_plot;

        private List<int> size_plot_ham;
        private List<long> time_plot_ham;
        public PlotterHelper(int size, String printname) {
            SelectionCounts = new double[size];
            this.PrintName = printname;
            time_plot = new();
            size_plot = new();
            size_plot_ham = new();
            time_plot_ham = new();
        }

        public void AddHamDatapoint(int size, long time) {
            size_plot_ham.Add(size);
            time_plot_ham.Add(time);
        }

        public void AddSOTDatapoint(int size, long time) {
            size_plot.Add(size);
            time_plot.Add(time);
        }

        public void Print(int best) {
            string projroot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..");
            /// HAMMING DIST EVERY 1000

            {
                long[] ys = time_plot_ham.ToArray();
                int[] xs = size_plot_ham.ToArray();
                var plt = new Plot();
                plt.Add.Scatter(ys, xs);
                double itps = 60 * size_plot.Count / ((double)time_plot[time_plot.Count() - 1]);
                plt.Title(PrintName + " - Hamming distance over time");

                string path = Path.GetFullPath(Path.Combine(projroot, "Output", PrintName + "_hamming.png"));
                plt.SavePng(path, 2000, 700);
            }

            // Solution over time plot
            {
                long[] ys = time_plot.ToArray();
                int[] xs = size_plot.ToArray();
                var plt = new Plot();
                plt.Add.Scatter(ys, xs);
                double itps = 60 * size_plot.Count / ((double)time_plot[time_plot.Count() - 1]);
                plt.Title(PrintName+" - Iterations: " + size_plot.Count() + ". It/s: " + itps + ". Best: "+ best);

                string path = Path.GetFullPath(Path.Combine(projroot, "Output", PrintName + ".png"));
                plt.SavePng(path, 2000, 700);
            }

            // SELECTION COUNT BAR PLOT
            {
                var plt = new Plot();
                SelectionCounts.Sort();
                plt.Add.Bars(SelectionCounts);
                plt.Title(PrintName + " - Vertex Selection Frequency");
                plt.YLabel("Times Selected");
                var path = Path.GetFullPath(Path.Combine(projroot, "Output", PrintName + "_Selection_Frequency.png"));
                plt.SavePng(path, 2000, 700);
            }
        }
    }

    protected int Hamming(bool[] a, BitArray b) {
        int count = 0;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) count++;
        return count;
    }

    String PrintName;
    public CC2FS(IGraph graph,String printname) {
        PrintName = printname;
        this.graph = graph;
        forbidlist = new HashSet<int>();
        AddHeap = new(graph.getSize());
        RemoveHeap = new(graph.getSize());
        InHeap = new(graph.getSize(), false);
        InRemoveHeap = new(graph.getSize(), false);
        plotterHelper = new(graph.getSize(),printname);

    }

    public ISolution Solve(IGraph graph, CancellationToken? token) {
        // --- Build TwoLevelNeighborhood ---
        TwoLevelNeighborhood = new List<int>[graph.getSize()];
        {
            int n = graph.getSize();
            int[] mark = new int[n];
            int currentMark = 1;

            for (int v = 0; v < n; v++) {
                TwoLevelNeighborhood[v] = new List<int>();
                int stamp = currentMark++;

                mark[v] = stamp; // exclude self only

                foreach (int u in graph.GetEdges(v)) {
                    if (mark[u] != stamp) {
                        mark[u] = stamp;
                        TwoLevelNeighborhood[v].Add(u); // N1(v)
                    }
                    foreach (int w in graph.GetEdges(u)) {
                        if (mark[w] != stamp) {
                            mark[w] = stamp;
                            TwoLevelNeighborhood[v].Add(w); // N2(v)
                        }
                    }
                }
            }
        }



        // --- Initialise state ---
        ConfChange = new bool[graph.getSize()];
        for(int i = 0; i < graph.getSize(); i++)
            ConfChange[i] = true; // CC2-R1: all vertices initially have CC=true

        freq = new uint[graph.getSize()];
        for (int i = 0; i < graph.getSize(); i++) freq[i] = 1;
        _timestamp = new uint[graph.getSize()];

        {
            ISolution init = new GreedyDecreaseKey().Solve(graph, null);
            CandidateSol = new SimpleSol(graph);
            CandidateSol.InitFromSol(init);
        }
        BestSolution = CandidateSol.GetAsRetSol();

        // Populate heaps from initial solution
        foreach (int v in graph.GetNodes()) {
            if (CandidateSol.SolutionContains(v)) {
                AddToRemoveHeap(v);
            } else {
                AddToAddHeap(v);
            }
        }

        SolveLoop(token);

        plotterHelper.Print(BestSolution.count);

        return BestSolution;
    }

    protected void SolveLoop(CancellationToken? token) {
        var sw = Stopwatch.StartNew();
        if (token == null) throw new Exception("CC2FS needs a CancellationToken");

        uint iterCount = 0;
        RetSol prevHam = CandidateSol.GetAsRetSol();
        while (!((CancellationToken)token).IsCancellationRequested) {
            if (iterCount % 10 == 0) { // CAN OPTIMIZE?
                if(iterCount % 1000 == 0) {
                    plotterHelper.AddHamDatapoint(Hamming(CandidateSol.VerticesInS, prevHam.Solution), sw.ElapsedMilliseconds);
                    prevHam = CandidateSol.GetAsRetSol();
                }
                plotterHelper.AddSOTDatapoint(CandidateSol.GetSolutionCount(), sw.ElapsedMilliseconds);
            }
            iterCount++;
            DoLoopLogic();


        }
    }

    protected virtual void DoLoopLogic() {
        if (CandidateSol.IsSolutionValid()) {
            if (CandidateSol.GetSolutionCount() < BestSolution.Count()) {
                BestSolution = CandidateSol.GetAsRetSol();
            }
            int v = GetBestRemove(forbidList: false);
            RemoveVertex(v);
        } else {
            int v = GetBestRemove(forbidList: true);
            RemoveVertex(v);
            forbidlist.Clear();

            while (!CandidateSol.IsSolutionValid()) {
                v = GetBestAdd();
                AddVertex(v);
                forbidlist.Add(v);
                IncreaseFreq();
            }
        }
    }

    protected int ActualBest() {
        //TODO: IMPLEMENT OLDEST ONE TIEBREAKER
        int highest = int.MinValue;
        int reference = -1;
        foreach (var node in graph.GetNodes()) {
            if (CandidateSol.SolutionContains(node)) continue;
            if (ConfChange[node] == false) continue;
            var score = GetScore(node);
            if (score > highest) {
                highest = score;
                reference = node;
            }
        }
        return reference;
    }

    protected int GetScore(int u) {
        if (!CandidateSol.SolutionContains(u)) {
            int sum = 0;
            foreach (int v in graph.GetEdges(u)) {
                if (!CandidateSol.IsCovered(v))
                    sum += (int)freq[v];
            }
            if (!CandidateSol.IsCovered(u))
                sum += (int)freq[u];
            //Console.WriteLine("Sum for " + u + " is: " + sum);
            return sum;
        } else {
            int sum = 0;
            foreach (int neigh in graph.GetEdges(u)) {
                if (CandidateSol.Covered(neigh) == 1)
                    sum -= (int)freq[neigh];
            }
            if (CandidateSol.Covered(u) == 1)
                sum -= (int)freq[u];
            //Console.WriteLine("Sum for " + u + " is: " + sum);
            return sum;
        }
    }

    protected virtual int GetBestAdd() {
        while (true) {
            int target = AddHeap.RemoveMax();
            InHeap[target] = false;
            if (CandidateSol.SolutionContains(target)) continue;
            if (!ConfChange[target]) continue;
            //Console.WriteLine("ADD: " + target + "@ " + GetScore(target));
            //if(target == 589020) {
            //    //int best = ActualBest();
            //    //Console.WriteLine(best + " @ " + GetScore(best));
            //
            //    //foreach (int neigh in graph.GetEdges(2)) {
            //    //    Console.WriteLine("NEIGH: " + neigh + ". IS IN?: " + CandidateSol.SolutionContains(neigh) + " CC2: " + ConfChange[neigh]);
            //    //}
            //
            //    //Console.WriteLine("BEEOP");
            //}

            return target;
        }
    }

    protected virtual int GetBestRemove(bool forbidList) {

        List<int> addAgainList = new List<int>();

        while(true) {

            int target = RemoveHeap.RemoveMax();

            if (forbidList && forbidlist.Contains(target)) {
                addAgainList.Add(target);
            } else {
                foreach(int u in addAgainList) {
                    AddToRemoveHeap(u);
                }
                InRemoveHeap[target] = false;
                //Console.WriteLine("REM: " + target + "@ "+GetScore(target));
                return target; 

            }
        }
    }

    protected void AddToAddHeap(int v) {
        InHeap[v] = true;
        AddHeap.Insert(v, GetScore(v), _timestamp[v]);
    }

    protected void AddToRemoveHeap(int v) {
        InRemoveHeap[v] = true;
        int score = GetScore(v);

        //if (score < 0) Console.WriteLine("Added " + v + " to removeHeap @ " + score);

        RemoveHeap.Insert(v, score, _timestamp[v]);
    }

    protected void UpdateHeapScores(int v) {
        if (InHeap[v]) {
            int newScore = GetScore(v);
            AddHeap.UpdateKey(v, newScore);
        }
        if (InRemoveHeap[v]) {
            int newScore = GetScore(v);
            RemoveHeap.UpdateKey(v, newScore);
        }
    }

    protected void AdjustAddScore(int v, int delta) {
        if (InHeap[v]) AddHeap.AdjustKey(v, delta);
    }

    protected void AdjustRemoveScore(int v, int delta) {
        if (InRemoveHeap[v]) RemoveHeap.AdjustKey(v, delta);
    }

    protected void SetCCTrue(int v) {
        if (ConfChange[v] == false) {
            ConfChange[v] = true;
            if (!CandidateSol.SolutionContains(v) && !InHeap[v]) {
                AddToAddHeap(v);
            }
            if (InRemoveHeap[v]) {
                RemoveHeap.UpdateKey(v, GetScore(v));
            }
        }
    }

    protected void AddVertex(int v) {
        _timestamp[v] = ++_stepCount;
        plotterHelper.SelectionCounts[v] += 1;
        CandidateSol.AddVertex(v);


        // --- Neighbors of v ---
        foreach (int u in graph.GetEdges(v)) {
            int cu = CandidateSol.Covered(u);
            if (cu == 1) {                              
                int fU = (int)freq[u];
                AdjustAddScore(u, -fU);
                AdjustRemoveScore(u, -fU);
                foreach (int y in graph.GetEdges(u)) {
                    AdjustAddScore(y, -fU);
                    AdjustRemoveScore(y, -fU);
                }
            } else if (cu == 2) {                       
                int fU = (int)freq[u];
                AdjustRemoveScore(u, +fU);
                foreach (int y in graph.GetEdges(u)) {
                    AdjustRemoveScore(y, +fU);
                }
            }
        }

        // --- v itself ---
        int cv = CandidateSol.Covered(v);
        if (cv == 1) {                                 
            int fV = (int)freq[v];
            foreach (int y in graph.GetEdges(v)) {
                AdjustAddScore(y, -fV);
                AdjustRemoveScore(y, -fV);
            }
        } else if (cv == 2) {                           
            int fV = (int)freq[v];
            foreach (int y in graph.GetEdges(v)) {
                AdjustRemoveScore(y, +fV);
            }
        }

        foreach (int u in TwoLevelNeighborhood[v])
            SetCCTrue(u);

        // Ensure v is out of AddHeap, then add to RemoveHeap
        if (InHeap[v]) { AddHeap.Remove(v); InHeap[v] = false; }
        AddToRemoveHeap(v);
    }

    protected void RemoveVertex(int v) {
        _timestamp[v] = ++_stepCount;
        plotterHelper.SelectionCounts[v] += 1;
        CandidateSol.RemoveVertex(v);
        ConfChange[v] = false;

        foreach (int u in graph.GetEdges(v)) {
            int cu = CandidateSol.Covered(u);
            if (cu == 0) {                            
                int fU = (int)freq[u];
                AdjustAddScore(u, +fU);
                AdjustRemoveScore(u, +fU);
                foreach (int y in graph.GetEdges(u)) {
                    AdjustAddScore(y, +fU);
                    AdjustRemoveScore(y, +fU);
                }
            } else if (cu == 1) {                      
                int fU = (int)freq[u];
                AdjustRemoveScore(u, -fU);
                foreach (int y in graph.GetEdges(u)) {
                    AdjustRemoveScore(y, -fU);
                }
            }
        }

        int cv = CandidateSol.Covered(v);
        if (cv == 0) {                                  
            int fV = (int)freq[v];
            foreach (int y in graph.GetEdges(v)) {
                AdjustAddScore(y, +fV);
                AdjustRemoveScore(y, +fV);
            }
        } else if (cv == 1) {                          
            int fV = (int)freq[v];
            foreach (int y in graph.GetEdges(v)) {
                AdjustRemoveScore(y, -fV);
            }
        }

        foreach (int u in TwoLevelNeighborhood[v])
            SetCCTrue(u);
    }

    protected void IncreaseFreq() {
        foreach (int v in CandidateSol.uncoveredVertices) {
            freq[v] += 1;
        }

        foreach (int v in CandidateSol.uncoveredVertices) {
            AdjustAddScore(v, +1);
            foreach (int neigh in graph.GetEdges(v)) {
                AdjustAddScore(neigh, +1);
            }
        }
    }
}
