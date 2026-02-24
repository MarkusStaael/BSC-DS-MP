using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Solutions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace BSC_DS_MP.Solvers;

internal class CC2FS : ISolver {

     



    int cutofftime = 500;

    // N2(v) = second level neighbors
    // N1(V) = first level neighbors

    BitArray ConfChange; // CC2 implementatiion - P6
    IGraph graph;
    HashSet<int> CandidateSol;
    ushort[] freq;
    ushort[] covered;
    public ISet<int> Solve(IGraph graph) {
        this.graph = graph;
        ConfChange = new(graph.getSize());
        ConfChange.SetAll(true); // CC2R1

        freq = new ushort[graph.getSize()];// short or int
        for(int i = 0; i < graph.getSize(); i++) {
            freq[i] = 1;
        }

        var bestSolution = new GreedyHeap().Solve(graph);
        CandidateSol = new HashSet<int>(bestSolution);

        while(0<cutofftime--) {

        }

        return bestSolution;

    }

    public ushort GetFrequency(int v) {

        if(CandidateSol.Contains(v)) {

        }

        return 1;
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
    }

    private void AddVertex(int v) {
        /**
         * CC2 RULE 3: CC2-RULE3.When adding a vertexvinto the candidate solutionS, for each vertexu∈N2(v),ConfChange[u]is set to 1.
         */
        foreach (int u in SecondNeighborhood(v)) {
            ConfChange.Set(u, true);
        }
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

    

    /**




    **/
    
}
