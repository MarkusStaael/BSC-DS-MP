using BSC_DS_MP.DataStructures.Graph;
using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Util; 
public interface IGraphReducer {
    public (IGraph, int[],int) Reduce(IGraph graph);
    public ISolution Reconstruct(ISolution reducedSolution);
}
