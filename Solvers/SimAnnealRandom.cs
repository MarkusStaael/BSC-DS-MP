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

public class SimAnnealRandom : ISolver
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

    public SimAnnealRandom(IGraph g, double timeLimitSeconds, string filename)
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
    // Maybe make the penalty proportional to the number of uncovered vertices instead of a fixed large number, so that we can have some intermediate solutions that are not fully dominating,
    // but still better than others.
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

    // 1-to-1 swap: remove worst vertex and add best vertex in a single atomic move
    private void Swap1to1(HashSet<int> D, int[] dominated, ref int undominated)
    {
        if (D.Count == 0)
        {
            int add = PickBestAdd(D, dominated);
            AddVertex(add, D, dominated, ref undominated);
            return;
        }

        int remove = PickWorstRemove(D, dominated);
        RemoveVertex(remove, D, dominated, ref undominated);

        int v = PickBestAdd(D, dominated);
        AddVertex(v, D, dominated, ref undominated);
    }

    //Random move types: add a vertex, remove a vertex, 1-to-1 swap, or 2-move swap.
    private void RandomMove(HashSet<int> D, int[] dominated, ref int undominated)
    {
        int move = rand.Next(10);

        if (move < 4 && D.Count > 0) // Remove: 40% (increased to focus on reduction)
        {
            int remove = PickWorstRemove(D, dominated);
            RemoveVertex(remove, D, dominated, ref undominated);
        }
        else if (move < 5) // Add: 10% (de-emphasized heavily)
        {
            int add = PickBestAdd(D, dominated);
            AddVertex(add, D, dominated, ref undominated);
        }
        else if (move < 8) // 1-to-1 swap: 30%
        {
            Swap1to1(D, dominated, ref undominated);
        }
        else // 2-move swap (remove and add separately): 20%
        {
            if (D.Count > 0)
            {
                int remove = PickWorstRemove(D, dominated);
                RemoveVertex(remove, D, dominated, ref undominated);
            }

            int v = PickBestAdd(D, dominated);
            AddVertex(v, D, dominated, ref undominated);
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


        //Temperature schedule parameters:
        // Start much cooler to force refinement instead of exploration. Non-dominating solutions now cost more.
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
                int oldCost = Cost(current.Count, undominated);

                // record move snapshot
                var snapshot = new HashSet<int>(current);
                var domSnapshot = (int[])dominated.Clone();
                int undSnapshot = undominated;

                RandomMove(current, dominated, ref undominated);

                int newCost = Cost(current.Count, undominated);

                int delta = newCost - oldCost;

                if (delta > 0 && rand.NextDouble() >= Math.Exp(-delta / T))
                {
                    current = snapshot;
                    dominated = domSnapshot;
                    undominated = undSnapshot;
                }
                else
                {
                    if (newCost < Cost(best.Count, bestUndominated))
                    {
                        best = new HashSet<int>(current);
                        bestUndominated = undominated;
                    }
                }

                

            }
            //Console.WriteLine("Temp: " + T );
            size_plot.Add(current.Count);
            time_plot.Add(sw.ElapsedMilliseconds);

            T = initialT * Math.Exp(-k * (sw.ElapsedMilliseconds / 1000.0)); // Newton's law of cooling
        }

        int[] xs = size_plot.ToArray(); 
        long[] ys = time_plot.ToArray(); 

        var plt = new Plot(); 
        plt.Add.Scatter(ys, xs); 
        double itps = size_plot.Count*opsPerT / (time_plot[time_plot.Count - 1] / 1000.0); // iterations per second
        plt.Title("Iterations: " + (size_plot.Count*opsPerT) + ". It/s: " + itps); 
        plt.SavePng("SAR_" + name + ".png", 1000, 700);
        
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