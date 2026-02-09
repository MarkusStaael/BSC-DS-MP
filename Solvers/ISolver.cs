using BSC_DS_MP.DataStructures.Graph;
using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Solutions; 
internal interface ISolver {
    public ISet<int> Solve(IGraph graph);
}
