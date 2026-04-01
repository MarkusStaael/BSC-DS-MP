using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Solvers;
using BSC_DS_MP.Util;
using ScottPlot;
using System.Diagnostics;
using System.IO.Enumeration;
using SkiaSharp;
using ScottPlot.AxisRules;
using System.Reflection.PortableExecutable;

namespace BSC_DS_MP.Solvers;

public class SimAnnealImproved : ISolver
{
    private readonly List<HashSet<int>> graph;
    private readonly int n;
    private readonly double timeLimitSeconds;
    private readonly int undominatedPenalty;

    private readonly string filename;
    private readonly Random rand = new Random();

    private bool[] confChange;

    // Candidate frontier sets (instead of scanning whole graph)
    private HashSet<int> candidateAdd = new();
    private HashSet<int> candidateRemove = new();

    public SimAnnealImproved(IGraph g, double timeLimitSeconds, string filename)
    {
        if(timeLimitSeconds <= 0)
            throw new ArgumentException("Time limit must be positive", nameof(timeLimitSeconds));

        n = g.getSize();
        this.timeLimitSeconds = timeLimitSeconds;
        this.filename = filename;

        // Make non-dominating intermediate states cheaper to explore.
        // 10% of n is aggressive but prevents near-impossible undominated moves.
        undominatedPenalty = Math.Max(1, n / 10);

        graph = new List<HashSet<int>>(n);
        for (int i = 0; i < n; i++)
            graph.Add(new HashSet<int>(g.GetEdges(i)));
    }

    //initial domination count for each vertex. Updated incrementally as vertices are added/removed from D.
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

    private void UpdateNeighborhood(int v)
    {
        candidateAdd.Add(v);
        candidateRemove.Add(v);

        foreach (var u in graph[v])
        {
            candidateAdd.Add(u);
            candidateRemove.Add(u);
        }
    }

    private void AddVertex(int v, HashSet<int> D, int[] dominated, ref int undominated)
    {
        if (!D.Add(v)) return;

        if (dominated[v] == 0) undominated--;
        dominated[v]++;

        foreach (var u in graph[v])
        {
            if (dominated[u] == 0) undominated--;
            dominated[u]++;
        }

        confChange[v] = true;

        foreach (var u in graph[v])
            confChange[u] = true;

        UpdateNeighborhood(v);
    }

    private void RemoveVertex(int v, HashSet<int> D, int[] dominated, ref int undominated)
    {
        if (!D.Remove(v)) return;

        dominated[v]--;
        if (dominated[v] == 0) undominated++;

        foreach (var u in graph[v])
        {
            dominated[u]--;
            if (dominated[u] == 0) undominated++;
        }

        confChange[v] = false;

        foreach (var u in graph[v])
            confChange[u] = true;

        UpdateNeighborhood(v);
    }

    // cost of a solution is the size of D plus a large penalty for each undominated vertex.
    private int Cost(int setSize, int undominated)
    {
        return setSize + undominated * undominatedPenalty;
    }

    /*
        if v is dominated, score if added is 0. If v is not dominated, score if added is 1.
        For each neighbor u of v, if u is not dominated, score if added increases by 1.
    */
    private int ScoreIfAdded(int v, int[] dominated)
    {
        int score = dominated[v] == 0 ? 1 : 0;

        foreach (var u in graph[v])
            if (dominated[u] == 0)
                score++;

        return score;
    }

    // score if removed is how many currently covered vertices would become uncovered
    private int ScoreIfRemoved(int v, int[] dominated)
    {
        int score = dominated[v] == 1 ? 1 : 0;

        foreach (var u in graph[v])
            if (dominated[u] == 1)
                score++;

        return score;
    }

    // Picks the vertex that would cover the most currently uncovered vertices.
    // Only scans the candidate neighborhood instead of the entire graph.
    private int PickBestAdd(HashSet<int> D, int[] dominated)
    {
        int best = -1;
        int bestScore = -1;

        foreach (var v in candidateAdd)
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

    // Picks the vertex that would uncover the fewest currently covered vertices
    private int PickWorstRemove(HashSet<int> D, int[] dominated)
    {
        int worst = -1;
        int worstScore = int.MaxValue;

        foreach (var v in candidateRemove)
        {
            if (!D.Contains(v)) continue;

            int score = ScoreIfRemoved(v, dominated);

            if (score < worstScore)
            {
                worstScore = score;
                worst = v;
            }
        }

        if (worst == -1 && D.Count > 0)
            worst = D.ElementAt(rand.Next(D.Count));

        return worst;
    }

    // After a move, check if any vertices in D are redundant
    private void RemoveRedundant(HashSet<int> D, int[] dominated, ref int undominated)
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
                RemoveVertex(v, D, dominated, ref undominated);
        }
    }

    // Random move types with greedy evaluation: evaluate delta before moving
    private void RandomMoveGreedy(HashSet<int> D, int[] dominated, ref int undominated, double T)
    {
        int move = rand.Next(10);

        int moveType;
        if (move < 4 && D.Count > 0) moveType = 0; // Remove: 40%
        else if (move < 5) moveType = 1; // Add: 10%
        else if (move < 8) moveType = 2; // 1-to-1 swap: 30%
        else moveType = 3; // 1-to-2 swap: 20%

        // Evaluate delta and execute move if accepted
        if (moveType == 0 && D.Count > 0) // Remove
        {
            int remove = PickWorstRemove(D, dominated);
            if (remove >= 0)
            {
                int score = ScoreIfRemoved(remove, dominated);
                int delta = -1 + score * undominatedPenalty;
                if (delta <= 0 || rand.NextDouble() < Math.Exp(-delta / T))
                {
                    RemoveVertex(remove, D, dominated, ref undominated);
                }
            }
        }
        else if (moveType == 1) // Add
        {
            int add = PickBestAdd(D, dominated);
            if (add >= 0)
            {
                int score = ScoreIfAdded(add, dominated);
                int delta = 1 - score * undominatedPenalty;
                if (delta <= 0 || rand.NextDouble() < Math.Exp(-delta / T))
                {
                    AddVertex(add, D, dominated, ref undominated);
                }
            }
        }
        else if (moveType == 2) // 1-to-1 swap
        {
            int remove = PickWorstRemove(D, dominated);
            int add = PickBestAdd(D, dominated);
            if (remove >= 0 && add >= 0)
            {
                int removeScore = ScoreIfRemoved(remove, dominated);
                int addScore = ScoreIfAdded(add, dominated);
                int delta = (-1 + removeScore * undominatedPenalty) + (1 - addScore * undominatedPenalty);
                if (delta <= 0 || rand.NextDouble() < Math.Exp(-delta / T))
                {
                    RemoveVertex(remove, D, dominated, ref undominated);
                    AddVertex(add, D, dominated, ref undominated);
                }
            }
        }
        else if (moveType == 3) // 1-to-2 swap
        {
            int remove = PickWorstRemove(D, dominated);
            int add1 = PickBestAdd(D, dominated);
            int add2 = PickBestAdd(D, dominated);
            if (add2 == add1) add2 = rand.Next(n);
            if (remove >= 0 && add1 >= 0 && add2 >= 0)
            {
                int removeScore = ScoreIfRemoved(remove, dominated);
                int addScore1 = ScoreIfAdded(add1, dominated);
                int addScore2 = ScoreIfAdded(add2, dominated);
                int delta = (-1 + removeScore * undominatedPenalty) + (1 - addScore1 * undominatedPenalty) + (1 - addScore2 * undominatedPenalty);
                if (delta <= 0 || rand.NextDouble() < Math.Exp(-delta / T))
                {
                    RemoveVertex(remove, D, dominated, ref undominated);
                    AddVertex(add1, D, dominated, ref undominated);
                    AddVertex(add2, D, dominated, ref undominated);
                }
            }
        }

        // Remove redundancy frequently to aggressively clean up the set
        if (rand.Next(3) == 0) // Every ~3 moves
            RemoveRedundant(D, dominated, ref undominated);
    }

    public HashSet<int> Optimize(HashSet<int> greedy, CancellationToken token, string name)
    {
        HashSet<int> current = new HashSet<int>(greedy);
        HashSet<int> best = new HashSet<int>(greedy);

        int[] dominated = ComputeDominatedCount(current);

        int undominated = dominated.Count(x => x == 0);
        int bestUndominated = undominated;

        confChange = new bool[n];
        Array.Fill(confChange, true);

        // initialize candidate frontier around greedy solution
        UpdateNeighborhood(rand.Next(n)); // random initial vertex to populate candidate sets

        //Temperature schedule parameters from SimAnnealRandom:
        double initialT = Math.Max(0.5, n / 10.0);
        double finalT = 0.01;

        double opsPerT = 10; // AKA epoch length
        double opsPerSecond = 200; // rough estimate, can be tuned based on graph size or observed performance
        double coolingRatio = opsPerSecond / opsPerT;

        // For Newton's law of cooling: T(t) = T0 * exp(-k * t)
        double k = -Math.Log(finalT / initialT) / timeLimitSeconds; // Cooling rate to reach finalT at timeLimit

        double T = initialT;

        //for plotting
        var size_plot = new List<int>();
        var time_plot = new List<long>();
        var sw = Stopwatch.StartNew();

        while (!token.IsCancellationRequested && T > finalT)
        {
            for (int i = 0; i < opsPerT && !token.IsCancellationRequested; i++) // inner loop: perform moves at current temperature.
            {
                RandomMoveGreedy(current, dominated, ref undominated, T);

                if (Cost(current.Count, undominated) < Cost(best.Count, bestUndominated))
                {
                    best = new HashSet<int>(current);
                    bestUndominated = undominated;
                }
            }

            size_plot.Add(current.Count);
            time_plot.Add(sw.ElapsedMilliseconds);

            T = initialT * Math.Exp(-k * (sw.ElapsedMilliseconds / 1000.0)); // Newton's law of cooling
        }

        int[] xs = size_plot.ToArray();
        long[] ys = time_plot.ToArray();

        var plt = new Plot();
        plt.Add.Scatter(ys, xs);
        double itps = size_plot.Count * opsPerT / (time_plot[time_plot.Count - 1] / 1000.0); // iterations per second
        plt.Title("Iterations: " + (size_plot.Count * opsPerT) + ". It/s: " + itps);
        plt.SavePng("SAI_" + name + ".png", 1000, 700);

        return best;
    }

    public ISolution Solve(IGraph graph, CancellationToken? token)
    {
        // Use GreedyDecreaseKey as starting point
        var greedySolver = new GreedyDecreaseKey();
        var greedySol = greedySolver.Solve(graph, token);
        var initial = new HashSet<int>(greedySol.GetEnumerator());

        CancellationToken ct = token ?? CancellationToken.None;

        var optimized = Optimize(initial, ct, filename);

        var result = new HashSetSolution(graph.getSize());
        foreach (var v in optimized)
            result.AddVertex(v);

        return result;
    }
}