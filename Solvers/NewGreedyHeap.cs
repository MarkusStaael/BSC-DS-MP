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

public class NewGreedyHeap : ISolver {
    public IEnumerable<int> Solve(IGraph graph) {
        int size = graph.getSize();
        HashSet<int> sol = new HashSet<int>();
        BitArray covered = new BitArray(size, false);

        var heap = new FibonacciHeap<int, int>(int.MaxValue);
        var key2Node = new FibonacciHeapNode<int, int>[size];

        // Initialize heap with each node's coverage count (uncovered neighbors + itself)
        foreach (int node in graph.GetNodes()) {
            int coverage = graph.GetEdges(node).Count() + 1; // +1 for the node itself
            var fnode = new FibonacciHeapNode<int, int>(node, coverage);
            key2Node[node] = fnode;
            heap.Insert(fnode);
        }

        // Greedy selection until all vertices are covered
        while (heap.Size() > 0) {
            // Extract node with maximum coverage
            FibonacciHeapNode<int, int> maxNode = heap.RemoveMax();
            if (maxNode == null) break;

            int selectedNode = maxNode.Data;

            // Skip if already covered
            if (covered[selectedNode]) continue;

            // Add to dominating set
            sol.Add(selectedNode);
            covered[selectedNode] = true;

            // Mark selected node's neighbors as covered
            foreach (int neighbor in graph.GetEdges(selectedNode)) {
                covered[neighbor] = true;
            }

            // Update heap: decrease key for nodes whose coverage changed
            foreach (int node in graph.GetNodes()) {
                if (!covered[node] && key2Node[node] != null) {
                    // Recalculate coverage: count uncovered neighbors + self
                    int newCoverage = 1; // Count the node itself
                    foreach (int neighbor in graph.GetEdges(node)) {
                        if (!covered[neighbor]) {
                            newCoverage++;
                        }
                    }

                    // If coverage decreased, update the key
                    if (newCoverage < key2Node[node].Key) {
                        key2Node[node].Key = newCoverage;
                    }
                }
            }

            // Stop if all vertices are covered
            if (covered.Cast<bool>().All(x => x)) break;
        }

        return sol;
    }
}

