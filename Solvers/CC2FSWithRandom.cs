using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static BSC_DS_MP.Solvers.CC2FS;

namespace BSC_DS_MP.Solvers;

public class CC2FSWithRandom : CC2FS {
    public CC2FSWithRandom(IGraph graph, string printname) : base(graph, printname) {
    
        Random = new Random(0);

    }

    Random Random;
    protected int GetNextRandomRemove(int random) {
        int i = random;
        while (true) {
            if (CandidateSol.SolutionContains(i) && !forbidlist.Contains(i)) return i;
            else
                i = (i + 1) % graph.getSize();
        }
    }



    public override void DoLoopLogic() {
        if (CandidateSol.IsSolutionValid()) {
            if (CandidateSol.GetSolutionCount() < BestSolution.Count()) {
                BestSolution = CandidateSol.GetAsRetSol();
            }
            int v = GetBestRemove(forbidList: false);
            RemoveVertex(v);
        } else {
            int v = GetNextRandomRemove(Random.Next(graph.getSize()));
            RemoveVertex(v);
            forbidlist.Clear();

            while (!CandidateSol.IsSolutionValid()) {
                v = GetBestAdd();
                AddVertex(v);
                forbidlist.Add(v);
                IncreaseFreq();
            }
        }
    }
}
