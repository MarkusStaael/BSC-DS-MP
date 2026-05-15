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

    // Reusable skip lists — avoids allocation in the hot loop
    protected readonly List<int> _reuseSkipList = new();
    protected readonly List<int> _reuseAddAgainList = new();

    // Batched score-delta scratch arrays for AddVertex/RemoveVertex
    protected int[] _adjAnyDelta;     // accumulated delta for AdjustScore (either heap)
    protected int[] _adjRemDelta;     // accumulated delta for AdjustRemoveScore only
    protected bool[] _adjInAffected;  // mark: vertex is queued in _adjAffected
    protected List<int> _adjAffected; // vertices that received a delta this step

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

    protected Func<int> _removePermCount;
    protected Func<double> _perturbProbability;

    public int GetRemovePerbCount() => _removePermCount();
    public double GetPerturbationProbability() => _perturbProbability();


    Random _random;
    protected void Perturb() {
        double fp = GetPerturbationProbability();
        int perturbCount = GetRemovePerbCount();

        for (int i = 0; i <= perturbCount; i++) {
            if (_random.NextDouble() < fp) {
                // Best Dscore: pick highest-scoring vertex from RemoveHeap
                if (RemoveHeap.Size() == 0) break;
                int v = -1;
                while (RemoveHeap.Size() > 0) {
                    int candidate = RemoveHeap.RemoveMax();
                    if (forbidlist.Contains(candidate)) {
                        _reuseAddAgainList.Add(candidate);
                    } else {
                        v = candidate;
                        break;
                    }
                }
                foreach (int u in _reuseAddAgainList) AddToRemoveHeap(u);
                _reuseAddAgainList.Clear();
                if (v == -1) break;
                InRemoveHeap[v] = false;
                RemoveVertex(v);
                forbidlist.Add(v);
            } else {
                // Structured: pick highest Mscore from SolNeighRemoveHeap
                if (SolNeighRemoveHeap.Size() == 0) break;
                int v = -1;
                while (SolNeighRemoveHeap.Size() > 0) {
                    int candidate = SolNeighRemoveHeap.RemoveMax();
                    InSolNeighHeap[candidate] = false;
                    if (forbidlist.Contains(candidate)) {
                        _reuseSkipList.Add(candidate);
                    } else {
                        v = candidate;
                        break;
                    }
                }
                foreach (int u in _reuseSkipList) AddToSolNeighHeap(u);
                _reuseSkipList.Clear();
                if (v == -1) break;
                //plotterHelper.SolNeighSelectionCounts[v] += 1;
                if (InRemoveHeap[v]) { RemoveHeap.Remove(v); InRemoveHeap[v] = false; }
                RemoveVertex(v);
                forbidlist.Add(v);
            }
        }
    }

    public PCC2FS(AdjLstWithSolGraph graph, String printname, CancellationToken token, Func<int> removePermCount, Func<double> perturbProbability) {
        _removePermCount = removePermCount;
        _perturbProbability = perturbProbability;
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
            _adjAnyDelta = new int[size];
            _adjRemDelta = new int[size];
            _adjInAffected = new bool[size];
            _adjAffected = new List<int>(size);
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

        // Precompute initial _solNeighCount (required since AddToSolNeighHeap no longer scans)
        for (int i = 0; i < graph.getSize(); i++)
            foreach (int u in graph.Edges[i])
                if (graph.SolutionContains(u)) _solNeighCount[i]++;

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
        //plotterHelper.Print(BestSolution.count);
    }
    public ISolution GetSolution() => BestSolution;

    protected void SolveLoop(CancellationToken token) {
        //var sw = Stopwatch.StartNew();
        //uint iterCount = 0;
        //RetSol prevHam = graph.GetAsRetSol();
        while (!token.IsCancellationRequested) {
            /*if (iterCount % 10 == 0) { // CAN OPTIMIZE?
                if (iterCount % 1000 == 0) {
                    plotterHelper.AddHamDatapoint(Hamming(graph.VerticesInS, prevHam.Solution), sw.ElapsedMilliseconds);
                    prevHam = graph.GetAsRetSol();
                }
                plotterHelper.AddSOTDatapoint(graph.GetSolutionCount(), sw.ElapsedMilliseconds);
            }
            iterCount++;*/
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
        while (true) {
            int target = AddHeap.RemoveMax();
            InAddHeap[target] = false;
            if (graph.SolutionContains(target) || !ConfChange[target]) continue;
            if (forbidlist.Contains(target)) { _reuseSkipList.Add(target); continue; }
            foreach (int u in _reuseSkipList) AddToAddHeap(u);
            _reuseSkipList.Clear();
            // prof.TicksGetBestAdd += Stopwatch.GetTimestamp() - _t;
            return target;
        }
    }

    protected virtual int GetBestRemove(bool forbidList) {
        // long _t = Stopwatch.GetTimestamp(); prof.CallsGetBestRemove++;
        while (true) {
            int target = RemoveHeap.RemoveMax();
            if (forbidList && forbidlist.Contains(target)) {
                _reuseAddAgainList.Add(target);
            } else {
                foreach (int u in _reuseAddAgainList) AddToRemoveHeap(u);
                _reuseAddAgainList.Clear();
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
        int deg = _degree[v];
        SolNeighRemoveHeap.Insert(v, deg > 0 ? _solNeighCount[v] * 1000 / deg : 0, _timestamp[v]);
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

    // Batch-delta helpers: accumulate per-vertex and flush once with FlushAdjust().
    // Reduces O(appearances) heap ops to O(1) per vertex per step.
    protected void AccAny(int v, int delta) {
        if (!_adjInAffected[v]) { _adjInAffected[v] = true; _adjAffected.Add(v); }
        _adjAnyDelta[v] += delta;
    }
    protected void AccRem(int v, int delta) {
        if (!_adjInAffected[v]) { _adjInAffected[v] = true; _adjAffected.Add(v); }
        _adjRemDelta[v] += delta;
    }
    protected void FlushAdjust() {
        foreach (int v in _adjAffected) {
            if (InAddHeap[v]) {
                if (_adjAnyDelta[v] != 0) AddHeap.AdjustKey(v, _adjAnyDelta[v]);
            } else if (InRemoveHeap[v]) {
                int total = _adjAnyDelta[v] + _adjRemDelta[v];
                if (total != 0) RemoveHeap.AdjustKey(v, total);
            }
            _adjAnyDelta[v] = 0;
            _adjRemDelta[v] = 0;
            _adjInAffected[v] = false;
        }
        _adjAffected.Clear();
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
        //plotterHelper.SelectionCounts[v] += 1;
        graph.AddVertexToSol(v);

        // Single pass over N(v): update SolNeigh heap + accumulate score deltas
        List<int> edgesV = graph.Edges[v];
        int degV = edgesV.Count;
        for (int i = 0; i < degV; i++) {
            int u = edgesV[i];
            // SolNeigh: v just joined D
            _solNeighCount[u]++;
            if (InSolNeighHeap[u])
                SolNeighRemoveHeap.UpdateKey(u, _degree[u] > 0 ? _solNeighCount[u] * 1000 / _degree[u] : 0);

            int cu = graph.CoveredCount[u];
            if (cu == 1) {
                int fU = (int)freq[u];
                AccAny(u, -fU);
                List<int> edgesU = graph.Edges[u];
                int degU = edgesU.Count;
                for (int j = 0; j < degU; j++) AccAny(edgesU[j], -fU);
            } else if (cu == 2) {
                int fU = (int)freq[u];
                AccRem(u, +fU);
                List<int> edgesU = graph.Edges[u];
                int degU = edgesU.Count;
                for (int j = 0; j < degU; j++) AccRem(edgesU[j], +fU);
            }
        }

        // --- v itself ---
        int cv = graph.CoveredCount[v];
        if (cv == 1) {
            int fV = (int)freq[v];
            for (int i = 0; i < degV; i++) AccAny(edgesV[i], -fV);
        } else if (cv == 2) {
            int fV = (int)freq[v];
            for (int i = 0; i < degV; i++) AccRem(edgesV[i], +fV);
        }

        FlushAdjust();

        List<int> twoLvl = TwoLevelNeighborhood[v];
        int twoLvlCnt = twoLvl.Count;
        for (int i = 0; i < twoLvlCnt; i++) SetCCTrue(twoLvl[i]);

        // Ensure v is out of AddHeap, then add to RemoveHeap
        if (InAddHeap[v]) { AddHeap.Remove(v); InAddHeap[v] = false; }
        AddToRemoveHeap(v);
        AddToSolNeighHeap(v);
        // prof.TicksAddVertex += Stopwatch.GetTimestamp() - _t;
    }

    protected void RemoveVertex(int v) {
        // long _t = Stopwatch.GetTimestamp(); prof.CallsRemoveVertex++;
        _timestamp[v] = ++_stepCount;
        //plotterHelper.SelectionCounts[v] += 1;
        SolNeighRemoveHeap.Remove(v);
        InSolNeighHeap[v] = false;
        graph.RemoveVertexFromSol(v);
        ConfChange[v] = false;

        // Single pass over N(v): accumulate score deltas + update SolNeigh heap
        List<int> edgesV = graph.Edges[v];
        int degV = edgesV.Count;
        for (int i = 0; i < degV; i++) {
            int u = edgesV[i];
            int cu = graph.CoveredCount[u];
            if (cu == 0) {
                int fU = (int)freq[u];
                AccAny(u, +fU);
                List<int> edgesU = graph.Edges[u];
                int degU = edgesU.Count;
                for (int j = 0; j < degU; j++) AccAny(edgesU[j], +fU);
            } else if (cu == 1) {
                int fU = (int)freq[u];
                AccRem(u, -fU);
                List<int> edgesU = graph.Edges[u];
                int degU = edgesU.Count;
                for (int j = 0; j < degU; j++) AccRem(edgesU[j], -fU);
            }

            // SolNeigh: v just left D
            _solNeighCount[u]--;
            if (InSolNeighHeap[u])
                SolNeighRemoveHeap.UpdateKey(u, _degree[u] > 0 ? _solNeighCount[u] * 1000 / _degree[u] : 0);
        }

        int cv = graph.CoveredCount[v];
        if (cv == 0) {
            int fV = (int)freq[v];
            for (int i = 0; i < degV; i++) AccAny(edgesV[i], +fV);
        } else if (cv == 1) {
            int fV = (int)freq[v];
            for (int i = 0; i < degV; i++) AccRem(edgesV[i], -fV);
        }

        FlushAdjust();

        List<int> twoLvl = TwoLevelNeighborhood[v];
        int twoLvlCnt = twoLvl.Count;
        for (int i = 0; i < twoLvlCnt; i++) SetCCTrue(twoLvl[i]);
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
            List<int> edges = graph.Edges[v];
            int deg = edges.Count;
            for (int i = 0; i < deg; i++) {
                int neigh = edges[i];
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
    private readonly Func<AdjLstWithSolGraph, int> _removePermCount;
    private readonly Func<double> _perturbProbability;

    public PCC2FSFactory(Func<AdjLstWithSolGraph, int> removePermCount, Func<double> perturbProbability) {
        _removePermCount = removePermCount;
        _perturbProbability = perturbProbability;
    }

    public ISolver Create(AdjLstWithSolGraph graph, CancellationToken token, string name) {
        return new PCC2FS(graph, name, token,
            removePermCount: () => _removePermCount(graph),
            perturbProbability: _perturbProbability);
    }
}
