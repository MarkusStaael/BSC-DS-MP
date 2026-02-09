using BSC_DS_MP.DataModel.Graph;
using DataModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Xml.Linq;

namespace BSC_DS_MP.Solutions;

public class GreedyHeap : ISolution {


    public ISet<int> Solve(IGraph graph) {
        bool solved = false;
        HashSet<int> added = new();
        FibonacciHeap<int,int> heap = new FibonacciHeap<int, int>(int.MinValue);
        Dictionary<int,FibonacciHeapNode<int,int>> key2Node = new();

        foreach (int node in graph.GetNodes()) {
            var fnode = new FibonacciHeapNode<int,int>(node, -graph.GetEdges(node).Count()); // NEGATIVE SO ITS A MAX HEAP
            key2Node.Add(node, fnode);
            heap.Insert(fnode);
        }

        while (!solved) {

            int nodeRef = heap.RemoveMin().Data;
            HashSet<int> updateSet = new HashSet<int>();

            added.Add(nodeRef);
            // remove from graph
            foreach(int node in graph.GetEdges(nodeRef)) {
                if (!graph.GetNodes().Contains(node)) continue;
                foreach (int neighbor in graph.GetEdges(node)){
                    updateSet.Add(neighbor);
                }
                graph.RemoveNode(node);
                heap.Delete(key2Node[node]);
            }
            graph.RemoveNode(nodeRef);

            // Update edge count of neighbors 
            foreach (int node in updateSet) {
                if(graph.GetNodes().Contains(node))
                    heap.IncreaseKey(key2Node[node], -graph.GetEdges(node).Count());
            }

            if (graph.GetNodes().Count() == 0) {
                solved = true;
            }
            //

        }



        return added;
    }
}
