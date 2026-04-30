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

public class Greedy : ISolver {
    
    public ISolution Solve(IGraph graph, CancellationToken? token) {
        int size = graph.getSize(); // +1 to account for 0-based indexing and dummy node
        ISolution sol = new HashSetSolution(size);
        BitArray covered = new BitArray(size, false);
        int coveredCount = 0;


        var heap = new FibonacciHeap<int, int>(int.MaxValue);
        FibonacciHeapNode<int, int>[] key2Node = new FibonacciHeapNode<int, int>[size];
        //Console.WriteLine("Initializing heap with graph nodes and their degrees... \n Graph size: " + graph.getSize() +"\n Nodes:: " + graph.GetNodes().Count());

        // Initialize heap with coverage (uncovered neighbors + self)
        foreach (int node in graph.GetNodes()) {
            int coverage = graph.GetEdges(node).Count() + 1;
            var fnode = new FibonacciHeapNode<int, int>(node, coverage);
            heap.Insert(fnode);
            key2Node[node] = fnode;
            //Console.WriteLine("Node: " + node + " Degree: " + graph.GetEdges(node).Count());
        }

        // Greedy selection until all vertices are covered
        while (heap.Size() > 0 && coveredCount < size) {
            FibonacciHeapNode<int, int> maxNode = heap.RemoveMax();
            if (maxNode == null) break;

            int selectedNode = maxNode.Data;

            // Add to dominating set
            sol.AddVertex(selectedNode);
            if (!covered[selectedNode]) {
                covered[selectedNode] = true;
                coveredCount++;
            }

            // Mark neighbors as covered
            foreach (int neighbor in graph.GetEdges(selectedNode)) {
                if (!covered[neighbor]) {
                    covered[neighbor] = true;
                    coveredCount++;
                }
            }
        }

        return sol;
    }
}

