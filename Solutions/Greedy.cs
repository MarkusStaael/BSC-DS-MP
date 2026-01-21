using BSC_DS_MP.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Solutions;

public class Greedy : ISolution {
    public ISet<int> Solve(IGraph graph) {
        bool solved = false;
        HashSet<int> added = new();
        graph = graph.Clone();
        while (!solved) {
            int nodeRef = -1;
            int edgeMax = -1;
            // find greatest
            foreach (int node in graph.GetNodes()) {
                int count = graph.GetEdges(node).Count();

                if(count > edgeMax) {
                    nodeRef = node;
                    edgeMax = count;
                }

            }

            // add to solution
            added.Add(nodeRef);
            // remove from graph
            foreach(int node in graph.GetEdges(nodeRef)) {
                graph.RemoveNode(node);
            }
            graph.RemoveNode(nodeRef);

            if(graph.GetNodes().Count() == 0) {
                solved = true;
            }

        }



        return added;
    }
}
