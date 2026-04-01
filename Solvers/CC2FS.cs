using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using BSC_DS_MP.Util;
using ScottPlot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace BSC_DS_MP.Solvers;
// https://jair.org/index.php/jair/article/view/11044/26218
internal class CC2FS : ISolver {
    internal class SimpleSol {
        public HashSet<int> vertices;
        public HashSet<int> uncoveredVertices;
        public int[] coveredCount;
        public int coveredSum;
        IGraph graph;

        public SimpleSol(IGraph graph) {
            vertices = new HashSet<int>();
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
        public void AddVertex(int v) {
            if (vertices.Contains(v)) throw new Exception("DOUBLE ADD: vertex " + v + " already in solution, coveredCount=" + coveredCount[v]);

            vertices.Add(v);
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
            if (!vertices.Contains(v)) throw new Exception("Vertex not in solution");
            vertices.Remove(v);
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
            return vertices.Count;
        }
        public bool IsSolutionValid() {
            return coveredSum == graph.getSize();
        }
        public IEnumerable<int> GetEnumerator() {
            return vertices;
        }
        public bool SolutionContains(int v) {
            return vertices.Contains(v);
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

            ret.vertices = new HashSet<int>(vertices);
            ret.uncoveredVertices = new HashSet<int>(uncoveredVertices);
            ret.coveredSum = coveredSum;
            ret.coveredCount = (int[])coveredCount.Clone();

            return ret;
        }
    }

    internal class RetSol : ISolution {
        public BitArray Solution;
        private int count;
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

    BitArray ConfChange;        // CC2 configuration change flags
    IGraph graph;
    SimpleSol CandidateSol;
    uint[] freq;
    HashSet<int> forbidlist;
    List<int>[] TwoLevelNeighborhood;


    IndexedMaxHeap AddHeap;
    IndexedMaxHeap RemoveHeap;
    BitArray InHeap;
    BitArray InRemoveHeap;

    StatsGetter statsGetter;
    internal class StatsGetter {
        public double[] SelectionCounts;

        public StatsGetter(int size) {
            SelectionCounts = new double[size];
        }

        public void Print() {
            var plt = new Plot();

            // Bar chart
            var bar = new Bar();

            SelectionCounts.Sort();

            plt.Add.Bars(SelectionCounts);


            plt.Title("Vertex Selection Frequency");
            plt.YLabel("Times Selected");
            plt.XLabel("Vertex ID");
            plt.SavePng("CCFS_Selection_Frequency.png", 2000, 700);
        }
    }

    public CC2FS(IGraph graph) {
        this.graph = graph;
        forbidlist = new HashSet<int>();
        AddHeap = new(graph.getSize());
        RemoveHeap = new(graph.getSize());
        InHeap = new(graph.getSize(), false);
        InRemoveHeap = new(graph.getSize(), false);
        statsGetter = new(graph.getSize());

    }

    public ISolution Solve(IGraph graph, CancellationToken? token) {

        var size_plot = new List<int>();
        var time_plot = new List<long>();

        if (token == null) throw new Exception("CC2FS needs a CancellationToken");

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

        SimpleSol bestSolution;
        {
            ISolution init = new GreedyDecreaseKey().Solve(graph.CloneInto(new AdjSetLstGraphFactory()), null);
            bestSolution = new SimpleSol(graph);
            bestSolution.InitFromSol(init);
        }
        CandidateSol = bestSolution.Clone();

        // Populate heaps from initial solution
        foreach (int v in graph.GetNodes()) {
            if (CandidateSol.SolutionContains(v)) {
                AddToRemoveHeap(v);
            } else {
                AddToAddHeap(v);
            }
        }

        var sw = Stopwatch.StartNew();

        int iterCount = 0;
        while (!((CancellationToken)token).IsCancellationRequested) {
            if (iterCount % 10 == 0) {
                size_plot.Add(CandidateSol.GetSolutionCount());
                time_plot.Add(sw.ElapsedMilliseconds);
            }
            iterCount++;

            if (CandidateSol.IsSolutionValid()) {
                if (CandidateSol.GetSolutionCount() < bestSolution.GetSolutionCount()) {
                    bestSolution = CandidateSol.Clone();
                }
                int v = GetBestRemove(forbidList: false);
                RemoveVertex(v);
            } else {
                int v = GetBestRemove(forbidList: true);
                RemoveVertex(v);
                forbidlist.Clear();

                while (!CandidateSol.IsSolutionValid() && !((CancellationToken)token).IsCancellationRequested) {
                    v = GetBestAdd();
                    AddVertex(v);
                    forbidlist.Add(v);
                    IncreaseFreq();
                }
            }
        }

        var ret = new BitArraySolution(graph.getSize());
        foreach (int i in bestSolution.GetEnumerator()) {
            ret.AddVertex(i);
        }

        long[] ys = time_plot.ToArray();
        int[] xs = size_plot.ToArray();
        var plt = new Plot();
        plt.Add.Scatter(ys, xs);
        double itps = 60 * size_plot.Count / ((double)time_plot[time_plot.Count() - 1]);
        plt.Title("Iterations: " + size_plot.Count() + ". It/s: " + itps);
        plt.SavePng("quickstart.png", 2000, 700);

        statsGetter.Print();

        return ret;
    }

    public int ActualBest() {
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

    public int GetScore(int u) {
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

    private int GetBestAdd() {
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

    private int GetBestRemove(bool forbidList) {

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

    private void AddToAddHeap(int v) {
        InHeap[v] = true;
        AddHeap.Insert(v, GetScore(v));
    }

    private void AddToRemoveHeap(int v) {
        InRemoveHeap[v] = true;
        int score = GetScore(v);

        //if (score < 0) Console.WriteLine("Added " + v + " to removeHeap @ " + score);

        RemoveHeap.Insert(v, score);
    }

    private void UpdateHeapScores(int v) {
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

    private void SetCCTrue(int v) {
        if (ConfChange[v] == false) {
            ConfChange.Set(v, true);
            if (!CandidateSol.SolutionContains(v) && !InHeap[v]) {
                AddToAddHeap(v);
            }
            if (InRemoveHeap[v]) {
                RemoveHeap.UpdateKey(v, GetScore(v));
            }
        }
    }

    private void AddVertex(int v) {
        statsGetter.SelectionCounts[v] += 1;
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

    private void RemoveVertex(int v) {
        statsGetter.SelectionCounts[v] += 1;
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

    public void IncreaseFreq() {
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
