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
        // Console.WriteLine($"Initial heap size after insertion: {heap.Size()}");
        // optionally peek at coverage values of first few nodes
        /*for (int i = 0; i < Math.Min(10, coverage.Length); i++) {
            Console.Write(coverage[i] + ",");
        }
        Console.WriteLine();*/

        // Greedy selection until all vertices are covered
        int totalRemovals = 0;
        int skipCount = 0;
        while (heap.Size() > 0 && coveredCount < size) {
            FibonacciHeapNode<int, int> maxNode = heap.RemoveMax();
            totalRemovals++;
            if (maxNode == null) break;

            int selectedNode = maxNode.Data;

            // lazy skip
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
                        // each newly covered vertex removes one uncovered neighbour from nbr
                        coverage[nbr]--;
                        var heapNode = key2Node[nbr];
                        // if heap has been emptied by a preceding RemoveMax there is nothing to update
                        if (heapNode != null && !heap.IsEmpty()) {
                            heap.DecreaseKey(heapNode, coverage[nbr]);
                        } // otherwise neighbor wasn't in heap (shouldn't happen now that GetNodes is fixed)
                    }
                }
            }
        }

        // debugging output
        // Console.WriteLine($"GreedyNoUpdate removed {totalRemovals} nodes (skipped {skipCount}) and covered {coveredCount}/{size}");

        // fallback: if heap emptied early (or break due to null max) leave some
        // vertices uncovered, we must finish by greedily covering them to ensure
        // a valid dominating set.  This occurs when the Fibonacci heap becomes
        // corrupted and cannot deliver further elements.
        if (coveredCount < size) {
            // Console.WriteLine("Heap terminated prematurely, executing fallback covering.");
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

