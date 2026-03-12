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

    // N2(v) = second level neighbors
    // N1(v) = first level neighbors

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
            foreach (int node in graph.GetNodes()) {
                coveredCount[node] = 0;
            }
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
            if (coveredCount[v] == 0) {
                coveredSum -= 1;
                uncoveredVertices.Add(v);
            }

            foreach (int neighbor in graph.GetEdges(v)) {
                coveredCount[neighbor] -= 1;
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
            foreach (int i in GetEnumerator())
                ret.AddVertex(i);
            return ret;
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



    public CC2FS(IGraph graph) {
        this.graph = graph;
        forbidlist = new HashSet<int>();
        AddHeap = new(graph.getSize());
        RemoveHeap = new(graph.getSize());
        InHeap = new(graph.getSize(), false);

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

                mark[v] = stamp;
                foreach (int u in graph.GetEdges(v))
                    mark[u] = stamp;

                foreach (int u in graph.GetEdges(v)) {
                    foreach (int w in graph.GetEdges(u)) {
                        if (mark[w] != stamp) {
                            mark[w] = stamp;
                            TwoLevelNeighborhood[v].Add(w);
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

            } else {
                AddToAddHeap(v);
            }
        }

        var sw = Stopwatch.StartNew();

        while (!((CancellationToken)token).IsCancellationRequested) {
            size_plot.Add(CandidateSol.GetSolutionCount());
            time_plot.Add(sw.ElapsedMilliseconds);

            if (CandidateSol.IsSolutionValid()) {
                if (CandidateSol.GetSolutionCount() < bestSolution.GetSolutionCount()) {
                    bestSolution = CandidateSol.Clone();
                }
                // Line 5: remove vertex in S with highest score_f, ties broken by oldest
                int v = VertexInSWithHighestScore();
                RemoveVertex(v);
            } else {
                // Line 9: remove vertex in S with highest score_f not in forbid_list, ties broken by oldest
                int v = VertexInSWithHighestScoreWithForbid();
                RemoveVertex(v);
                forbidlist.Clear();

                while (!CandidateSol.IsSolutionValid() && !((CancellationToken)token).IsCancellationRequested) {
                    // Line 13: add vertex in CCV2 with highest score_f, ties broken by oldest
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

        return ret;
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
        int target = AddHeap.RemoveMax();
        InHeap[target] = false;

        return target;
    }

    private void AddToAddHeap(int v) {
        InHeap[v] = true;
        AddHeap.Insert(v, GetScore(v));
    }

    public int VertexInSWithHighestScoreWithForbid() {
        //TODO: IMPLEMENT OLDEST ONE TIEBREAKER
        int highest = int.MinValue;
        int reference = -1;
        foreach (var node in CandidateSol.GetEnumerator()) {
            if (forbidlist.Contains(node)) continue;
            var score = GetScore(node);
            if (score > highest) {
                highest = score;
                reference = node;
            }
        }
        return reference;
    }
    public int VertexInSWithHighestScore() { // NOTE: SEEMS EXPENSIVE
        //TODO: IMPLEMENT OLDEST ONE TIEBREAKER
        int highest = int.MinValue;
        int reference = -1;
        foreach (var node in CandidateSol.GetEnumerator()) {
            var score = GetScore(node);
            if (score > highest) {
                highest = score;
                reference = node;
            }
        }
        return reference;
    }

    private void UpdateAddHeapElm(int v) {
        if (InHeap[v]) {
            int newScore = GetScore(v);
            AddHeap.UpdateKey(v, newScore);
            //Console.WriteLine("Updated " + v + " to " + newScore);

        }
    }

    private void SetCCTrue(int v) {
        if (ConfChange[v] == false) {
            AddHeap.Insert(v, GetScore(v));
        }

        ConfChange.Set(v, true);

    }

    private void AddVertex(int v) {
        //Console.WriteLine("Adding " + v);
        CandidateSol.AddVertex(v);

        foreach (int u in TwoLevelNeighborhood[v]) {
            SetCCTrue(u);
        }

        foreach (int u in graph.GetEdges(v)) { // CAN HAVE REPEAT UPDATES 
            UpdateAddHeapElm(u);
            if (CandidateSol.IsCovered(v)) {
                foreach (int y in graph.GetEdges(u)) {
                    UpdateAddHeapElm(y);
                }
            }
        }
    }

    private void RemoveVertex(int v) {
        //Console.WriteLine("Removing " + v);
        CandidateSol.RemoveVertex(v);

        ConfChange.Set(v, false);

        foreach (int u in TwoLevelNeighborhood[v]) {
            SetCCTrue(u);
        }


        // UPDATE THE SCORES OF THE VERTICES TOUCHED
        foreach (int u in graph.GetEdges(v)) {
            if (!CandidateSol.IsCovered(v)) {
                foreach (int y in graph.GetEdges(u)) { // CAN HAVE REPEAT UPDATES 
                    UpdateAddHeapElm(y);
                }
            }
        }
    }


    public void IncreaseFreq() {
        foreach (int v in CandidateSol.uncoveredVertices) {
            freq[v] += 1;

        }

        foreach (int v in CandidateSol.uncoveredVertices) { // CAN HAVE REPEAT UPDATES
            UpdateAddHeapElm(v);
            foreach (int neigh in graph.GetEdges(v)) {
                UpdateAddHeapElm(neigh);
            }
        }
    }
}
