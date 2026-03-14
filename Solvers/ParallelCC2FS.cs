using System;
using System.Threading;
using System.Threading.Tasks;
using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Util;

namespace BSC_DS_MP.Solvers;

/// <summary>
/// Runs multiple CC2FS_Claude instances in parallel with different random seeds.
/// Returns the best solution found across all instances.
/// CC2FS is embarrassingly parallel at the multi-start level.
/// </summary>
public class ParallelCC2FS : ISolver {
    private readonly int numThreads;
    private readonly bool useOneLevelCC;

    public ParallelCC2FS(int numThreads = -1, bool useOneLevelCC = false) {
        this.numThreads = numThreads == -1 ? Environment.ProcessorCount : numThreads;
        this.useOneLevelCC = useOneLevelCC;
    }

    public ISolution Solve(IGraph graph, CancellationToken? token) {
        if (token == null) throw new Exception("ParallelCC2FS needs a CancellationToken");

        var ct = (CancellationToken)token;
        var tasks = new Task<ISolution>[numThreads];

        Console.WriteLine($"ParallelCC2FS: launching {numThreads} instances");

        for (int i = 0; i < numThreads; i++) {
            int seed = i;
            string plotName = i == 0 ? "Claude.png" : $"Claude_{i}.png";
            tasks[i] = Task.Run(() => {
                var solver = new CC2FS_Claude(graph, seed, useOneLevelCC, plotName);
                return solver.Solve(graph, ct);
            });
        }

        Task.WaitAll(tasks);

        ISolution best = null!;
        int bestCount = int.MaxValue;
        for (int i = 0; i < numThreads; i++) {
            int count = tasks[i].Result.Count();
            Console.WriteLine($"  Thread {i}: solution size = {count}");
            if (count < bestCount) {
                bestCount = count;
                best = tasks[i].Result;
            }
        }

        return best;
    }
}
