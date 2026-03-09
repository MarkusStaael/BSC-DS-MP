using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Solvers;
using BSC_DS_MP.Util;

namespace BSC_DS_MP.Solvers;

public class SimAnneal : ISolver
{
    private readonly List<HashSet<int>> graph;
    private readonly int n;
    private readonly double timeLimitSeconds;
    private readonly Random rand = new Random();

    private bool[] confChange;

    public SimAnneal(IGraph g, double timeLimitSeconds)
    {
        if(timeLimitSeconds <= 0)
            throw new ArgumentException("Time limit must be positive", nameof(timeLimitSeconds));

        n = g.getSize();
        this.timeLimitSeconds = timeLimitSeconds;
        graph = new List<HashSet<int>>(n);
        for (int i = 0; i < n; i++)
        {
            graph.Add(new HashSet<int>(g.GetEdges(i)));
        }
    }

    private int[] ComputeDominatedCount(HashSet<int> D)
    {
        int[] dominated = new int[n];

        foreach (var v in D)
        {
            dominated[v]++;
            foreach (var u in graph[v])
                dominated[u]++;
        }

        return dominated;
    }

private void AddVertex(int v, HashSet<int> D, int[] dominated)
{
    if (!D.Add(v)) return;

    dominated[v]++;
    foreach (var u in graph[v])
        dominated[u]++;

    confChange[v] = true;

    foreach (var u in graph[v])
        confChange[u] = true;
}

private void RemoveVertex(int v, HashSet<int> D, int[] dominated)
{
    if (!D.Remove(v)) return;

    dominated[v]--;
    foreach (var u in graph[v])
        dominated[u]--;

    confChange[v] = false;

    foreach (var u in graph[v])
        confChange[u] = true;
}

    private int Cost(HashSet<int> D, int[] dominated)
    {
        int undominated = 0;

        for (int i = 0; i < n; i++)
            if (dominated[i] == 0)
                undominated++;

        return D.Count + undominated * n;
    }

    private int ScoreIfAdded(int v, int[] dominated)
    {
        int score = dominated[v] == 0 ? 1 : 0;

        foreach (var u in graph[v])
            if (dominated[u] == 0)
                score++;

        return score;
    }

    private int ScoreIfRemoved(int v, int[] dominated)
    {
        int score = dominated[v] == 1 ? 1 : 0;

        foreach (var u in graph[v])
            if (dominated[u] == 1)
                score++;

        return score;
    }

private int PickBestAdd(HashSet<int> D, int[] dominated)
{
    int best = -1;
    int bestScore = -1;

    for (int v = 0; v < n; v++)
    {
        if (D.Contains(v)) continue;
        if (!confChange[v]) continue;

        int score = ScoreIfAdded(v, dominated);

        if (score > bestScore)
        {
            bestScore = score;
            best = v;
        }
    }

    if (best == -1)
        best = rand.Next(n);

    return best;
}

    private int PickWorstRemove(HashSet<int> D, int[] dominated)
    {
        int worst = -1;
        int worstScore = int.MaxValue;

        foreach (var v in D)
        {
            int score = ScoreIfRemoved(v, dominated);

            if (score < worstScore)
            {
                worstScore = score;
                worst = v;
            }
        }

        return worst;
    }

    private void RemoveRedundant(HashSet<int> D, int[] dominated)
    {
        var vertices = D.ToList();

        foreach (var v in vertices)
        {
            bool redundant = true;

            if (dominated[v] <= 1)
                redundant = false;

            foreach (var u in graph[v])
            {
                if (dominated[u] <= 1)
                {
                    redundant = false;
                    break;
                }
            }

            if (redundant)
                RemoveVertex(v, D, dominated);
        }
    }

    private void RandomMove(HashSet<int> D, int[] dominated)
    {
        int move = rand.Next(10);

        if (move < 1 && D.Count > 0)
        {
            int remove = PickWorstRemove(D, dominated);
            RemoveVertex(remove, D, dominated);
        }
        else if (move < 7)
        {
            int add = PickBestAdd(D, dominated);
            AddVertex(add, D, dominated);
        }
        else
        {
            int remove = PickWorstRemove(D, dominated);
            RemoveVertex(remove, D, dominated);

            int v = PickBestAdd(D, dominated);
            AddVertex(v, D, dominated);

            int w = rand.Next(n);
            AddVertex(w, D, dominated);
        }

        RemoveRedundant(D, dominated);
    }

    public HashSet<int> Optimize(HashSet<int> greedy, CancellationToken token)
    {
        HashSet<int> current = new HashSet<int>(greedy);
        HashSet<int> best = new HashSet<int>(greedy);

        int[] dominated = ComputeDominatedCount(current);

        confChange = new bool[n];
        Array.Fill(confChange, true);

        double initialT = n;
        double finalT = 0.01 * (60.0 / timeLimitSeconds);
        double movesPerSecond = 500.0;  // Reduced estimate
        double innerPerOuter = 500.0;
        double outerPerSecond = movesPerSecond / innerPerOuter;
        double numOuter = timeLimitSeconds * outerPerSecond;
        double alpha = Math.Pow(finalT / initialT, 1.0 / numOuter);

        double T = initialT;

        while (!token.IsCancellationRequested)
        {
            for (int i = 0; i < 500 && !token.IsCancellationRequested; i++)
            {
                var next = new HashSet<int>(current);
                var nextDom = (int[])dominated.Clone();

                RandomMove(next, nextDom);

                int currentCost = Cost(current, dominated);
                int nextCost = Cost(next, nextDom);

                int delta = nextCost - currentCost;

                if (delta <= 0 || rand.NextDouble() < Math.Exp(-delta / T))
                {
                    current = next;
                    dominated = nextDom;

                    if (nextCost < Cost(best, ComputeDominatedCount(best)))
                        best = new HashSet<int>(next);
                }
            }

            T *= alpha;
        }

        return best;
    }

    public ISolution Solve(IGraph graph, CancellationToken? token)
    {
        // Use GreedyDecreaseKey as starting point
        var greedySolver = new GreedyDecreaseKey();
        var greedySol = greedySolver.Solve(graph, token);
        var initial = new HashSet<int>(greedySol.GetEnumerator());

        // Handle cancellation token - assume it's time-based if provided
        CancellationToken ct = token ?? CancellationToken.None;

        var optimized = Optimize(initial, ct);

        var result = new HashSetSolution(graph.getSize());
        foreach (var v in optimized)
        {
            result.AddVertex(v);
        }

        return result;
    }
}