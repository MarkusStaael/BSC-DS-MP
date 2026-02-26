using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Security;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace BSC_DS_MP.Solvers;

internal class CC2FS : ISolver {


    int cutofftime = 100;

    // N2(v) = second level neighbors
    // N1(V) = first level neighbors

    internal class SimpleSol {
        public HashSet<int> vertices; // 1m ->
        public ushort[] coveredCount; // 1m -> 1.91 MB
        public bool solutionIsValid;
        public int coveredSum;
        IGraph graph;

        public SimpleSol(IGraph graph) {
            vertices = new HashSet<int>();
            coveredCount = new ushort[graph.getSize()];
            foreach (int node in graph.GetNodes()) {
                coveredCount[node] = 0;
            }
            solutionIsValid = false;
            this.graph = graph;
            coveredSum = 0;
        }
        public void AddVertex(int v) {
            vertices.Add(v);
            foreach (int neighbor in graph.GetEdges(v)) {
                coveredCount[neighbor] += 1;
                if (coveredCount[neighbor] == 1) {
                    coveredSum += 1;
                }
            }
        }
        public void RemoveVertex(int v) {
            vertices.Remove(v);
            foreach (int neighbor in graph.GetEdges(v)) {
                coveredCount[neighbor] -= 1;
                if (coveredCount[neighbor] == 0) {
                    solutionIsValid = false;
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
    ushort[] freq; // 1m -> 1.91 MB
    List<int> forbidlist; 

    public ISolution Solve(IGraph graph) { 
        this.graph = graph;
        ConfChange = new(graph.getSize());
        ConfChange.SetAll(true); // CC2R1
        freq = new ushort[graph.getSize()];// short or int
        for (int i = 0; i < graph.getSize(); i++) {
            freq[i] = 1;
        }

        ISolution init = new GreedyLazyHeap().Solve(graph.CloneInto(new AdjSetLstGraphFactory()));
        SimpleSol bestSolution = new SimpleSol(graph);  //TODO: GREEDY CAN JUST RETURN A SOL
        foreach(int i in init.GetEnumerator()) {
            bestSolution.AddVertex(i);
        }
        

        forbidlist = new List<int>();

        CandidateSol = bestSolution.Clone();

        while (0<cutofftime--) {
            if (CandidateSol.IsSolutionValid()) {
                if (CandidateSol.GetCoveredSum() < bestSolution.GetCoveredSum()) {
                    bestSolution = CandidateSol.Clone(); // SAVE SOL IF BETTER
                }
                int v = VertexWithHighestScore();
                RemoveVertex(v);
                continue;
            } else {
                int v = VertexWithHighestScoreWithForbid();
                RemoveVertex(v);
                forbidlist = new List<int>();

                while(!CandidateSol.IsSolutionValid()) {

                    v = VertexWithHighestScore();

                    AddVertex(v);
                    forbidlist.Add(v);

                    if(!IsNeighborOfSol(v)) {
                        freq[v] += 1;
                    }
                }
            }
        }

        var ret = new BitArraySolution(graph.getSize());
        foreach(int i in CandidateSol.GetSolution()) {
            ret.AddVertex(i);
        }

        return ret;

    }

    public bool IsNeighborOfSol(int v) {
        foreach (int node in graph.GetEdges(v)) {
            if(CandidateSol.GetSolution().Contains(node)) return true;
        }
        return false;

    }

    public int VertexWithHighestScoreWithForbid() { // NOTE: SEEMS EXPENSIVE
        //TODO: IMPLEMENT OLDEST ONE TIEBREAKER
        int highest = -1;
        int reference = -1;
        foreach (var node in graph.GetNodes()) {
            if (forbidlist.Contains(node)) continue; 
            var score = GetScore(node);
            if (score > highest) {
                highest = score;
                reference = node;
            }
        }
        return reference;
    }

    public int VertexWithHighestScore() { // NOTE: SEEMS EXPENSIVE
        //TODO: IMPLEMENT OLDEST ONE TIEBREAKER
        int highest = -1;
        int reference = -1;
        foreach (var node in graph.GetNodes()) {
            var score = GetScore(node);
            if(score > highest) {
                highest = score;
                reference = node;
            }
        }
        return reference;
    }

    public ushort GetScore(int u) {

        /**
         Definition 3For a graphG=  (V, E), and a candidate solutionS, the frequency based scoringfunction denoted byscoref, is a function such that
        
        Remark that, in the above definition,C1is indeed the set of uncovered vertices that would be-come covered by addinguintoSandC2is the set of covered vertices that would become uncoveredby removingufromS.
         
         */
        if (!CandidateSol.GetSolution().Contains(u)) {
            // CASE: u not in S
            // C1 = n[u] \ N[S] -> Neighborhood of u without the neighborhood of the solution.
            ushort sum = 0;
            foreach (var v in graph.GetEdges(u)) { // Neighborhood of u
                if (!CandidateSol.IsCovered(v)) { // Not neighbor of S / covered by S
                    sum += freq[v];
                }
            }
            return sum;
        } else {
            // CASE: u in S
            ushort sum = 0;
            // C2 = n[u] \ N[S\{u}] -> is the set of covered vertices that would become uncoveredby removing u from S
            foreach (int neigh in graph.GetEdges(u)) { 
                if(CandidateSol.Covered(neigh) == 1) { // Only covered by u
                    sum -= freq[neigh];
                }
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
        foreach(int u in SecondNeighborhood(v)) {
            ConfChange.Set(u, true);
        }

        // UPDATE IN SOL
        CandidateSol.RemoveVertex(v);
        Console.WriteLine("Removed vertex: " + v);
    }

    private void AddVertex(int v) {
        /**
         * CC2 RULE 3: CC2-RULE3.When adding a vertexvinto the candidate solutionS, for each vertexu∈N2(v),ConfChange[u]is set to 1.
         */
        foreach (int u in SecondNeighborhood(v)) {
            ConfChange.Set(u, true);
        }
        // UPDATE IN SOL
        CandidateSol.AddVertex(v);
        Console.WriteLine("Added vertex: " + v);
    }

    private HashSet<int> SecondNeighborhood(int v) {
        HashSet<int> secondNeighborhood = new();
        foreach (int neighbor in graph.GetEdges(v)) {
            foreach(int secondNeighbor in graph.GetEdges(neighbor)) {
                secondNeighborhood.Add(secondNeighbor);
            }
        }
        return secondNeighborhood;
    }    
}
