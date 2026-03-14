using System;
using System.Collections.Generic;
using System.Threading;
using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Solvers;
using BSC_DS_MP.Util;
using ScottPlot;
using System.Diagnostics;

namespace BSC_DS_MP.Solvers;

public class SimAnneal : ISolver
{
    // A3: CSR graph for cache-friendly neighbor iteration (replaces List<HashSet<int>>)
    private readonly CsrGraph csr;
    private readonly int n;
    private readonly double timeLimitSeconds;
    private readonly Random rand = new Random();

    private bool[] confChange;

    // A4: SwapList for O(1) random access + contains on solution set
    private SwapList solution;

    // Incremental domination tracking
    private int[] dominated;
    private int undominated;

    // A1: Move log for undo-based moves (replaces O(n) snapshot cloning)
    private readonly List<(int vertex, bool wasAdd)> moveLog = new(32);

    // A5: Candidate frontier sets with periodic pruning
    private HashSet<int> candidateAdd = new();
    private HashSet<int> candidateRemove = new();

    // Temp buffer for RemoveRedundant snapshot
    private int[] tempSnapshot = Array.Empty<int>();

    struct SwapList
    {
        public int[] items;
        public int[] position;
        public int count;

        public SwapList(int capacity)
        {
            items = new int[capacity];
            position = new int[capacity];
            count = 0;
            Array.Fill(position, -1);
        }

        public bool Add(int v)
        {
            if (position[v] != -1) return false;
            position[v] = count;
            items[count] = v;
            count++;
            return true;
        }

        public bool Remove(int v)
        {
            int pos = position[v];
            if (pos == -1) return false;
            count--;
            int last = items[count];
            items[pos] = last;
            position[last] = pos;
            position[v] = -1;
            return true;
        }

        public bool Contains(int v) => position[v] != -1;
        public int RandomElement(Random rng) => items[rng.Next(count)];
    }

    public SimAnneal(IGraph g, double timeLimitSeconds)
    {
        if (timeLimitSeconds <= 0)
            throw new ArgumentException("Time limit must be positive", nameof(timeLimitSeconds));

        n = g.getSize();
        this.timeLimitSeconds = timeLimitSeconds;
        csr = new CsrGraph(g);
    }

    private void InitDominatedCount()
    {
        dominated = new int[n];
        undominated = 0;

        for (int i = 0; i < solution.count; i++)
        {
            int v = solution.items[i];
            dominated[v]++;
            var neighbors = csr.GetNeighbors(v);
            for (int j = 0; j < neighbors.Length; j++)
                dominated[neighbors[j]]++;
        }

        for (int i = 0; i < n; i++)
            if (dominated[i] == 0) undominated++;
    }

    private void UpdateNeighborhood(int v)
    {
        candidateAdd.Add(v);
        candidateRemove.Add(v);
        var neighbors = csr.GetNeighbors(v);
        for (int i = 0; i < neighbors.Length; i++)
        {
            candidateAdd.Add(neighbors[i]);
            candidateRemove.Add(neighbors[i]);
        }
    }

    // Full add: updates solution, dominated, confChange, candidates, and logs for undo
    private void AddVertex(int v)
    {
        if (!solution.Add(v)) return;
        moveLog.Add((v, true));

        if (dominated[v] == 0) undominated--;
        dominated[v]++;

        var neighbors = csr.GetNeighbors(v);
        for (int i = 0; i < neighbors.Length; i++)
        {
            int u = neighbors[i];
            if (dominated[u] == 0) undominated--;
            dominated[u]++;
        }

        confChange[v] = true;
        for (int i = 0; i < neighbors.Length; i++)
            confChange[neighbors[i]] = true;

        UpdateNeighborhood(v);
    }

    // Full remove: updates solution, dominated, confChange, candidates, and logs for undo
    private void RemoveVertex(int v)
    {
        if (!solution.Remove(v)) return;
        moveLog.Add((v, false));

        dominated[v]--;
        if (dominated[v] == 0) undominated++;

        var neighbors = csr.GetNeighbors(v);
        for (int i = 0; i < neighbors.Length; i++)
        {
            int u = neighbors[i];
            dominated[u]--;
            if (dominated[u] == 0) undominated++;
        }

        confChange[v] = false;
        for (int i = 0; i < neighbors.Length; i++)
            confChange[neighbors[i]] = true;

        UpdateNeighborhood(v);
    }

    // A1: Raw undo operations — restore solution/dominated/undominated only,
    // leave confChange and candidates unchanged (matches original snapshot behavior)
    private void RawAddBack(int v)
    {
        solution.Add(v);
        if (dominated[v] == 0) undominated--;
        dominated[v]++;
        var neighbors = csr.GetNeighbors(v);
        for (int i = 0; i < neighbors.Length; i++)
        {
            int u = neighbors[i];
            if (dominated[u] == 0) undominated--;
            dominated[u]++;
        }
    }

    private void RawRemoveBack(int v)
    {
        solution.Remove(v);
        dominated[v]--;
        if (dominated[v] == 0) undominated++;
        var neighbors = csr.GetNeighbors(v);
        for (int i = 0; i < neighbors.Length; i++)
        {
            int u = neighbors[i];
            dominated[u]--;
            if (dominated[u] == 0) undominated++;
        }
    }

    private void UndoMove()
    {
        for (int i = moveLog.Count - 1; i >= 0; i--)
        {
            var (vertex, wasAdd) = moveLog[i];
            if (wasAdd)
                RawRemoveBack(vertex);
            else
                RawAddBack(vertex);
        }
    }

    private int Cost(int setSize, int undominatedCount)
    {
        return setSize + undominatedCount * n;
    }

    private int ScoreIfAdded(int v)
    {
        int score = dominated[v] == 0 ? 1 : 0;
        var neighbors = csr.GetNeighbors(v);
        for (int i = 0; i < neighbors.Length; i++)
            if (dominated[neighbors[i]] == 0) score++;
        return score;
    }

    private int ScoreIfRemoved(int v)
    {
        int score = dominated[v] == 1 ? 1 : 0;
        var neighbors = csr.GetNeighbors(v);
        for (int i = 0; i < neighbors.Length; i++)
            if (dominated[neighbors[i]] == 1) score++;
        return score;
    }

    private int PickBestAdd()
    {
        int best = -1;
        int bestScore = -1;

        foreach (var v in candidateAdd)
        {
            if (solution.Contains(v)) continue;
            if (!confChange[v]) continue;
            int score = ScoreIfAdded(v);
            if (score > bestScore)
            {
                bestScore = score;
                best = v;
            }
        }

        if (best == -1) best = rand.Next(n);
        return best;
    }

    private int PickWorstRemove()
    {
        int worst = -1;
        int worstScore = int.MaxValue;

        foreach (var v in candidateRemove)
        {
            if (!solution.Contains(v)) continue;
            int score = ScoreIfRemoved(v);
            if (score < worstScore)
            {
                worstScore = score;
                worst = v;
            }
        }

        // A4: O(1) random element from SwapList instead of O(n) HashSet.ElementAt
        if (worst == -1 && solution.count > 0)
            worst = solution.RandomElement(rand);

        return worst;
    }

    private void RemoveRedundant()
    {
        // Snapshot current solution to iterate safely while modifying
        int snapCount = solution.count;
        if (snapCount > tempSnapshot.Length)
            tempSnapshot = new int[snapCount * 2];
        Array.Copy(solution.items, tempSnapshot, snapCount);

        for (int idx = 0; idx < snapCount; idx++)
        {
            int v = tempSnapshot[idx];
            if (!solution.Contains(v)) continue; // already removed in this pass

            if (dominated[v] <= 1) continue;

            bool redundant = true;
            var neighbors = csr.GetNeighbors(v);
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (dominated[neighbors[i]] <= 1)
                {
                    redundant = false;
                    break;
                }
            }

            if (redundant)
                RemoveVertex(v);
        }
    }

    private void RandomMove()
    {
        int move = rand.Next(10);

        if (move < 1 && solution.count > 0)
        {
            int remove = PickWorstRemove();
            RemoveVertex(remove);
        }
        else if (move < 7)
        {
            int add = PickBestAdd();
            AddVertex(add);
        }
        else
        {
            int remove = PickWorstRemove();
            RemoveVertex(remove);
            int v = PickBestAdd();
            AddVertex(v);
            int w = rand.Next(n);
            AddVertex(w);
        }

        RemoveRedundant();
    }

    // A5: Rebuild candidate sets from current solution neighborhood
    private void RebuildCandidates()
    {
        candidateAdd.Clear();
        candidateRemove.Clear();
        for (int i = 0; i < solution.count; i++)
        {
            int v = solution.items[i];
            candidateRemove.Add(v);
            var neighbors = csr.GetNeighbors(v);
            for (int j = 0; j < neighbors.Length; j++)
                candidateAdd.Add(neighbors[j]);
        }
    }

    public HashSet<int> Optimize(HashSet<int> greedy, CancellationToken token)
    {
        // Initialize solution SwapList from greedy
        solution = new SwapList(n);
        foreach (var v in greedy)
            solution.Add(v);

        InitDominatedCount();

        confChange = new bool[n];
        Array.Fill(confChange, true);

        // Initialize candidate frontier
        RebuildCandidates();

        // Temperature schedule
        double initialT = n;
        double finalT = 0.01 * (60.0 / timeLimitSeconds);
        double movesPerSecond = 20000.0;
        double opsPerT = 500.0;
        double outerPerSecond = movesPerSecond / opsPerT;
        double numOuter = timeLimitSeconds * outerPerSecond;
        double alpha = Math.Pow(finalT / initialT, 1.0 / numOuter);
        double T = initialT;

        // A2: Track best cost as an int (no recomputation)
        int bestCost = Cost(solution.count, undominated);
        int bestVertexCount = solution.count;
        int[] bestVertices = new int[bestVertexCount];
        Array.Copy(solution.items, bestVertices, bestVertexCount);

        // Plotting
        var size_plot = new List<int>();
        var time_plot = new List<long>();
        var sw = Stopwatch.StartNew();

        int outerIter = 0;
        while (!token.IsCancellationRequested)
        {
            // A5: Prune unbounded candidate sets periodically
            if (outerIter % 100 == 0 && outerIter > 0)
                RebuildCandidates();

            outerIter++;

            for (int i = 0; i < opsPerT && !token.IsCancellationRequested; i++)
            {
                int oldCost = Cost(solution.count, undominated);

                // A1: Log moves instead of cloning O(n) snapshot
                moveLog.Clear();
                RandomMove();

                int newCost = Cost(solution.count, undominated);
                int delta = newCost - oldCost;

                if (delta > 0 && rand.NextDouble() >= Math.Exp(-delta / T))
                {
                    // Reject move: undo in O(sum of degrees) instead of O(n) clone restore
                    UndoMove();
                }
                else
                {
                    // Accept: check if new best (A2: O(1) comparison vs old O(n+m))
                    if (newCost < bestCost)
                    {
                        bestCost = newCost;
                        bestVertexCount = solution.count;
                        if (bestVertices.Length < bestVertexCount)
                            bestVertices = new int[bestVertexCount];
                        Array.Copy(solution.items, bestVertices, bestVertexCount);
                    }
                }

                size_plot.Add(solution.count);
                time_plot.Add(sw.ElapsedMilliseconds);
            }

            T *= alpha;
        }

        // Build result
        var best = new HashSet<int>(bestVertexCount);
        for (int i = 0; i < bestVertexCount; i++)
            best.Add(bestVertices[i]);

        // Plot
        int[] xs = size_plot.ToArray();
        long[] ys = time_plot.ToArray();
        var plt = new Plot();
        plt.Add.Scatter(ys, xs);
        double itps = size_plot.Count / (time_plot[time_plot.Count - 1] / 1000.0);
        plt.Title("Iterations: " + size_plot.Count + ". It/s: " + itps);
        plt.SavePng("SimulatedAnnealing.png", 1000, 700);

        return best;
    }

    public ISolution Solve(IGraph graph, CancellationToken? token)
    {
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
