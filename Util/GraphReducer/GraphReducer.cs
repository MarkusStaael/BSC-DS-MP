using BSC_DS_MP.DataStructures.Graph;
using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Util; 
public class GraphReducer : IGraphReducer {

    public GraphReducer() {
    }

    public (IGraph, int[],int) Reduce(IGraph graph) {
        throw new NotImplementedException();
    }
    
    public IGraph Reconstruct(IGraph reducedGraph) {
        throw new NotImplementedException();
    }
}
