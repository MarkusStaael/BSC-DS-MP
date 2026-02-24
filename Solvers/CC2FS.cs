using BSC_DS_MP.DataStructures.Graph;
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

        freq = new ushort[graph.getSize()+1];// short or int
        covered = new ushort[graph.getSize()+1];
        for (int i = 0; i < graph.getSize(); i++) {
            freq[i] = 1;
            covered[i] = 0;
        }

        var bestSolution = new GreedyHeap2().Solve(graph);
        foreach(int i in bestSolution) {
            foreach (int neighbor in graph.GetEdges(i)) {
                covered[i] += 1;
            }
        }

        CandidateSol = new HashSet<int>(bestSolution);

        while(0<cutofftime--) {
            if(NoUncoveredVetices()) {
                if()
                continue;
            }
        }

        return bestSolution;

    }
    public bool NoUncoveredVetices() {
        throw new NotImplementedException();
    }

    public ushort GetFrequency(int v) {

        /**
         Definition 3For a graphG=  (V, E), and a candidate solutionS, the frequency based scoringfunction denoted byscoref, is a function such that
        
        Remark that, in the above definition,C1is indeed the set of uncovered vertices that would be-come covered by addinguintoSandC2is the set of covered vertices that would become uncoveredby removingufromS.
         
         */

        if (CandidateSol.Contains(v)) {
            ushort sum = 0;
            foreach(int neigh in graph.GetEdges(v)) {
                if (covered[neigh]==0) {
                    sum += freq[neigh];
                }
            }
            return sum;
        } else {
            ushort sum = 0;
            foreach (int neigh in graph.GetEdges(v)) {
                if (covered[neigh] == 1) {
                    sum -= freq[neigh];
                }
            }
            return sum;
        }

            return 1;
    }

    private void DecisionRemoveVertex(int v) {
        /*
         * Remove rule 1
         */

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
        foreach (int neighbor in graph.GetEdges(v)) {
            covered[neighbor] -= 1;
        }
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
        foreach(int neighbor in graph.GetEdges(v)) {
            covered[neighbor] += 1;
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
