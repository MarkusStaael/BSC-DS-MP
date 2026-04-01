using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using ScottPlot;
using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Util;

namespace BSC_DS_MP.Solvers;

public class HybridSimAnneal : ISolver
{
    private readonly int[][] graph;
    private readonly int n;
    private readonly double timeLimitSeconds;
    private readonly string name = "default";

    private readonly int undominatedPenalty;
    private readonly Random rand = new Random();

    private bool[] confChange;

    // Fast candidate structures
    private readonly List<int> candidateAdd = new();
    private readonly List<int> candidateRemove = new();
    private bool[] inAdd;
    private bool[] inRemove;

    public HybridSimAnneal(IGraph g, double timeLimitSeconds, string name)
    {
        this.name = name;
        if (timeLimitSeconds <= 0)
            throw new ArgumentException("Time must be positive");

        n = g.getSize();
        this.timeLimitSeconds = timeLimitSeconds;

        graph = new int[n][];
        for (int i = 0; i < n; i++)
            graph[i] = g.GetEdges(i).ToArray();

        // Balanced penalty (exploration + feasibility)
        undominatedPenalty = Math.Max(2, n / 5);

        confChange = new bool[n];
        inAdd = new bool[n];
        inRemove = new bool[n];
    }

    private int[] ComputeDominated(HashSet<int> D)
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
        if (!inAdd[v]) { candidateAdd.Add(v); inAdd[v] = true; }
        if (!inRemove[v]) { candidateRemove.Add(v); inRemove[v] = true; }

        foreach (var u in graph[v])
        {
            if (!inAdd[u]) { candidateAdd.Add(u); inAdd[u] = true; }
            if (!inRemove[u]) { candidateRemove.Add(u); inRemove[u] = true; }
        }
    }

    private void Add(int v, HashSet<int> D, int[] dom, ref int und)
    {
        if (!D.Add(v)) return;

        if (dom[v] == 0) und--;
        dom[v]++;

        foreach (var u in graph[v])
        {
            if (dom[u] == 0) und--;
            dom[u]++;
        }

        confChange[v] = true;
        foreach (var u in graph[v]) confChange[u] = true;

        UpdateNeighborhood(v);
    }

    private void Remove(int v, HashSet<int> D, int[] dom, ref int und)
    {
        if (!D.Remove(v)) return;

        dom[v]--;
        if (dom[v] == 0) und++;

        foreach (var u in graph[v])
        {
            dom[u]--;
            if (dom[u] == 0) und++;
        }

        confChange[v] = false;
        foreach (var u in graph[v]) confChange[u] = true;

        UpdateNeighborhood(v);
    }

    private int Cost(int size, int und)
        => size + und * undominatedPenalty;

    private int ScoreAdd(int v, int[] dom)
    {
        int score = dom[v] == 0 ? 1 : 0;
        foreach (var u in graph[v])
            if (dom[u] == 0) score++;
        return score;
    }

    private int ScoreRemove(int v, int[] dom)
    {
        int score = dom[v] == 1 ? 1 : 0;
        foreach (var u in graph[v])
            if (dom[u] == 1) score++;
        return score;
    }

    private int PickAdd(HashSet<int> D, int[] dom)
    {
        // 80% greedy, 20% random
        if (rand.NextDouble() < 0.2)
            return rand.Next(n);

        int best = -1, bestScore = -1;

        foreach (var v in candidateAdd)
        {
            if (D.Contains(v) || !confChange[v]) continue;

            int s = ScoreAdd(v, dom);
            if (s > bestScore)
            {
                bestScore = s;
                best = v;
            }
        }

        return best == -1 ? rand.Next(n) : best;
    }

    private int PickRemove(HashSet<int> D, int[] dom)
    {
        if (D.Count == 0) return -1;

        // 20% random removal
        if (rand.NextDouble() < 0.2)
            return D.ElementAt(rand.Next(D.Count));

        int best = -1, bestScore = int.MaxValue;

        foreach (var v in candidateRemove)
        {
            if (!D.Contains(v)) continue;

            int s = ScoreRemove(v, dom);
            if (s < bestScore)
            {
                bestScore = s;
                best = v;
            }
        }

        return best == -1 ? D.ElementAt(rand.Next(D.Count)) : best;
    }

    private void RemoveRedundant(HashSet<int> D, int[] dom, ref int und)
    {
        foreach (var v in D.ToList())
        {
            if (dom[v] <= 1) continue;

            bool redundant = true;
            foreach (var u in graph[v])
                if (dom[u] <= 1)
                {
                    redundant = false;
                    break;
                }

            if (redundant)
                Remove(v, D, dom, ref und);
        }
    }

    private bool TryMove(HashSet<int> D, int[] dom, ref int und, double T)
    {
        int move = rand.Next(10);

        if (move < 5) // add
        {
            int v = PickAdd(D, dom);
            int delta = 1 - ScoreAdd(v, dom) * undominatedPenalty;

            if (delta > 0 && rand.NextDouble() >= Math.Exp(-delta / T))
                return false;

            Add(v, D, dom, ref und);
        }
        else if (move < 8 && D.Count > 0) // remove
        {
            int v = PickRemove(D, dom);
            int delta = -1 + ScoreRemove(v, dom) * undominatedPenalty;

            if (delta > 0 && rand.NextDouble() >= Math.Exp(-delta / T))
                return false;

            Remove(v, D, dom, ref und);
        }
        else // swap
        {
            int r = PickRemove(D, dom);
            int a = PickAdd(D, dom);

            int delta =
                (-1 + ScoreRemove(r, dom) * undominatedPenalty) +
                (1 - ScoreAdd(a, dom) * undominatedPenalty);

            if (delta > 0 && rand.NextDouble() >= Math.Exp(-delta / T))
                return false;

            Remove(r, D, dom, ref und);
            Add(a, D, dom, ref und);
        }

        // occasional cleanup
        if (rand.Next(3) == 0)
            RemoveRedundant(D, dom, ref und);

        return true;
    }

    public HashSet<int> Optimize(HashSet<int> initial, CancellationToken token)
    {
        var current = new HashSet<int>(initial);
        var best = new HashSet<int>(initial);

        int[] dom = ComputeDominated(current);
        int und = dom.Count(x => x == 0);
        int bestCost = Cost(best.Count, und);

        Array.Fill(confChange, true);

   

        foreach (var v in initial)
            UpdateNeighborhood(v);

        double T0 = Math.Max(1, n / 10.0);
        double Tf = 0.01;
        double k = -Math.Log(Tf / T0) / timeLimitSeconds;

         //for plotting
        var size_plot = new List<int>();
        var time_plot = new List<long>();
        var sw = Stopwatch.StartNew();
        double T = T0;

        while (!token.IsCancellationRequested && T > Tf)
        {
            for (int i = 0; i < 10; i++)
            {
                if (!TryMove(current, dom, ref und, T)) continue;

                int c = Cost(current.Count, und);
                if (c < bestCost)
                {
                    bestCost = c;
                    best = new HashSet<int>(current);
                }
            size_plot.Add(current.Count);
            time_plot.Add(sw.ElapsedMilliseconds);
            }

            T = T0 * Math.Exp(-k * (sw.ElapsedMilliseconds / 1000.0));
        }
        int[] xs = size_plot.ToArray();
        long[] ys = time_plot.ToArray();

        var plt = new Plot();
        plt.Add.Scatter(ys, xs);
        double itps = size_plot.Count * 10 / (time_plot[time_plot.Count - 1] / 1000.0); // iterations per second
        plt.Title("Iterations: " + (size_plot.Count * 10) + ". It/s: " + itps);
        plt.SavePng("SAH_" + name + ".png", 1000, 700);

        return best;
    }

    public ISolution Solve(IGraph graph, CancellationToken? token)
    {
        var greedy = new GreedyDecreaseKey().Solve(graph, token);
        var init = new HashSet<int>(greedy.GetEnumerator());

        var resultSet = Optimize(init, token ?? CancellationToken.None);

        var result = new HashSetSolution(graph.getSize());
        foreach (var v in resultSet)
            result.AddVertex(v);

        return result;
    }
}