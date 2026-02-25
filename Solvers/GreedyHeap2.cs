using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using BSC_DS_MP.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Xml.Linq;

namespace BSC_DS_MP.Solvers;

public class GreedyHeap2 : ISolver {
    public ISet<int> Solve(IGraph graph) {
        
        ISolution sol = new SimpleSol(graph);

        var heap = new FibonacciHeap<int, int>(int.MinValue);
        var key2Node = new FibonacciHeapNode<int,int>[graph.getSize()+1];

        foreach (int node in graph.GetNodes()) {
            var fnode = new FibonacciHeapNode<int,int>(node, -graph.GetEdges(node).Count()); // NEGATIVE SO ITS A MAX HEAP
            key2Node[node] = fnode;
            heap.Insert(fnode);
        }

        while (true) {

            int selectedNode = heap.RemoveMin().Data;   // Select the best 'greedy' option
            sol.AddVertex(selectedNode);

            List<int> toBeReduced = new();
            // remove next-doors, update their neighbors
            foreach(int nextdoor in graph.GetEdges(selectedNode)) {
                if (!sol.SolutionContains(nextdoor)) {
                    foreach (int secondDoor in graph.GetEdges(nextdoor)) {
                        toBeReduced.Add(secondDoor);
                    }
                }
                heap.DecreaseKey(key2Node[nextdoor], int.MinValue); // Mark as deleted by setting key to max value
                //heap.Delete(key2Node[nextdoor]);
                //heap.IncreaseKey(key2Node[nextdoor], BigInteger.Min);
                //toDelete.Add(nextdoor); // Mark for deletion
            }
            //graph.RemoveNode(nodeRef);

            // Update edge count of neighbors 

            foreach (int node in toBeReduced) {
                if (!sol.SolutionContains(node))
                    heap.IncreaseKey(key2Node[node], -graph.GetEdges(node).Count());
            }

            //System.Console.WriteLine(selectedNode+"->"+sol.GetSolution().Count()+ ", "+ sol.IsSolutionValid()+"/"+sol.GetCoveredSum());

            if (sol.IsSolutionValid()) {
                break;
            }
        }



        return new HashSet<int>(sol.GetSolution());
    }
}
