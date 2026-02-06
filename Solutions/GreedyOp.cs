using BSC_DS_MP.DataModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Xml.Linq;

namespace BSC_DS_MP.Solutions;

public class GreedyOp : ISolution {

    private class Stuff {

        public void add(int id) {
            throw new NotImplementedException();
        }

        public int getMax() {
            throw new NotImplementedException();
        }

        public void updateNode(int id) {
            throw new NotImplementedException();
        }

        public void deleteNode(int id) {
            throw new NotImplementedException();
        }

    }


    public ISet<int> Solve(IGraph graph) {
        bool solved = false;
        HashSet<int> added = new();
        graph = graph.Clone();

        var maxFinder = new Stuff();

        foreach (int node in graph.GetNodes()) {
            maxFinder.add(graph.GetEdges(node).Count());
        }

        while (!solved) {

            int nodeRef = maxFinder.getMax();
            HashSet<int> updateSet = new HashSet<int>();

            added.Add(nodeRef);
            // remove from graph
            foreach(int node in graph.GetEdges(nodeRef)) {
                foreach(int neighbor in graph.GetEdges(node)){
                    updateSet.Add(neighbor);
                }
                graph.RemoveNode(node);
                maxFinder.deleteNode(node);
            }
            graph.RemoveNode(nodeRef);
            maxFinder.deleteNode(nodeRef);

            // Update edge count of neighbors 
            foreach (int node in updateSet) {
                maxFinder.updateNode(node);
            }

            if (graph.GetNodes().Count() == 0) {
                solved = true;
            }
            //

        }



        return added;
    }
}
