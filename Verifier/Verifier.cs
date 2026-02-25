using BSC_DS_MP.DataStructures.Graph;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Verifier; 
internal class Verifier {

    public static bool Verify(BitArray solution, IGraph graph) {
        HashSet<int> covered = new HashSet<int>();
        for(int i = 0; i < solution.Length; i++) {
            if (solution[i]) {
                covered.Add(i);
                foreach (int neighbor in graph.GetEdges(i)) {
                    covered.Add(neighbor);
                }
            }
        }
        return covered.Count == graph.getSize();
    }

}
