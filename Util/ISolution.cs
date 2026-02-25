using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Util; 
internal interface ISolution {

    public void AddVertex(int v);
    public void RemoveVertex(int v);

    public bool IsSolutionValid();
    public bool IsCovered(int v);
    public bool SolutionContains(int v);

    public int GetCoveredSum();
    public IEnumerable<int> GetSolution();
}
