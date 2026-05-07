using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Util;

namespace BSC_DS_MP.Solvers;

public interface ISolverFactory {
    ISolver Create(AdjLstWithSolGraph graph, CancellationToken token, String name);
}
