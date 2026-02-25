using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Solvers; 
internal interface ISolver {
    public ISolution Solve(IGraph graph);
}
