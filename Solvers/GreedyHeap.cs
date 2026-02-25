using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Xml.Linq;

namespace BSC_DS_MP.Solvers;
/**
public class GreedyHeap : ISolver {
    public ISet<int> Solve(IGraph graph) {
        bool solved = false;
        HashSet<int> ds = new();
        FibonacciHeap<int, int> heap = new(int.MaxValue);
        FibonacciHeapNode<int, int>[] key2Node = new FibonacciHeapNode<int, int>[graph.getSize()];

        foreach (int node in graph.GetNodes()) {
            var fnode = new FibonacciHeapNode<int, int>(node, graph.GetEdges(node).Count()); // POSITIVE FOR MAX HEAP
            key2Node[node] = fnode;
            heap.Insert(fnode);
        }

        while (!solved) {

            int nodeRef = heap.RemoveMax().Data;
            HashSet<int> updateSet = new HashSet<int>();

            ds.Add(nodeRef);
            // remove from graph
            foreach (int node in graph.GetEdges(nodeRef)) {
                if (!graph.GetNodes().Contains(node)) continue;
                foreach (int neighbor in graph.GetEdges(node)) {
                    updateSet.Add(neighbor);
                }
                graph.RemoveNode(node);
                //heap.Delete(key2Node[node]);
            }
            graph.RemoveNode(nodeRef);

            // Update edge count of neighbors 
            foreach (int node in updateSet) {
                if (graph.GetNodes().Contains(node))
                    heap.IncreaseKey(key2Node[node], graph.GetEdges(node).Count());
            }

            if (graph.GetNodes().Count() == 0) {
                solved = true;
            }
            //

        }



        return ds;
    }
}
**/