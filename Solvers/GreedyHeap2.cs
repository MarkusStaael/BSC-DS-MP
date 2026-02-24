using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Xml.Linq;

namespace BSC_DS_MP.Solvers;

public class GreedyHeap2 : ISolver {
    public ISet<int> Solve(IGraph graph) {
        
        HashSet<int> ds = new();
        BitArray inSolution = new(graph.getSize());
        var heap = new FibonacciHeap<int, int>(int.MinValue);
        var key2Node = new FibonacciHeapNode<int,int>[graph.getSize()];

        foreach (int node in graph.GetNodes()) {
            var fnode = new FibonacciHeapNode<int,int>(node, -graph.GetEdges(node).Count()); // NEGATIVE SO ITS A MAX HEAP
            key2Node[node] = fnode;
            heap.Insert(fnode);
        }

        while (true) {

            int selectedNode = heap.RemoveMin().Data;   // Select the best 'greedy' option
            ds.Add(selectedNode);                       // Add to dominating set
            inSolution.Set(selectedNode, true);         // Mark that this set is in/dominated in our solution


            HashSet<int> toDelete = new();
            List<int> toBeReduced = new();
            // remove next-doors, update their neighbors
            foreach(int nextdoor in graph.GetEdges(selectedNode)) {
                if (!inSolution[nextdoor]) {
                    foreach (int secondDoor in graph.GetEdges(nextdoor)) {
                        toBeReduced.Add(secondDoor);
                    }
                }
                inSolution.Set(nextdoor,true); // Write down that we have used this node - basically deleted
                heap.Delete(key2Node[nextdoor]);
                //heap.IncreaseKey(key2Node[nextdoor], BigInteger.Min);
                //toDelete.Add(nextdoor); // Mark for deletion
            }
            //graph.RemoveNode(nodeRef);

            // Update edge count of neighbors 

            foreach (int node in toBeReduced) {
                if (!inSolution[node])
                    heap.IncreaseKey(key2Node[node], -graph.GetEdges(node).Count());
            }

            if (graph.GetNodes().Count() == 0) {
                break;
            }
            //

        }



        return ds;
    }
}
