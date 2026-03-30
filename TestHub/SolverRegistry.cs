using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Solvers;

namespace BSC_DS_MP.TestHub;

internal static class SolverRegistry
{
    public delegate ISolver SolverFactory(IGraph graph, double timeLimitSeconds);

    private static readonly Dictionary<string, SolverFactory> _solvers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["greedy"]       = (g, t) => new Greedy(),
        ["greedy-dk"]    = (g, t) => new GreedyDecreaseKey(),
        ["greedy-lazy"]  = (g, t) => new GreedyLazyHeap(),
        ["greedy-fib"]   = (g, t) => new GreedyDecreaseKeyFib(),
        ["cc2fs"]        = (g, t) => new CC2FS(g),
        ["cc2fs-claude"] = (g, t) => new CC2FS_Claude(g),
        ["simanneal"]    = (g, t) => new SimAnneal(g, t),
    };

    public static ISolver Create(string name, IGraph graph, double timeLimitSeconds)
    {
        if (!_solvers.TryGetValue(name, out var factory))
            throw new ArgumentException($"Unknown solver: '{name}'. Available: {string.Join(", ", _solvers.Keys)}");
        return factory(graph, timeLimitSeconds);
    }

    public static IReadOnlyCollection<string> Names => _solvers.Keys;

    public static bool Exists(string name) => _solvers.ContainsKey(name);
}
