using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Solvers;
using BSC_DS_MP.Util;
using ScottPlot;
using System.Diagnostics;

namespace BSC_DS_MP.Solvers;

public class SimAnneal2 : ISolver
{
    private readonly int[][] graph;
    private readonly int n;
    private readonly double timeLimitSeconds;
    private readonly Random rand = new Random();

    private bool[] confChange;

    // Candidate frontier sets (instead of scanning whole graph)
    private readonly List<int> candidateAdd = new();
    private readonly List<int> candidateRemove = new();
    private bool[] isInCandidateAdd;
    private bool[] isInCandidateRemove;

    public SimAnneal2(IGraph g, double timeLimitSeconds)
    {
        if(timeLimitSeconds <= 0)
            throw new ArgumentException("Time limit must be positive", nameof(timeLimitSeconds));

        n = g.getSize();
        this.timeLimitSeconds = timeLimitSeconds;

        graph = new int[n][];
        for (int i = 0; i < n; i++)
            graph[i] = g.GetEdges(i).ToArray();

        confChange = new bool[n];
        isInCandidateAdd = new bool[n];
        isInCandidateRemove = new bool[n];
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
        if (!isInCandidateAdd[v])
        {
            candidateAdd.Add(v);
            isInCandidateAdd[v] = true;
        }

        if (!isInCandidateRemove[v])
        {
            candidateRemove.Add(v);
            isInCandidateRemove[v] = true;
        }

        foreach (var u in graph[v])
        {
            if (!isInCandidateAdd[u])
            {
                candidateAdd.Add(u);
                isInCandidateAdd[u] = true;
            }

            if (!isInCandidateRemove[u])
            {
                candidateRemove.Add(u);
                isInCandidateRemove[u] = true;
            }
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
    // Maybe make the penalty proportional to the number of uncovered vertices instead of a fixed large number, so that we can have some intermediate solutions that are not fully dominating,
    // but still better than others.
    private int Cost(int setSize, int undominated)
    {
        return setSize + undominated * n;
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

    //Random move types: add a vertex, remove a vertex, or swap.
    //Use probabilistic acceptance before applying modifications to avoid expensive rollback copies.
    private bool TryRandomMove(HashSet<int> D, int[] dominated, ref int undominated, double T)
    {
        int move = rand.Next(10);

        if (move < 1 && D.Count > 0)
        {
            int v = PickWorstRemove(D, dominated);
            if (v < 0) return false;

            int score = ScoreIfRemoved(v, dominated);
            int delta = -1 + score * n;

            if (delta > 0 && rand.NextDouble() >= Math.Exp(-delta / T))
                return false;

            RemoveVertex(v, D, dominated, ref undominated);
        }
        else if (move < 7)
        {
            int v = PickBestAdd(D, dominated);
            if (v < 0) return false;

            int score = ScoreIfAdded(v, dominated);
            int delta = 1 - score * n;

            if (delta > 0 && rand.NextDouble() >= Math.Exp(-delta / T))
                return false;

            AddVertex(v, D, dominated, ref undominated);
        }
        else
        {
            int submove = rand.Next(2); // 0 for 1-1 swap, 1 for 1-2 swap

            if (submove == 0)
            {
                // 1-1 swap: remove 1, add 1
                int remove = PickWorstRemove(D, dominated);
                if (remove < 0) return false;

                int add = PickBestAdd(D, dominated);
                if (add < 0) return false;

                // evaluate combined swap effect
                int removeScore = ScoreIfRemoved(remove, dominated);
                int addScore = ScoreIfAdded(add, dominated);
                int delta = (-1 + removeScore * n) + (1 - addScore * n);

                if (delta > 0 && rand.NextDouble() >= Math.Exp(-delta / T))
                    return false;

                RemoveVertex(remove, D, dominated, ref undominated);
                AddVertex(add, D, dominated, ref undominated);
            }
            else
            {
                // 1-2 swap: remove 1, add 2
                int remove = PickWorstRemove(D, dominated);
                if (remove < 0) return false;

                int add1 = PickBestAdd(D, dominated);
                if (add1 < 0) return false;

                int add2 = PickBestAdd(D, dominated);
                if (add2 < 0 || add2 == add1) add2 = rand.Next(n); // fallback to random if same

                // evaluate combined swap effect
                int removeScore = ScoreIfRemoved(remove, dominated);
                int addScore1 = ScoreIfAdded(add1, dominated);
                int addScore2 = ScoreIfAdded(add2, dominated);
                int delta = (-1 + removeScore * n) + (1 - addScore1 * n) + (1 - addScore2 * n);

                if (delta > 0 && rand.NextDouble() >= Math.Exp(-delta / T))
                    return false;

                RemoveVertex(remove, D, dominated, ref undominated);
                AddVertex(add1, D, dominated, ref undominated);
                AddVertex(add2, D, dominated, ref undominated);
            }
        }

        RemoveRedundant(D, dominated, ref undominated);
        return true;
    }

    public HashSet<int> Optimize(HashSet<int> greedy, CancellationToken token)
    {
        HashSet<int> current = new HashSet<int>(greedy);
        HashSet<int> best = new HashSet<int>(greedy);

        int[] dominated = ComputeDominatedCount(current);

        int undominated = dominated.Count(x => x == 0);

        confChange = new bool[n];
        Array.Fill(confChange, true);

        // initialize candidate frontier around greedy solution
        foreach (var v in greedy)
        {
            if (!isInCandidateRemove[v])
            {
                candidateRemove.Add(v);
                isInCandidateRemove[v] = true;
            }

            foreach (var u in graph[v])
            {
                if (!isInCandidateAdd[u])
                {
                    candidateAdd.Add(u);
                    isInCandidateAdd[u] = true;
                }
            }
        }


        //Temperature schedule parameters:
        double initialT = n;
        double finalT = 0.01 * (60.0 / timeLimitSeconds);

        double movesPerSecond = 20000.0;
        double opsPerT = 10.0;
        double outerPerSecond = movesPerSecond / opsPerT;
        double numOuter = timeLimitSeconds * outerPerSecond;

        double alpha = Math.Pow(finalT / initialT, 1.0 / numOuter);

        double T = initialT;

        int bestCost = Cost(best.Count, undominated);

        var size_plot = new List<int>();
        var time_plot = new List<long>();
        var sw = Stopwatch.StartNew();

        while (!token.IsCancellationRequested)
        {
            for (int i = 0; i < opsPerT && !token.IsCancellationRequested; i++) // inner loop: perform moves at current temperature.
            {
                bool moved = TryRandomMove(current, dominated, ref undominated, T);
                if (!moved) continue;

                int newCost = Cost(current.Count, undominated);
                if (newCost < bestCost)
                {
                    bestCost = newCost;
                    best = new HashSet<int>(current);
                }

            size_plot.Add(current.Count);
            time_plot.Add(sw.ElapsedMilliseconds);
            }
            

            T *= alpha;
        }

        int[] xs = size_plot.ToArray(); 
        long[] ys = time_plot.ToArray(); 

        var plt = new Plot(); 
        plt.Add.Scatter(ys, xs); 
        double itps = size_plot.Count / (time_plot[time_plot.Count - 1] / 1000.0); // iterations per second
        plt.Title("Iterations: " + size_plot.Count + ". It/s: " + itps); 
        plt.SavePng("SimulatedAnnealing2.png", 1000, 700);

        return best;
    }

    public ISolution Solve(IGraph graph, CancellationToken? token)
    {
        // Use GreedyDecreaseKey as starting point
        var greedySolver = new GreedyDecreaseKey();
        var greedySol = greedySolver.Solve(graph, token);
        var initial = new HashSet<int>(greedySol.GetEnumerator());

        CancellationToken ct = token ?? CancellationToken.None;

        var optimized = Optimize(initial, ct);

        var result = new HashSetSolution(graph.getSize());
        foreach (var v in optimized)
            result.AddVertex(v);

        return result;
    }
}