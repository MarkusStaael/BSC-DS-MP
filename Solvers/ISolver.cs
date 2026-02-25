using BSC_DS_MP.DataStructures.Graph;
using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Solvers; 
internal interface ISolver {
    public IEnumerable<int> Solve(IGraph graph);
}
