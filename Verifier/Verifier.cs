using BSC_DS_MP.DataStructures.Graph;
using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Verifier; 
internal class Verifier {

    public bool Verify(IEnumerable<int> solution, IGraph graph) {
        HashSet<int> covered = new HashSet<int>();
        foreach (int node in solution) {
            covered.Add(node);
            foreach (int neighbor in graph.GetEdges(node)) {
                covered.Add(neighbor);
            }
        }
        return covered.Count == graph.getSize();
    }

}
