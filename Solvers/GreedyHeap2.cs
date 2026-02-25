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

public class GreedyHeap2 : ISolver {
    public IEnumerable<int> Solve(IGraph graph) {
        int size = graph.getSize();

        BitArray sol = new BitArray(size, false);

        BitArray markChanged = new BitArray(size, false);
        BitArray coveredmark = new BitArray(size, false);
        ushort[] coveredNeighbors = new ushort[size];
        int coveredSum = 0; 


        var heap = new FibonacciHeap<int, int>(int.MaxValue);
        var key2Node = new FibonacciHeapNode<int,int>[size];

        foreach (int node in graph.GetNodes()) {
            var fnode = new FibonacciHeapNode<int,int>(node, graph.GetEdges(node).Count()); // POSITIVE FOR MAX HEAP
            key2Node[node] = fnode;
            heap.Insert(fnode);
            coveredNeighbors[node] = 0;
        }

        while (true) {
            int selectedNode; 
            while(true) {
                selectedNode = heap.RemoveMax().Data;
                Console.WriteLine("Selected node: " + (selectedNode+1));
                if (markChanged[selectedNode]) {
                    int unCoveredNeighbors = graph.GetEdges(selectedNode).Count()-coveredNeighbors[selectedNode];

                    Console.WriteLine("unCoveredNeighbors: " + unCoveredNeighbors);
                    if (unCoveredNeighbors != 0) { 
                        heap.Insert(new(selectedNode, unCoveredNeighbors));
                        markChanged[selectedNode] = false;
                    } else {
                        graph.RemoveNode(selectedNode);
                    }

                        continue;
                }
                break;
            }

            sol[selectedNode] = true;

            foreach(int neighbor in graph.GetEdges(selectedNode)) {
                markChanged[neighbor] = true;
                if (!coveredmark[neighbor]) { // COUNT UP
                    coveredmark[neighbor] = true;
                    coveredSum++;
                }
                foreach(int neighbor2 in graph.GetEdges(neighbor)) {
                    coveredNeighbors[neighbor2] += 1;
                    markChanged[neighbor2] = true;
                }
            }
            graph.RemoveNode(selectedNode);
            Console.WriteLine((selectedNode+1) + "-> " + coveredSum + "/" + size );
            if (coveredSum == size) {
                break;
            }
        }

        //System.Console.WriteLine(selectedNode+"->"+sol.GetSolution().Count()+ ", "+ sol.IsSolutionValid()+"/"+sol.GetCoveredSum());
        // RETURN ENUMERABLE
        for (int i = 0; i < sol.Length; i++) {
            if (sol[i])
                yield return i;
        }

    }
}

