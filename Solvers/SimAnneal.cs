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

    // cost of a solution is the size of D plus a large penalty for each undominated vertex. 
    // Maybe make the penalty proportional to the number of uncovered vertices instead of a fixed large number, so that we can have some intermediate solutions that are not fully dominating,
    // but still better than others.
    private int Cost(HashSet<int> D, int[] dominated)
    {
        int undominated = 0;

        for (int i = 0; i < n; i++)
            if (dominated[i] == 0)
                undominated++;

        return D.Count + undominated * n; // change cost scoring? 
    }

    /* 
        if v is dominated, score if added is 0. If v is not dominated, score if added is 1. 
        For each neighbor u of v, if u is not dominated, score if added increases by 1. 
        
        Note to self: 
        Maybe consider score change instead of absolute score, so for example if v is already dominated but would become dominated by a different vertex in D, 
        that would be a positive score change since it would allow us to remove the other vertex and potentially add another vertex that dominates more uncovered vertices. 

        Would also need to change the remove scoring to consider this. Could also consider a more complex scoring function that takes into account the degree of the vertex and its neighbors, 
        or the number of currently uncovered vertices it would cover, etc.
    */
    private int ScoreIfAdded(int v, int[] dominated)
    {
        int score = dominated[v] == 0 ? 1 : 0;

        foreach (var u in graph[v])
            if (dominated[u] == 0) //change scoring eval to consider creating redundancy and allowing removal of other vertices in D.
                score++;

        return score;
    }

    // score if removed is how many currently covered vertices would become uncovered, so we want to minimize this when picking a vertex to remove. 
    // Note that this can be > 1 if the vertex dominates multiple vertices that are only dominated by it, or if it dominates a vertex that is also in D (which would become uncovered if this vertex is removed).
    private int ScoreIfRemoved(int v, int[] dominated)
    {
        int score = dominated[v] == 1 ? 1 : 0;

        foreach (var u in graph[v])
            if (dominated[u] == 1) //change scoring eval to consider highly dominated vertices or dominating other vertices in D that would become uncovered, etc.
                score++;

        return score;
    }

// Picks the vertex that would cover the most currently uncovered vertices, breaking ties with random choice. Skips currently in D and confChange true.
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

// Picks the vertex that would uncover the fewest currently covered vertices, breaking ties with random choice. Also considers currently in D and confChange true. Maybe change?
    private int PickWorstRemove(HashSet<int> D, int[] dominated)
    {
        int worst = -1;
        int worstScore = int.MaxValue;

        foreach (var v in D)
        {
            int score = ScoreIfRemoved(v, dominated);

            if (score < worstScore) //maybe change to best 
            {
                worstScore = score;
                worst = v;
            }
        }

        return worst;
    }

    // After a move, check if any vertices in D are redundant (dominated count > 1), and if so remove them. Change to only check the affected vertices and their neighbors instead of all of D?
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

    //Random move types: add a vertex, remove a vertex, or swap (remove worst, then add best + 1 random).
    //probabilities: 10%, 60%, 30%. After the move, remove any redundant vertices.
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
        double finalT = 0.01 * (60.0 / timeLimitSeconds);  // adjust time limit scaling. Maybe reheating schedule instead of exponential cooling?

        //maybe remove estimate and just use a fixed alpha or adaptive alpha 
        double movesPerSecond = 500.0;  // Reduced estimate 
        double innerPerOuter = 500.0;
        double outerPerSecond = movesPerSecond / innerPerOuter;
        double numOuter = timeLimitSeconds * outerPerSecond;
        double alpha = Math.Pow(finalT / initialT, 1.0 / numOuter);

        double T = initialT;

        while (!token.IsCancellationRequested)
        {
            for (int i = 0; i < 500 && !token.IsCancellationRequested; i++) // moves before cooling. Maybe change to adaptive based on acceptance rate or time?
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