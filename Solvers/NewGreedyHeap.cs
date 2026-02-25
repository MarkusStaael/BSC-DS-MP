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
        HashSet<int> sol = new HashSet<int>(size);

        var heap = new FibonacciHeap<int, int>(int.MaxValue);
        var key2Node = new FibonacciHeapNode<int,int>[size];

        Console.WriteLine("Initializing heap...");
        Console.WriteLine("Graph size: " + size);
        Console.WriteLine("Nodes in graph: " + string.Join(", ", graph.GetNodes()));

        foreach (int node in graph.GetNodes()) {
            var fnode = new FibonacciHeapNode<int,int>(node, graph.GetEdges(node).Count());
            key2Node[node] = fnode;
            heap.Insert(fnode);
            Console.WriteLine($"Inserted node {node} with degree {graph.GetEdges(node).Count()} into heap.");
        }

        

        return sol; 
    }
}

