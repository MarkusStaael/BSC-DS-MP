using BSC_DS_MP.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Solutions; 
internal interface ISolution {
    public ISet<int> Solve(IGraph graph);
}
