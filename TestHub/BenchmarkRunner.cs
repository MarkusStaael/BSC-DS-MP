using System.Diagnostics;
using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Reading;
using Verify = BSC_DS_MP.Verifier.Verifier;

namespace BSC_DS_MP.TestHub;

internal record BenchmarkResult(
    string File,
    string Solver,
    int SolutionSize,
    int? OptimalSize,
    double? GapPct,
    TimeSpan Elapsed,
    bool Verified);

internal class BenchmarkRunner
{
    private readonly IGraphFactory _graphFactory;

    public BenchmarkRunner(IGraphFactory graphFactory)
    {
        _graphFactory = graphFactory;
    }

    public BenchmarkResult RunSingle(
        string solverName,
        string filePath,
        double timeLimitSeconds,
        bool verify,
        Dictionary<string, int>? optimals)
    {
        IGraph graph = Reader.DominatingSetReader(_graphFactory, filePath);
        IGraph clone = graph.CloneInto(_graphFactory);

        var solver = SolverRegistry.Create(solverName, clone, timeLimitSeconds);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeLimitSeconds));

        var sw = Stopwatch.StartNew();
        var result = solver.Solve(clone, cts.Token);
        sw.Stop();

        bool verified = verify && Verify.Verify(result, graph);
        if (verify && !verified)
            Console.WriteLine($"  WARNING: {solverName} on {Path.GetFileName(filePath)} FAILED verification");

        int size = result.Count();
        int? optimal = LookupOptimal(filePath, optimals);
        double? gap = optimal.HasValue && optimal.Value > 0
            ? 100.0 * (size - optimal.Value) / optimal.Value
            : null;

        return new BenchmarkResult(
            Path.GetFileName(filePath),
            solverName,
            size,
            optimal,
            gap,
            sw.Elapsed,
            verified);
    }

    public List<BenchmarkResult> RunBatch(
        TestConfig config,
        List<string> filePaths,
        Dictionary<string, int>? optimals)
    {
        var solverNames = config.Solvers.Length == 1
                          && config.Solvers[0].Equals("all", StringComparison.OrdinalIgnoreCase)
            ? SolverRegistry.Names.ToList()
            : config.Solvers.ToList();

        var results = new List<BenchmarkResult>();

        foreach (string filePath in filePaths)
        {
            Console.WriteLine($"--- {Path.GetFileName(filePath)} ---");
            foreach (string solver in solverNames)
            {
                try
                {
                    var r = RunSingle(solver, filePath, config.TimeLimit, config.Verify, optimals);
                    results.Add(r);

                    if (config.PrintSolutions)
                    {
                        // Re-run is wasteful; instead we'd need to capture the solution.
                        // For now, the result record doesn't carry vertices. This is a known limitation.
                        Console.WriteLine($"  (printSolutions not yet implemented in batch mode)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR: {solver} on {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }
        }

        return results;
    }

    private static int? LookupOptimal(string filePath, Dictionary<string, int>? optimals)
    {
        if (optimals == null) return null;
        string name = Path.GetFileNameWithoutExtension(filePath);
        return optimals.TryGetValue(name, out int val) ? val : null;
    }
}
