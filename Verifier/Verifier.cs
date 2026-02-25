using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Verifier; 
internal class Verifier {

    public static bool Verify(ISolution solution, IGraph graph) {
        HashSet<int> covered = new HashSet<int>();
        foreach(int node in solution.GetEnumerator()) {
            covered.Add(node);
            foreach(int neighbor in graph.GetEdges(node)) {
                covered.Add(neighbor);
            }
        }
        return covered.Count == graph.getSize();
    }

}
