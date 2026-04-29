using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using BSC_DS_MP.Util;
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
    protected class SimpleSol {
        public BitArray VerticesInS;
        public HashSet<int> uncoveredVertices;
        public int[] coveredCount;
        public int coveredSum;
        private int SolutionCount;
        IGraph graph;

        public SimpleSol(IGraph graph) {
            VerticesInS = new(graph.getSize(),false);
            coveredCount = new int[graph.getSize()];
            uncoveredVertices = new();
            this.graph = graph;
            coveredSum = 0;
        }

        public void InitFromSol(ISolution sol) {
            foreach (int i in sol.GetEnumerator()) {
                AddVertex(i);
            }
            foreach (int i in graph.GetNodes()) {
                if (!IsCovered(i)) {
                    uncoveredVertices.Add(i);
                }
            }
        }

        private bool IsInS(int v) {
            return VerticesInS[v] == true;
        }
        private void AddToS(int v) {
            VerticesInS.Set(v, true);
        }
        private void RemoveFromS(int v) {
            VerticesInS.Set(v, false);
        }

        public void AddVertex(int v) {
            if (IsInS(v)) throw new Exception("DOUBLE ADD: vertex " + v + " already in solution, coveredCount=" + coveredCount[v]);
            SolutionCount++;
            AddToS(v);
            coveredCount[v] += 1;
            if (coveredCount[v] == 1) {
                coveredSum += 1;
                uncoveredVertices.Remove(v);
            }

            foreach (int neighbor in graph.GetEdges(v)) {
                coveredCount[neighbor] += 1;
                if (coveredCount[neighbor] == 1) {
                    coveredSum += 1;
                    uncoveredVertices.Remove(neighbor);
                }
            }
        }
        public void RemoveVertex(int v) {
            if (!IsInS(v)) throw new Exception("Vertex not in solution");
            SolutionCount--;
            RemoveFromS(v);
            coveredCount[v] -= 1;
            if (coveredCount[v] < 0) throw new Exception("NEGATIVE COVER: vertex " + v + " coveredCount=" + coveredCount[v]);
            if (coveredCount[v] == 0) {
                coveredSum -= 1;
                uncoveredVertices.Add(v);
            }

            foreach (int neighbor in graph.GetEdges(v)) {
                coveredCount[neighbor] -= 1;
                if (coveredCount[neighbor] < 0) throw new Exception("NEGATIVE COVER: neighbor " + neighbor + " of " + v + " coveredCount=" + coveredCount[neighbor]);
                if (coveredCount[neighbor] == 0) {
                    coveredSum -= 1;
                    uncoveredVertices.Add(neighbor);
                }
            }
        }
        public int GetSolutionCount() {
            return SolutionCount;
        }
        public bool IsSolutionValid() {
            return coveredSum == graph.getSize();
        }
        public bool SolutionContains(int v) {
            return IsInS(v);
        }
        public int GetCoveredSum() {
            return coveredSum;
        }
        public int Covered(int v) {
            return coveredCount[v];
        }
        public bool IsCovered(int v) {
            return coveredCount[v] > 0;
        }
        public SimpleSol Clone() {
            var ret = new SimpleSol(graph);

            ret.VerticesInS = new(VerticesInS);
            ret.uncoveredVertices = new HashSet<int>(uncoveredVertices);
            ret.coveredSum = coveredSum;
            ret.coveredCount = (int[])coveredCount.Clone();

            return ret;
        }
        public RetSol GetAsRetSol() {
            var ret = new RetSol(graph.getSize());
            ret.Solution = new BitArray(VerticesInS);
            ret.count = SolutionCount;
            return ret;
        }
    }
    protected class RetSol : ISolution {
        public BitArray Solution;
        public int count;
        public RetSol(int size) {
            Solution = new BitArray(size);
        }
        public void AddVertex(int v) {
        }

        public int Count() {
            return count;
        }

        public IEnumerable<int> GetEnumerator() {
            for (int i = 0; i < Solution.Length; i++) {
                if (Solution.Get(i)) yield return i;
            }
        }
    }
    protected BitArray ConfChange;        // CC2 configuration change flags
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

    protected int Hamming(BitArray a, BitArray b) {
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
        ConfChange = new(graph.getSize());
        ConfChange.SetAll(true); // CC2-R1
        freq = new uint[graph.getSize()];
        for (int i = 0; i < graph.getSize(); i++) freq[i] = 1;

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
        AddHeap.Insert(v, GetScore(v));
    }

    protected void AddToRemoveHeap(int v) {
        InRemoveHeap[v] = true;
        int score = GetScore(v);

        //if (score < 0) Console.WriteLine("Added " + v + " to removeHeap @ " + score);

        RemoveHeap.Insert(v, score);
    }

    protected void UpdateHeapScores(int v) {
        if (InHeap[v]) {
            int newScore = GetScore(v);
            AddHeap.UpdateKey(v, newScore);
            //if(newScore > 0)
            //    Console.WriteLine("Updated " + v + " to " + newScore);

        }
        if (InRemoveHeap[v]) {

            int newScore = GetScore(v);
            RemoveHeap.UpdateKey(v, newScore);

            //if (newScore > 0)
            //    Console.WriteLine("Updated " + v + " to " + newScore);
        }
    }

    protected void SetCCTrue(int v) {
        if (ConfChange[v] == false) {
            ConfChange.Set(v, true);
            if (!CandidateSol.SolutionContains(v) && !InHeap[v]) {
                AddToAddHeap(v);
            } 
            //if (InRemoveHeap[v]) { ADDED THIS WHEN I ADDED HEAPS, I DON'T THINK IT SHOULD BE HERE BUT WILL LEAVE IT
            //    RemoveHeap.UpdateKey(v, GetScore(v));
            //}
        }
    }

    protected void AddVertex(int v) {
        plotterHelper.SelectionCounts[v] += 1;
        CandidateSol.AddVertex(v);

        foreach (int u in TwoLevelNeighborhood[v])
            SetCCTrue(u);

        // Update v's direct neighbors and propagate on coverage transitions
        foreach (int u in graph.GetEdges(v)) {
            UpdateHeapScores(u);
            if (CandidateSol.Covered(u) == 1) {           // 0→1: neighbors' add-scores decrease
                foreach (int y in graph.GetEdges(u))
                    UpdateHeapScores(y);
            }
            if (CandidateSol.Covered(u) == 2) {           // 1→2: adjacent solution vertices become more redundant
                foreach (int y in graph.GetEdges(u))
                    UpdateHeapScores(y);
            }
        }
        // v itself: propagate on both transitions
        if (CandidateSol.Covered(v) == 1 || CandidateSol.Covered(v) == 2) {
            foreach (int y in graph.GetEdges(v))
                UpdateHeapScores(y);
        }

        // Ensure v is out of AddHeap, then add to RemoveHeap
        if (InHeap[v]) { AddHeap.Remove(v); InHeap[v] = false; }
        AddToRemoveHeap(v);

        
    }

    protected void RemoveVertex(int v) {
        plotterHelper.SelectionCounts[v] += 1;
        CandidateSol.RemoveVertex(v);
        ConfChange.Set(v, false);

        foreach (int u in TwoLevelNeighborhood[v])
            SetCCTrue(u);

        // Ensure v is out of RemoveHeap (usually already popped by GetBestRemove)
        //if (InRemoveHeap[v]) { RemoveHeap.Remove(v); InRemoveHeap[v] = false; }
        // v has CC=false so do NOT add to AddHeap (per paper's ConfChange rule)

        // Update neighbors and propagate on coverage transitions
        foreach (int u in graph.GetEdges(v)) {
            UpdateHeapScores(u);
            if (CandidateSol.Covered(u) == 0) {           // 1→0: neighbors' add-scores increase
                foreach (int y in graph.GetEdges(u))
                    UpdateHeapScores(y);
            }
            if (CandidateSol.Covered(u) == 1) {           // 2→1: adjacent solution vertices become less redundant
                foreach (int y in graph.GetEdges(u))
                    UpdateHeapScores(y);
            }
        }
        // v itself: propagate on both transitions
        //if (CandidateSol.Covered(v) == 0 || CandidateSol.Covered(v) == 1) {
        //    UpdateHeapScores(v);
        //    foreach (int y in graph.GetEdges(v))
        //        UpdateHeapScores(y);
        //}
    }

    protected void IncreaseFreq() {
        foreach (int v in CandidateSol.uncoveredVertices) {
            freq[v] += 1;

        }

        foreach (int v in CandidateSol.uncoveredVertices) {
            UpdateHeapScores(v);
            foreach (int neigh in graph.GetEdges(v)) {
                UpdateHeapScores(neigh);
            }
        }
    }
}
