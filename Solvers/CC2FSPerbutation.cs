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
public class PCC2FS : ISolver {

    // Theory
    protected bool[] ConfChange;
    protected uint[] freq;
    protected HashSet<int> forbidlist;

    // Graph and solution state
    protected AdjLstWithSolGraph graph;
    protected RetSol BestSolution;

    // Improvements / Implementation
    protected List<int>[] TwoLevelNeighborhood;
    protected IndexedMaxHeap AddHeap;
    protected IndexedMaxHeap RemoveHeap;
    protected BitArray InAddHeap;
    protected BitArray InRemoveHeap;
    protected uint[] _timestamp;
    protected uint _stepCount = 0;
    protected int[] _incFreqDelta;    // scratch array for batching IncreaseFreq heap updates
    protected List<int> _incFreqAffected; // vertices touched in current IncreaseFreq call
    protected IndexedMaxHeap SolNeighRemoveHeap; // keyed by |N(v) ∩ D| * 1000 / deg(v); RemoveMax yields highest-ratio solution-neighbor vertex in D
    protected BitArray InSolNeighHeap;
    protected int[] _solNeighCount; // raw |N(v) ∩ D| per vertex
    protected int[] _degree;         // static degree of each vertex

    // Helpers
    protected PlotterHelper plotterHelper;
    protected class PlotterHelper {
        public double[] SelectionCounts;
        public double[] SolNeighSelectionCounts;
        private String PrintName;
        private List<int> size_plot;
        private List<long> time_plot;

        private List<int> size_plot_ham;
        private List<long> time_plot_ham;
        public PlotterHelper(int size, String printname) {
            SelectionCounts = new double[size];
            SolNeighSelectionCounts = new double[size];
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
                plt.Title(PrintName + " - Iterations: " + size_plot.Count() + ". It/s: " + itps + ". Best: " + best);

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

            // SOLNEIGH HEAP SELECTION COUNT BAR PLOT
            {
                var plt = new Plot();
                SolNeighSelectionCounts.Sort();
                plt.Add.Bars(SolNeighSelectionCounts);
                plt.Title(PrintName + " - SolNeigh Heap Vertex Selection Frequency");
                plt.YLabel("Times Selected");
                var path = Path.GetFullPath(Path.Combine(projroot, "Output", PrintName + "_SolNeigh_Selection_Frequency.png"));
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


    public int GetRemovePerbCount() {
        return 10;//(int)(0.001 * graph.getSize());
    }
    public double GetPerturbationProbability() {
        return 0.8; ///0;
    }
    Random _random;
    protected void Perturb() {
        double fp = GetPerturbationProbability();
        int perturbCount = GetRemovePerbCount();

        for (int i = 0; i <= perturbCount; i++) {
            if (_random.NextDouble() < fp) {
                // Best Dscore: pick highest-scoring vertex from RemoveHeap
                if (RemoveHeap.Size() == 0) break;
                List<int> addAgainList = new List<int>();
                int v = -1;
                while (RemoveHeap.Size() > 0) {
                    int candidate = RemoveHeap.RemoveMax();
                    if (forbidlist.Contains(candidate)) {
                        addAgainList.Add(candidate);
                    } else {
                        v = candidate;
                        break;
                    }
                }
                foreach (int u in addAgainList) AddToRemoveHeap(u);
                if (v == -1) break;
                InRemoveHeap[v] = false;
                RemoveVertex(v);
                forbidlist.Add(v);
            } else {
                // Structured: pick highest Mscore from SolNeighRemoveHeap
                if (SolNeighRemoveHeap.Size() == 0) break;
                List<int> skipList = new List<int>();
                int v = -1;
                while (SolNeighRemoveHeap.Size() > 0) {
                    int candidate = SolNeighRemoveHeap.RemoveMax();
                    InSolNeighHeap[candidate] = false;
                    if (forbidlist.Contains(candidate)) {
                        skipList.Add(candidate);
                    } else {
                        v = candidate;
                        break;
                    }
                }
                foreach (int u in skipList) AddToSolNeighHeap(u);
                if (v == -1) break;
                plotterHelper.SolNeighSelectionCounts[v] += 1;
                if (InRemoveHeap[v]) { RemoveHeap.Remove(v); InRemoveHeap[v] = false; }
                RemoveVertex(v);
                forbidlist.Add(v);
            }
        }
    }

    public PCC2FS(AdjLstWithSolGraph graph, String printname, CancellationToken token) {
        int size = graph.getSize();
        this.graph = graph;

        // init stuff
        {
            _random = new Random(0);
            forbidlist = new HashSet<int>();
            AddHeap = new(size);
            RemoveHeap = new(size);
            InAddHeap = new(size, false);
            InRemoveHeap = new(size, false);
            SolNeighRemoveHeap = new(size);
            InSolNeighHeap = new(size, false);
            _solNeighCount = new int[size];
            _degree = new int[size];
            plotterHelper = new(size, printname);
            ConfChange = new bool[size];
            freq = new uint[size];
            // prof                = new Profiler();
            _incFreqDelta = new int[size];
            _incFreqAffected = new List<int>(size);
        }

        // Greedy initial solution

        graph = GreedyDecreaseKey.Solve(graph);
        BestSolution = graph.GetAsRetSol();

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
        for (int i = 0; i < graph.getSize(); i++) // CONFCHANGE INIT
            ConfChange[i] = true; // CC2-R1: all vertices initially have CC=true


        for (int i = 0; i < graph.getSize(); i++) // FREQ INIT
            freq[i] = 1;

        // PLOTTING
        _timestamp = new uint[size];
        PrintName = printname;

        // Precompute static degrees
        for (int i = 0; i < graph.getSize(); i++)
            foreach (int _ in graph.GetEdges(i)) _degree[i]++;

        // Populate heaps from initial solution
        foreach (int v in graph.GetNodes()) {
            if (graph.SolutionContains(v)) {
                AddToRemoveHeap(v);
                AddToSolNeighHeap(v);
            } else {
                AddToAddHeap(v);
            }
        }

        // START
        SolveLoop(token);
        // prof.Print();
        plotterHelper.Print(BestSolution.count);
    }
    public ISolution GetSolution() => BestSolution;

    protected void SolveLoop(CancellationToken token) {
        var sw = Stopwatch.StartNew();
        uint iterCount = 0;
        RetSol prevHam = graph.GetAsRetSol();
        while (!token.IsCancellationRequested) {
            if (iterCount % 10 == 0) { // CAN OPTIMIZE?
                if (iterCount % 1000 == 0) {
                    plotterHelper.AddHamDatapoint(Hamming(graph.VerticesInS, prevHam.Solution), sw.ElapsedMilliseconds);
                    prevHam = graph.GetAsRetSol();
                }
                plotterHelper.AddSOTDatapoint(graph.GetSolutionCount(), sw.ElapsedMilliseconds);
            }
            iterCount++;
            DoLoopLogic();
        }
    }

    protected virtual void DoLoopLogic() {
        if (graph.IsSolutionValid()) {
            if (graph.GetSolutionCount() < BestSolution.Count()) {
                BestSolution = graph.GetAsRetSol();
            }
            int v = GetBestRemove(forbidList: false);
            RemoveVertex(v);
        } else {
            //int v = GetBestRemove(forbidList: true);
            //RemoveVertex(v);
            forbidlist.Clear();
            Perturb();

            while (!graph.IsSolutionValid()) {
                int v = GetBestAdd();
                AddVertex(v);
                forbidlist.Add(v);
                IncreaseFreq();
            }
        }
    }

    protected int GetScore(int u) {
        if (!graph.SolutionContains(u)) {
            int sum = 0;
            foreach (int v in graph.GetEdges(u)) {
                if (graph.CoveredCount[v] == 0)
                    sum += (int)freq[v];
            }
            if (graph.CoveredCount[u] == 0)
                sum += (int)freq[u];
            //Console.WriteLine("Sum for " + u + " is: " + sum);
            return sum;
        } else {
            int sum = 0;
            foreach (int neigh in graph.GetEdges(u)) {
                if (graph.CoveredCount[neigh] == 1)
                    sum -= (int)freq[neigh];
            }
            if (graph.CoveredCount[u] == 1)
                sum -= (int)freq[u];
            //Console.WriteLine("Sum for " + u + " is: " + sum);
            return sum;
        }
    }

    protected virtual int GetBestAdd() {
        // long _t = Stopwatch.GetTimestamp(); prof.CallsGetBestAdd++;
        List<int> skipList = new List<int>();
        while (true) {
            int target = AddHeap.RemoveMax();
            InAddHeap[target] = false;
            if (graph.SolutionContains(target) || !ConfChange[target]) continue;
            if (forbidlist.Contains(target)) { skipList.Add(target); continue; }
            foreach (int u in skipList) AddToAddHeap(u);
            // prof.TicksGetBestAdd += Stopwatch.GetTimestamp() - _t;
            return target;
        }
    }

    protected virtual int GetBestRemove(bool forbidList) {
        // long _t = Stopwatch.GetTimestamp(); prof.CallsGetBestRemove++;
        List<int> addAgainList = new List<int>();
        while (true) {
            int target = RemoveHeap.RemoveMax();
            if (forbidList && forbidlist.Contains(target)) {
                addAgainList.Add(target);
            } else {
                foreach (int u in addAgainList) AddToRemoveHeap(u);
                InRemoveHeap[target] = false;
                // prof.TicksGetBestRemove += Stopwatch.GetTimestamp() - _t;
                return target;
            }
        }
    }

    protected void AddToAddHeap(int v) {
        InAddHeap[v] = true;
        AddHeap.Insert(v, GetScore(v), _timestamp[v]);
    }

    protected void AddToRemoveHeap(int v) {
        InRemoveHeap[v] = true;
        int score = GetScore(v);

        //if (score < 0) Console.WriteLine("Added " + v + " to removeHeap @ " + score);

        RemoveHeap.Insert(v, score, _timestamp[v]);
    }

    protected void AddToSolNeighHeap(int v) {
        InSolNeighHeap[v] = true;
        int msc = 0;
        foreach (int u in graph.GetEdges(v))
            if (graph.SolutionContains(u)) msc++;
        _solNeighCount[v] = msc;
        int deg = _degree[v];
        SolNeighRemoveHeap.Insert(v, deg > 0 ? msc * 1000 / deg : 0, _timestamp[v]);
    }

    protected void UpdateHeapScores(int v) {
        if (InAddHeap[v]) {
            int newScore = GetScore(v);
            AddHeap.UpdateKey(v, newScore);
        }
        if (InRemoveHeap[v]) {
            int newScore = GetScore(v);
            RemoveHeap.UpdateKey(v, newScore);
        }
    }

    protected void AdjustAddScore(int v, int delta) {
        if (InAddHeap[v]) AddHeap.AdjustKey(v, delta);
    }

    protected void AdjustRemoveScore(int v, int delta) {
        if (InRemoveHeap[v]) RemoveHeap.AdjustKey(v, delta);
    }

    // A vertex is in exactly one heap at a time — use this when the delta applies
    // regardless of which heap the vertex is in, avoiding a redundant second check.
    protected void AdjustScore(int v, int delta) {
        if (InAddHeap[v]) AddHeap.AdjustKey(v, delta);
        else if (InRemoveHeap[v]) RemoveHeap.AdjustKey(v, delta);
    }

    protected void SetCCTrue(int v) {
        // long _t = Stopwatch.GetTimestamp(); prof.CallsSetCCTrue++;
        if (ConfChange[v] == false) {
            ConfChange[v] = true;
            if (!graph.SolutionContains(v) && !InAddHeap[v]) {
                AddToAddHeap(v);
            }
            if (InRemoveHeap[v]) {
                RemoveHeap.UpdateKey(v, GetScore(v));
            }
        }
        // prof.TicksSetCCTrue += Stopwatch.GetTimestamp() - _t;
    }

    protected void AddVertex(int v) {
        // long _t = Stopwatch.GetTimestamp(); prof.CallsAddVertex++;
        _timestamp[v] = ++_stepCount;
        plotterHelper.SelectionCounts[v] += 1;
        graph.AddVertexToSol(v);

        // Update SolNeigh heap for v's neighbors (v just joined D)
        foreach (int u in graph.GetEdges(v)) {
            _solNeighCount[u]++;
            if (InSolNeighHeap[u])
                SolNeighRemoveHeap.UpdateKey(u, _degree[u] > 0 ? _solNeighCount[u] * 1000 / _degree[u] : 0);
        }

        // --- Neighbors of v ---
        foreach (int u in graph.GetEdges(v)) {
            int cu = graph.CoveredCount[u];
            if (cu == 1) {
                int fU = (int)freq[u];
                AdjustScore(u, -fU);
                foreach (int y in graph.GetEdges(u)) {
                    AdjustScore(y, -fU);
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
        int cv = graph.CoveredCount[v];
        if (cv == 1) {
            int fV = (int)freq[v];
            foreach (int y in graph.GetEdges(v)) {
                AdjustScore(y, -fV);
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
        if (InAddHeap[v]) { AddHeap.Remove(v); InAddHeap[v] = false; }
        AddToRemoveHeap(v);
        AddToSolNeighHeap(v);
        // prof.TicksAddVertex += Stopwatch.GetTimestamp() - _t;
    }

    protected void RemoveVertex(int v) {
        // long _t = Stopwatch.GetTimestamp(); prof.CallsRemoveVertex++;
        _timestamp[v] = ++_stepCount;
        plotterHelper.SelectionCounts[v] += 1;
        SolNeighRemoveHeap.Remove(v);
        InSolNeighHeap[v] = false;
        graph.RemoveVertexFromSol(v);
        ConfChange[v] = false;

        foreach (int u in graph.GetEdges(v)) {
            int cu = graph.CoveredCount[u];
            if (cu == 0) {
                int fU = (int)freq[u];
                AdjustScore(u, +fU);
                foreach (int y in graph.GetEdges(u)) {
                    AdjustScore(y, +fU);
                }
            } else if (cu == 1) {
                int fU = (int)freq[u];
                AdjustRemoveScore(u, -fU);
                foreach (int y in graph.GetEdges(u)) {
                    AdjustRemoveScore(y, -fU);
                }
            }
        }

        int cv = graph.CoveredCount[v];
        if (cv == 0) {
            int fV = (int)freq[v];
            foreach (int y in graph.GetEdges(v)) {
                AdjustScore(y, +fV);
            }
        } else if (cv == 1) {
            int fV = (int)freq[v];
            foreach (int y in graph.GetEdges(v)) {
                AdjustRemoveScore(y, -fV);
            }
        }

        // Update SolNeigh heap for v's neighbors (v just left D)
        foreach (int u in graph.GetEdges(v)) {
            _solNeighCount[u]--;
            if (InSolNeighHeap[u])
                SolNeighRemoveHeap.UpdateKey(u, _degree[u] > 0 ? _solNeighCount[u] * 1000 / _degree[u] : 0);
        }

        foreach (int u in TwoLevelNeighborhood[v])
            SetCCTrue(u);
        // prof.TicksRemoveVertex += Stopwatch.GetTimestamp() - _t;
    }

    protected void IncreaseFreq() {
        // long _t = Stopwatch.GetTimestamp(); prof.CallsIncreaseFreq++;

        // Increment freq for every uncovered vertex and accumulate the per-vertex
        // add-score delta in one pass.  score(u) increases by the number of uncovered
        // vertices in N[u]∪{u} whose freq just increased.  Batching avoids
        // O(|Uncovered|×deg) heap operations; each affected vertex gets one update.
        foreach (int v in graph.UncoveredVertices) {
            freq[v]++;
            if (_incFreqDelta[v] == 0) _incFreqAffected.Add(v);
            _incFreqDelta[v]++;
            foreach (int neigh in graph.GetEdges(v)) {
                if (_incFreqDelta[neigh] == 0) _incFreqAffected.Add(neigh);
                _incFreqDelta[neigh]++;
            }
        }

        foreach (int v in _incFreqAffected) {
            AdjustAddScore(v, _incFreqDelta[v]);
            _incFreqDelta[v] = 0;
        }
        _incFreqAffected.Clear();

        // prof.TicksIncreaseFreq += Stopwatch.GetTimestamp() - _t;
    }
}

public class PCC2FSFactory : ISolverFactory {
    public ISolver Create(AdjLstWithSolGraph graph, CancellationToken token, string name) {
        return new PCC2FS(graph, name, token);
    }
}
