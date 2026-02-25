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
        ushort[] uncoveredNeighbors = new ushort[size]; // uncovered neighbors
        int coveredSum = 0;


        var heap = new FibonacciHeap<int, int>(int.MinValue);
        var key2Node = new FibonacciHeapNode<int, int>[size];

        foreach (int node in graph.GetNodes()) {
            var fnode = new FibonacciHeapNode<int, int>(node, graph.GetEdges(node).Count()); // NEGATIVE SO ITS A MAX HEAP
            key2Node[node] = fnode;
            heap.Insert(fnode);
            uncoveredNeighbors[node] = (ushort)graph.GetEdges(node).Count();
        }// test

        while (true) {
            int selectedNode;
            while (true) {
                selectedNode = heap.RemoveMax().Data;

                int unCoveredNeighbors = (uncoveredNeighbors[selectedNode]);
                //Console.WriteLine("Selected node: " + (selectedNode + 1) + "(" + unCoveredNeighbors + ")");

                if (markChanged[selectedNode]) {
                    if (unCoveredNeighbors != 0) {
                        heap.Insert(new(selectedNode, unCoveredNeighbors));
                        markChanged[selectedNode] = false;
                    } else {
                    }

                    continue;
                }
                break;
            }

            sol[selectedNode] = true;
            foreach (int neighbor in graph.GetEdges(selectedNode)) { // Foreach neighbor
                //Console.WriteLine("-"+neighbor + " is a neighbor of " + selectedNode);
                foreach (int neighbor2 in graph.GetEdges(neighbor)) { // Foreach neighbor of the neighbor
                    //Console.WriteLine("--"+neighbor2 + " is a neighbor of " + neighbor);
                    if (!coveredmark[neighbor]) uncoveredNeighbors[neighbor2] -= 1; // Reduce their uncovered neighbors count
                    markChanged[neighbor2] = true; // Mark it as changes so lazy can update it when its selected
                }
                if (!coveredmark[selectedNode]) {
                    uncoveredNeighbors[neighbor] -= 1;
                }
                markChanged[neighbor] = true; // -||-
                if (!coveredmark[neighbor]) {
                    coveredmark[neighbor] = true;
                    coveredSum++;
                }
            }
            if (!coveredmark[selectedNode]) {
                coveredmark[selectedNode] = true;
                coveredSum++;
            }
            graph.RemoveNode(selectedNode);
            //Console.WriteLine((selectedNode+1) + "-> " + coveredSum + "/" + size );
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

