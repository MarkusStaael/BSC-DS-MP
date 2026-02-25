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

     



    int cutofftime = 1000;

    // N2(v) = second level neighbors
    // N1(V) = first level neighbors

    BitArray ConfChange; // CC2 implementatiion - P6 // 1m -> 122 KB
    IGraph graph;
    HashSet<int> CandidateSol; 
    ushort[] freq; // 1m -> 1.91 MB
    ISolution solstate;
    List<int> forbidlist; 

    public ISet<int> Solve(IGraph graph) { 
        this.graph = graph;
        ConfChange = new(graph.getSize());
        ConfChange.SetAll(true); // CC2R1
        solstate = new SimpleSol(graph);

        freq = new ushort[graph.getSize()];// short or int
        for (int i = 0; i < graph.getSize(); i++) {
            freq[i] = 1;
        }

        ISet<int> bestSolution = new GreedyHeap2().Solve(graph); //TODO: GREEDY CAN JUST RETURN A SOL
        foreach(int i in bestSolution) {
            foreach (int neighbor in graph.GetEdges(i)) {
                solstate.AddVertex(i);
            }
        }
        forbidlist = new List<int>();

        CandidateSol = new HashSet<int>(bestSolution);

        while(0<cutofftime--) {
            if (solstate.IsSolutionValid()) {
                if (CandidateSol.Count() < bestSolution.Count()) {
                    bestSolution = new HashSet<int>(CandidateSol); // SAVE SOL IF BETTER
                }
                int v = VertexWithHighestScore();
                RemoveVertex(v);
                continue;
            } else {
                int v = VertexWithHighestScoreWithForbid();
                RemoveVertex(v);
                forbidlist = new List<int>();

                while(!solstate.IsSolutionValid()) {

                    v = VertexWithHighestScore();

                    AddVertex(v);
                    forbidlist.Add(v);

                    if(!IsNeighborOfSol(v)) {
                        freq[v] += 1;
                    }
                }
            }
        }

        return bestSolution;

    }

    public bool IsNeighborOfSol(int v) {
        foreach (int node in graph.GetEdges(v)) {
            if(CandidateSol.Contains(node)) return true;
        }
        return false;

    }

    public int VertexWithHighestScoreWithForbid() { // NOTE: SEEMS EXPENSIVE
        //TODO: IMPLEMENT OLDEST ONE TIEBREAKER
        int highest = -1;
        int reference = -1;
        foreach (var node in graph.GetNodes()) {
            if (forbidlist.Contains(node)) continue; 
            var score = GetFrequency(node);
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
            var score = GetFrequency(node);
            if(score > highest) {
                highest = score;
                reference = node;
            }
        }
        return reference;
    }

    public ushort GetFrequency(int v) {

        /**
         Definition 3For a graphG=  (V, E), and a candidate solutionS, the frequency based scoringfunction denoted byscoref, is a function such that
        
        Remark that, in the above definition,C1is indeed the set of uncovered vertices that would be-come covered by addinguintoSandC2is the set of covered vertices that would become uncoveredby removingufromS.
         
         */
        if (CandidateSol.Contains(v)) {
            ushort sum = 0;
            foreach(int neigh in graph.GetEdges(v)) {
                if (!solstate.IsCovered(neigh)) {
                    sum += freq[neigh];
                }
            }
            return sum;
        } else {
            ushort sum = 0;
            foreach (int neigh in graph.GetEdges(v)) {
                if (solstate.IsCovered(neigh)) {
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
        CandidateSol.Remove(v);
        solstate.RemoveVertex(v);
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
        CandidateSol.Add(v);
        solstate.AddVertex(v);
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
