using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Util;

namespace BSC_DS_MP.Util.Reduction;

public interface IReduction {
    /// <summary>
    /// Applies MDS reduction rules to <paramref name="original"/>.
    /// Returns a smaller graph whose vertices are contiguous IDs 0..k-1,
    /// with CoveredCount pre-populated by a greedy initial solution.
    /// State needed to reconstruct the full solution is stored internally.
    /// </summary>
    AdjLstWithSolGraph Reduce(IGraph original);

    /// <summary>Number of vertices in the original graph. Valid after Reduce().</summary>
    int OriginalSize { get; }

    /// <summary>Number of vertices in the reduced graph. Valid after Reduce().</summary>
    int ReducedSize { get; }

    /// <summary>
    /// Maps a solution on the reduced graph back to the original vertex IDs,
    /// adds any vertices forced into DS during reduction, and removes redundant
    /// vertices.
    /// </summary>
    ISolution Reconstruct(ISolution reducedSolution);
}
