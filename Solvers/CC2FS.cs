using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using BSC_DS_MP.Util;
using ScottPlot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Security;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace BSC_DS_MP.Solvers;
// https://jair.org/index.php/jair/article/view/11044/26218
internal class CC2FS : ISolver {

    // N2(v) = second level neighbors
    // N1(V) = first level neighbors

    internal class SimpleSol {
        public HashSet<int> vertices; // 1m ->
        public int[] coveredCount; // 1m -> 1.91 MB
        public int coveredSum;
        IGraph graph;

        public SimpleSol(IGraph graph) {
            vertices = new HashSet<int>();
            coveredCount = new int[graph.getSize()];
            foreach (int node in graph.GetNodes()) {
                coveredCount[node] = 0;
            }
            this.graph = graph;
            coveredSum = 0;
        }
        public void AddVertex(int v) {
            vertices.Add(v);
            coveredCount[v] += 1;
            if (coveredCount[v] == 1) {
                coveredSum += 1;
            }

            foreach (int neighbor in graph.GetEdges(v)) {
                coveredCount[neighbor] += 1;
                if (coveredCount[neighbor] == 1) {
                    coveredSum += 1;
                }
            }
        }
        public void RemoveVertex(int v) {
            if(!vertices.Contains(v)) throw new Exception("Vertex not in solution");
            vertices.Remove(v);
            coveredCount[v] -= 1;
            if (coveredCount[v] == 0) {
                coveredSum -= 1;
            }

            foreach (int neighbor in graph.GetEdges(v)) {
                coveredCount[neighbor] -= 1;
                if (coveredCount[neighbor] == 0) {
                    coveredSum -= 1;
                }
            }
        }
        public bool IsSolutionValid() {
            return coveredSum == (graph.getSize());
        }
        public IEnumerable<int> GetSolution() {
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
            foreach(int i in vertices)
                ret.AddVertex(i);

            return ret;
        }
    }

    BitArray ConfChange; // CC2 implementatiion - P6 // 1m -> 122 KB
    IGraph graph;
    SimpleSol CandidateSol; 
    uint[] freq; // 1m -> 1.91 MB
    List<int> forbidlist;
    List<int> addCandidates;

    public CC2FS() {
        addCandidates = new List<int>();
    }


    public ISolution Solve(IGraph graph, CancellationToken? token) {

        // STUFF
        var size_plot = new List<int>();
        var time_plot = new List<long>();
        //int iter = 0;

        if (token == null) throw new Exception("CC2FS needs a CancellationToken");

        this.graph = graph;
        ConfChange = new(graph.getSize());
        ConfChange.SetAll(true); // CC2R1
        freq = new uint[graph.getSize()];// short or int
        for (int i = 0; i < graph.getSize(); i++) {
            freq[i] = 1;
        }
        SimpleSol bestSolution;
        {
            ISolution init = new GreedyLazyHeap().Solve(graph.CloneInto(new AdjSetLstGraphFactory()),null);
            bestSolution = new SimpleSol(graph);  //TODO: GREEDY CAN JUST RETURN A SOL
            
            foreach (int i in init.GetEnumerator()) {
                bestSolution.AddVertex(i);
            }

            //
        }
        


        forbidlist = new List<int>();

        CandidateSol = bestSolution.Clone();
        foreach (int i in graph.GetNodes()) {
            if (CandidateSol.SolutionContains(i)) continue;
            addCandidates.Add(i);
        }

        var sw = Stopwatch.StartNew();


        while (!((CancellationToken) token).IsCancellationRequested) {
            size_plot.Add(CandidateSol.GetSolution().Count());
            time_plot.Add(sw.ElapsedMilliseconds);
            if (CandidateSol.IsSolutionValid()) {
                if (CandidateSol.GetSolution().Count() < bestSolution.GetSolution().Count()) {
                    bestSolution = CandidateSol.Clone(); // SAVE SOL IF BETTER
                }
                int v = VertexWithHighestScoreInS();
                // IDEA: WHEN REMOVING HAVE TWO HEAPS, ONE WITH FORBID AND ONE WITH IN S, COMPARE THE TWO AND POP THE LARGER
                RemoveVertex(v);
                continue;
            } else {
                int v = VertexInSWithHighestScoreWithForbid();
                RemoveVertex(v);
                forbidlist = new List<int>();

                while(!CandidateSol.IsSolutionValid()) {

                    //v = GetCCV2New(); // CCV2={v|ConfChange[v] = 1, v /∈S}
                    v = GetCCV2();
                    //Console.WriteLine("Selecteed vertex: " + (v+1) + " SCORE: "+GetScore(v));
                    AddVertex(v);

                    forbidlist.Add(v);                    
                    IncreaseFreq(); // LINE 16 -> Foreach vertex not in the Solution neighborhood, increase FREQ
                }
            }
        }

        var ret = new BitArraySolution(graph.getSize());
        foreach(int i in bestSolution.GetSolution()) {
            ret.AddVertex(i);
        }


        int[] xs = size_plot.ToArray();
        long[] ys = time_plot.ToArray();

        var plt = new Plot();
        plt.Add.Scatter(ys, xs);
        double itps = 60 * size_plot.Count / ((double)time_plot[time_plot.Count() - 1]);
        plt.Title(("Iterations: " + size_plot.Count() + ". It/s: " + itps));
        plt.SavePng("quickstart.png", 1000, 700);

        return ret;

    }

    public void IncreaseFreq() {
        foreach(int v in graph.GetNodes()) {

            if (CandidateSol.IsCovered(v)) continue;

            freq[v] += 1;
        }
    }

    public int GetCCV2() { // CCV2={v|ConfChange[v] = 1, v /∈S}
        int highest = int.MinValue;
        int reference = -1;
        foreach (int node in graph.GetNodes()) {
            if (CandidateSol.SolutionContains(node)) continue;
            if (!ConfChange.Get(node)) continue;
            var score = GetScore(node);
            if (score > highest) {
                highest = score;
                reference = node;
            }
        }

        return reference;
    }

    public int VertexInSWithHighestScoreWithForbid() {
        int highest = int.MinValue;
        int reference = -1;
        foreach (var node in CandidateSol.GetSolution()) {
            if (forbidlist.Contains(node)) continue;
            var score = GetScore(node);
            if (score > highest) {
                highest = score;
                reference = addCandidates[i];
                index = i;
            }
        }
        addCandidates.RemoveAt(index);
        
        return reference;
    }

    public int VertexInSWithHighestScoreWithForbid() { // NOTE: SEEMS EXPENSIVE
        //TODO: IMPLEMENT OLDEST ONE TIEBREAKER
        int highest = int.MinValue;
        int reference = -1;
        foreach (var node in CandidateSol.GetSolution()) {
            if(forbidlist.Contains(node)) continue;
            var score = GetScore(node);
            if (score > highest) {
                highest = score;
                reference = node;
            }
        }
        return reference;
    }

    public int GetScore(int u) {
        /**
         Definition 3For a graphG=  (V, E), and a candidate solutionS, the frequency based scoringfunction denoted byscoref, is a function such that
        Remark that, in the above definition,C1is indeed the set of uncovered vertices that would be-come covered by addinguintoSandC2is the set of covered vertices that would become uncoveredby removingufromS.
         */
        if (!CandidateSol.SolutionContains(u)) { 
            // CASE: u not in S
            // C1 = n[u] \ N[S] -> Neighborhood of u without the neighborhood of the solution.
            int sum = 0;
            foreach (var v in graph.GetEdges(u)) { // Neighborhood of u
                if (!CandidateSol.IsCovered(v)) { // Not neighbor of S / covered by S
                    sum += (int) freq[v];
                }
            }
            if(!CandidateSol.IsCovered(u)) {
                sum += (int)freq[u];
            }

            return sum;
        } else {
            // CASE: u in S
            int sum = 0;
            // C2 = n[u] \ N[S\{u}] -> is the set of covered vertices that would become uncoveredby removing u from S
            foreach (int neigh in graph.GetEdges(u)) { 
                if(CandidateSol.Covered(neigh) == 1) { // Only covered by u
                    sum -= (int) freq[neigh];
                }
            }
            if (CandidateSol.Covered(u) == 1) {
                sum -= (int)freq[u];
            }
            return sum;
        }
    }

    private void RemoveVertex(int v) {
        /**
         * CC2 RULE 2: When removing a vertex v from the candidate solution S,
         * ConfChange[v] isset to 0, and for each vertexu∈N2(v),ConfChange[u]is set to 1.
         */
        ConfChange.Set(v, false);
        foreach(int u in OpenTwoNeighborhood(v)) {
            ConfChange.Set(u, true);
        }
        ConfChange.Set(v, false); // UPDATE

        // UPDATE IN SOL
        CandidateSol.RemoveVertex(v);
        //Console.WriteLine("Removed vertex: " + (v+1));
    }

    private void AddVertex(int v) {
        /**
         * CC2 RULE 3: CC2-RULE3.When adding a vertexvinto the candidate solutionS, for each vertexu∈N2(v),ConfChange[u]is set to 1.
         */
        foreach (int u in OpenTwoNeighborhood(v)) {
            ConfChange.Set(u, true);
        }
        // UPDATE IN SOL
        CandidateSol.AddVertex(v);
        //Console.WriteLine("Added vertex: " + (v + 1) + "("+ CandidateSol.GetCoveredSum() + "/" +graph.getSize()+")");
    }

    private IEnumerable<int> OpenTwoNeighborhood(int v ) {
        // THOSE EXACTLY 2 AWAY, EXCLUDING V AND NEIGHBORS OF V
        HashSet<int> excluded = new HashSet<int>();
        excluded.Add(v);
        HashSet<int> secondNeighborhood = new();

        foreach (int neighbor in graph.GetEdges(v)) {
            excluded.Add(neighbor);
            foreach (int secondNeighbor in graph.GetEdges(neighbor)) {
                secondNeighborhood.Add(secondNeighbor);
            }
        }

        secondNeighborhood.ExceptWith(excluded);
        return secondNeighborhood;
    }

    private IEnumerable<int> OpenTwoNeighborhood(int v) {
        // THOSE EXACTLY 2 AWAY, EXCLUDING V AND NEIGHBORS OF V
        HashSet<int> excluded = new HashSet<int>();
        excluded.Add(v);
        HashSet<int> secondNeighborhood = new();

        foreach (int neighbor in graph.GetEdges(v)) {
            excluded.Add(neighbor);
            foreach (int secondNeighbor in graph.GetEdges(neighbor)) {
                secondNeighborhood.Add(secondNeighbor);
            }
        }

        secondNeighborhood.ExceptWith(excluded);
        return secondNeighborhood;
    } 
}
