using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using BSC_DS_MP.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Numerics;
using System.Text;
using System.Xml.Linq;

namespace BSC_DS_MP.Solvers;

public class GreedyDecreaseKey : ISolver {
    public ISolution Solve(IGraph graph, CancellationToken? token) {
        int size = graph.getSize();
        ISolution sol = new HashSetSolution(size);
        BitArray covered = new BitArray(size, false);
        int coveredCount = 0;

        // store current coverage for each node (uncovered neighbours + self)
        int[] coverage = new int[size];
        var heap = new IndexedMaxHeap(size);

        // initialize heap with coverage values
        foreach (int node in graph.GetNodes()) {
            int cov = graph.GetEdges(node).Count() + 1;
            coverage[node] = cov;
            heap.Insert(node, cov);
        }
        // Console.WriteLine($"Initial heap size after insertion: {heap.Size()}");
        // optionally peek at coverage values of first few nodes
        /*for (int i = 0; i < Math.Min(10, coverage.Length); i++) {
            Console.Write(coverage[i] + ",");
        }
        Console.WriteLine();*/
        
        // Greedy selection until all vertices are covered
        int totalRemovals = 0;
        int skipCount = 0;
        while (!heap.IsEmpty() && coveredCount < size) {
            int selectedNode = heap.RemoveMax();
            totalRemovals++;

            // lazy skip (should not occur)
            if (covered[selectedNode]) {
                skipCount++;
                continue;
            }

            // gather nodes that will become covered this round (selected + its neighbors)
            var newlyCovered = new List<int>();
            if (!covered[selectedNode]) {
                covered[selectedNode] = true;
                coveredCount++;
                newlyCovered.Add(selectedNode);
                sol.AddVertex(selectedNode);
            }

            foreach (int neighbor in graph.GetEdges(selectedNode)) {
                if (!covered[neighbor]) {
                    covered[neighbor] = true;
                    coveredCount++;
                    newlyCovered.Add(neighbor);
                }
            }

            // for each node that just became covered, decrement coverage of its uncovered neighbours
            foreach (int c in newlyCovered) {
                foreach (int nbr in graph.GetEdges(c)) {
                    if (!covered[nbr]) {
                        coverage[nbr]--;
                        heap.DecreaseKey(nbr, coverage[nbr]);
                    }
                }
            }
        }

        // debugging output
        // Console.WriteLine($"GreedyNoUpdate removed {totalRemovals} nodes (skipped {skipCount}) and covered {coveredCount}/{size}");

        // fallback: should not be necessary with the indexed heap, but keep it just in case
        /*if (coveredCount < size) {
            for (int v = 0; v < size && coveredCount < size; v++) {
                if (!covered[v]) {
                    sol.AddVertex(v);
                    covered[v] = true;
                    coveredCount++;
                    foreach (int nbr in graph.GetEdges(v)) {
                        if (!covered[nbr]) {
                            covered[nbr] = true;
                            coveredCount++;
                        }
                    }
                }
            }
        }*/

        return sol;
    }
}

