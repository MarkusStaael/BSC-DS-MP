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

public class GreedyDecreaseKeyFib : ISolver {
    public ISolution Solve(IGraph graph, CancellationToken? token) {
        int size = graph.getSize();
        ISolution sol = new HashSetSolution(size);
        BitArray covered = new BitArray(size, false);
        int coveredCount = 0;

        // store current coverage for each node (uncovered neighbours + self)
        int[] coverage = new int[size];
        var heap = new FibonacciHeap<int, int>(int.MaxValue);
        var key2Node = new FibonacciHeapNode<int, int>[size];

        // initialize heap with coverage values
        foreach (int node in graph.GetNodes()) {
            int cov = graph.GetEdges(node).Count() + 1;
            coverage[node] = cov;
            var fnode = new FibonacciHeapNode<int, int>(node, cov);
            heap.Insert(fnode);
            key2Node[node] = fnode;
        }

        // Greedy selection until all vertices are covered
        while (!heap.IsEmpty() && coveredCount < size) {
            var maxNode = heap.RemoveMax();
            if (maxNode == null) break;

            int selectedNode = maxNode.Data;

            // lazy skip
            if (covered[selectedNode]) {
                continue;
            }

            // gather nodes that will become covered this round
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
                        var heapNode = key2Node[nbr];
                        if (heapNode != null) {
                            heap.DecreaseKey(heapNode, coverage[nbr]);
                        }
                    }
                }
            }
        }

        // fallback (should not occur with fixed heap)
        if (coveredCount < size) {
            Console.WriteLine($"Fallback triggered: covered {coveredCount}/{size}");
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
        }

        return sol;
    }
}
