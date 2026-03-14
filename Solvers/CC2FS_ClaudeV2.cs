using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using BSC_DS_MP.Util;
using ScottPlot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace BSC_DS_MP.Solvers;

/// <summary>
/// CC2FS V2: Same core loop as V1 but with progressive perturbation
/// (escalating disruption on stagnation) and randomized restarts with
/// history-based diversity penalties.
/// </summary>
internal class CC2FS_ClaudeV2 : ISolver {

    struct SwapList {
        public int[] items;
        public int[] position;
        public int count;

        public SwapList(int capacity) {
            items = new int[capacity];
            position = new int[capacity];
            count = 0;
            Array.Fill(position, -1);
        }

        public void Add(int v) {
            if (position[v] != -1) return;
            position[v] = count;
            items[count] = v;
            count++;
        }

        public void Remove(int v) {
            int pos = position[v];
            if (pos == -1) return;
            count--;
            int last = items[count];
            items[pos] = last;
            position[last] = pos;
            position[v] = -1;
        }

        public bool Contains(int v) => position[v] != -1;
        public int RandomElement(Random rng) => items[rng.Next(count)];
    }

    int n;
    SwapList solList;
    SwapList uncovList;
    int[] coveredCount;
    int coveredSum;

    int[] score;
    int[] freq;
    int[] timestamp;
    int stepCounter;

    bool[] confChange;

    CsrGraph csr;
    CsrTwoLevel twoLevel;
    IGraph originalGraph;

    const int BMS_K = 50;
    Random rng;

    // --- Forbid list (same as V1) ---
    bool[] forbid;
    int[] forbidBuf;
    int forbidCount;

    // --- Stagnation / progressive perturbation ---
    int stepsSinceImprovement;
    int perturbLevel;
    static readonly int[] STAGNATION_THRESHOLDS = { 100_000, 300_000, 700_000, 1_500_000 };

    const int SMOOTH_INTERVAL = 100_000;
    int smoothCounter;

    // --- Restart history ---
    int[] vertexAppearCount;
    int restartCount;

    private readonly bool useOneLevelCC;
    private readonly string plotFile;

    public CC2FS_ClaudeV2(IGraph graph, int seed = 42, bool useOneLevelCC = false, string plotFile = "ClaudeV2.png") {
        this.originalGraph = graph;
        this.n = graph.getSize();
        forbid = new bool[graph.getSize()];
        forbidBuf = new int[graph.getSize()];
        rng = new Random(seed);
        this.useOneLevelCC = useOneLevelCC;
        this.plotFile = plotFile;
    }

    public ISolution Solve(IGraph graph, CancellationToken? token) {
        if (token == null) throw new Exception("CC2FS_ClaudeV2 needs a CancellationToken");

        var size_plot = new List<int>(1024 * 64);
        var time_plot = new List<long>(1024 * 64);

        csr = new CsrGraph(graph);
        if (!useOneLevelCC)
            twoLevel = new CsrTwoLevel(graph, csr);

        n = csr.NodeCount;
        solList = new SwapList(n);
        uncovList = new SwapList(n);
        coveredCount = new int[n];
        coveredSum = 0;

        confChange = new bool[n];
        Array.Fill(confChange, true);

        freq = new int[n];
        Array.Fill(freq, 1);

        score = new int[n];
        timestamp = new int[n];
        stepCounter = 0;
        smoothCounter = 0;
        stepsSinceImprovement = 0;
        perturbLevel = 0;

        vertexAppearCount = new int[n];
        restartCount = 0;

        ISolution init = new GreedyDecreaseKey().Solve(graph, null);
        foreach (int v in init.GetEnumerator()) {
            SolAddVertex(v);
        }
        for (int v = 0; v < n; v++) {
            if (coveredCount[v] == 0)
                uncovList.Add(v);
        }

        for (int v = 0; v < n; v++) {
            score[v] = ComputeScoreFromScratch(v);
        }

        int bestCount = solList.count;
        int[] bestSolVertices = new int[solList.count];
        Array.Copy(solList.items, bestSolVertices, solList.count);

        var sw = Stopwatch.StartNew();

        int iterCount = 0;
        while (!((CancellationToken)token).IsCancellationRequested) {
            if (iterCount % 10 == 0) {
                size_plot.Add(solList.count);
                time_plot.Add(sw.ElapsedMilliseconds);
            }
            iterCount++;

            if (coveredSum == n) {
                if (solList.count < bestCount) {
                    bestCount = solList.count;
                    bestSolVertices = new int[solList.count];
                    Array.Copy(solList.items, bestSolVertices, solList.count);
                    stepsSinceImprovement = 0;
                    perturbLevel = 0;

                    for (int i = 0; i < solList.count; i++)
                        vertexAppearCount[solList.items[i]]++;
                }
                int v = GetBestRemoveBMS(useForbidList: false);
                RemoveVertex(v);
            } else {
                // Same forbid-based repair loop as V1
                int v = GetBestRemoveBMS(useForbidList: true);
                RemoveVertex(v);
                forbidCount = 0;

                while (coveredSum != n && !((CancellationToken)token).IsCancellationRequested) {
                    v = GetBestAddBMS();
                    AddVertex(v);
                    forbid[v] = true;
                    forbidBuf[forbidCount++] = v;
                    IncreaseFreq();
                }

                for (int i = 0; i < forbidCount; i++) forbid[forbidBuf[i]] = false;
            }

            stepsSinceImprovement++;

            // Progressive perturbation (V2 improvement over V1's fixed perturbation)
            if (perturbLevel < STAGNATION_THRESHOLDS.Length &&
                stepsSinceImprovement > STAGNATION_THRESHOLDS[perturbLevel] &&
                solList.count > 0) {
                if (perturbLevel < 3) {
                    Perturb(perturbLevel + 1);
                    perturbLevel++;
                    stepsSinceImprovement = 0;
                } else {
                    TriggerRestart(graph);
                    perturbLevel = 0;
                    stepsSinceImprovement = 0;
                }
            }
        }

        Console.WriteLine($"[V2 Stats] OuterIter: {iterCount}, Restarts: {restartCount}");

        var ret = new BitArraySolution(n);
        for (int i = 0; i < bestSolVertices.Length; i++) {
            ret.AddVertex(bestSolVertices[i]);
        }

        if (size_plot.Count > 0) {
            long[] ys = time_plot.ToArray();
            int[] xs = size_plot.ToArray();
            var plt = new Plot();
            plt.Add.Scatter(ys, xs);
            double itps = 60.0 * size_plot.Count / ((double)time_plot[time_plot.Count - 1]);
            plt.Title("V2 Iterations: " + size_plot.Count + ". It/s: " + itps);
            plt.SavePng(plotFile, 2000, 700);
        }

        return ret;
    }

    // --- V1-identical core methods ---

    private void SolAddVertex(int v) {
        solList.Add(v);
        coveredCount[v]++;
        if (coveredCount[v] == 1) { coveredSum++; uncovList.Remove(v); }
        var neighbors = csr.GetNeighbors(v);
        for (int i = 0; i < neighbors.Length; i++) {
            int u = neighbors[i];
            coveredCount[u]++;
            if (coveredCount[u] == 1) { coveredSum++; uncovList.Remove(u); }
        }
    }

    private void SolRemoveVertex(int v) {
        solList.Remove(v);
        coveredCount[v]--;
        if (coveredCount[v] == 0) { coveredSum--; uncovList.Add(v); }
        var neighbors = csr.GetNeighbors(v);
        for (int i = 0; i < neighbors.Length; i++) {
            int u = neighbors[i];
            coveredCount[u]--;
            if (coveredCount[u] == 0) { coveredSum--; uncovList.Add(u); }
        }
    }

    private void AddVertex(int v) {
        var neighbors = csr.GetNeighbors(v);
        SolAddVertex(v);
        timestamp[v] = stepCounter++;

        int ccV = coveredCount[v];
        if (ccV == 1) {
            for (int i = 0; i < neighbors.Length; i++) {
                int w = neighbors[i];
                if (!solList.Contains(w)) score[w] -= freq[v];
            }
            if (!solList.Contains(v)) score[v] -= freq[v];
        } else if (ccV == 2) {
            for (int i = 0; i < neighbors.Length; i++) {
                int w = neighbors[i];
                if (solList.Contains(w)) score[w] += freq[v];
            }
            if (solList.Contains(v)) score[v] += freq[v];
        }

        for (int i = 0; i < neighbors.Length; i++) {
            int u = neighbors[i];
            int ccU = coveredCount[u];
            if (ccU == 1) {
                var uNeighbors = csr.GetNeighbors(u);
                for (int j = 0; j < uNeighbors.Length; j++) {
                    int w = uNeighbors[j];
                    if (!solList.Contains(w)) score[w] -= freq[u];
                }
                if (!solList.Contains(u)) score[u] -= freq[u];
            } else if (ccU == 2) {
                var uNeighbors = csr.GetNeighbors(u);
                for (int j = 0; j < uNeighbors.Length; j++) {
                    int w = uNeighbors[j];
                    if (solList.Contains(w)) score[w] += freq[u];
                }
                if (solList.Contains(u)) score[u] += freq[u];
            }
        }

        score[v] = ComputeScoreFromScratch(v);

        if (useOneLevelCC) {
            for (int i = 0; i < neighbors.Length; i++)
                confChange[neighbors[i]] = true;
        } else {
            var twoLvl = twoLevel.GetNeighbors(v);
            for (int i = 0; i < twoLvl.Length; i++)
                confChange[twoLvl[i]] = true;
        }

#if DEBUG
        DebugVerifyScores();
#endif
    }

    private void RemoveVertex(int v) {
        var neighbors = csr.GetNeighbors(v);
        SolRemoveVertex(v);
        confChange[v] = false;

        int ccV = coveredCount[v];
        if (ccV == 0) {
            for (int i = 0; i < neighbors.Length; i++) {
                int w = neighbors[i];
                if (!solList.Contains(w)) score[w] += freq[v];
            }
            if (!solList.Contains(v)) score[v] += freq[v];
        } else if (ccV == 1) {
            for (int i = 0; i < neighbors.Length; i++) {
                int w = neighbors[i];
                if (solList.Contains(w)) score[w] -= freq[v];
            }
            if (solList.Contains(v)) score[v] -= freq[v];
        }

        for (int i = 0; i < neighbors.Length; i++) {
            int u = neighbors[i];
            int ccU = coveredCount[u];
            if (ccU == 0) {
                var uNeighbors = csr.GetNeighbors(u);
                for (int j = 0; j < uNeighbors.Length; j++) {
                    int w = uNeighbors[j];
                    if (!solList.Contains(w)) score[w] += freq[u];
                }
                if (!solList.Contains(u)) score[u] += freq[u];
            } else if (ccU == 1) {
                var uNeighbors = csr.GetNeighbors(u);
                for (int j = 0; j < uNeighbors.Length; j++) {
                    int w = uNeighbors[j];
                    if (solList.Contains(w)) score[w] -= freq[u];
                }
                if (solList.Contains(u)) score[u] -= freq[u];
            }
        }

        score[v] = ComputeScoreFromScratch(v);

        if (useOneLevelCC) {
            for (int i = 0; i < neighbors.Length; i++)
                confChange[neighbors[i]] = true;
        } else {
            var twoLvl = twoLevel.GetNeighbors(v);
            for (int i = 0; i < twoLvl.Length; i++)
                confChange[twoLvl[i]] = true;
        }

#if DEBUG
        DebugVerifyScores();
#endif
    }

    private int GetBestAddBMS() {
        int bestVertex = -1;
        int bestScore = int.MinValue;
        int bestTimestamp = int.MaxValue;

        for (int sample = 0; sample < BMS_K; sample++) {
            if (uncovList.count == 0) break;
            int uncov = uncovList.RandomElement(rng);

            if (!solList.Contains(uncov) && confChange[uncov]) {
                int s = score[uncov];
                if (s > bestScore || (s == bestScore && timestamp[uncov] < bestTimestamp)) {
                    bestScore = s; bestVertex = uncov; bestTimestamp = timestamp[uncov];
                }
            }

            var neighbors = csr.GetNeighbors(uncov);
            for (int i = 0; i < neighbors.Length; i++) {
                int cand = neighbors[i];
                if (!solList.Contains(cand) && confChange[cand]) {
                    int s = score[cand];
                    if (s > bestScore || (s == bestScore && timestamp[cand] < bestTimestamp)) {
                        bestScore = s; bestVertex = cand; bestTimestamp = timestamp[cand];
                    }
                }
            }
        }

        if (bestVertex == -1) {
            for (int v = 0; v < n; v++) {
                if (!solList.Contains(v) && confChange[v]) {
                    int s = score[v];
                    if (s > bestScore || (s == bestScore && timestamp[v] < bestTimestamp)) {
                        bestScore = s; bestVertex = v; bestTimestamp = timestamp[v];
                    }
                }
            }
        }
        if (bestVertex == -1) {
            for (int v = 0; v < n; v++) {
                if (!solList.Contains(v)) {
                    int s = score[v];
                    if (s > bestScore || (s == bestScore && timestamp[v] < bestTimestamp)) {
                        bestScore = s; bestVertex = v; bestTimestamp = timestamp[v];
                    }
                }
            }
        }
        return bestVertex;
    }

    private int GetBestRemoveBMS(bool useForbidList) {
        int bestVertex = -1;
        int bestScore = int.MinValue;
        int bestTimestamp = int.MaxValue;
        if (solList.count == 0) return -1;

        for (int sample = 0; sample < BMS_K; sample++) {
            int cand = solList.RandomElement(rng);
            if (useForbidList && forbid[cand]) continue;
            int s = score[cand];
            if (s > bestScore || (s == bestScore && timestamp[cand] < bestTimestamp)) {
                bestScore = s; bestVertex = cand; bestTimestamp = timestamp[cand];
            }
        }

        if (bestVertex == -1) {
            for (int i = 0; i < solList.count; i++) {
                int cand = solList.items[i];
                if (useForbidList && forbid[cand]) continue;
                int s = score[cand];
                if (s > bestScore || (s == bestScore && timestamp[cand] < bestTimestamp)) {
                    bestScore = s; bestVertex = cand; bestTimestamp = timestamp[cand];
                }
            }
        }
        if (bestVertex == -1 && solList.count > 0) bestVertex = solList.items[0];
        return bestVertex;
    }

    private void IncreaseFreq() {
        for (int i = 0; i < uncovList.count; i++) {
            int v = uncovList.items[i];
            freq[v]++;
            var neighbors = csr.GetNeighbors(v);
            for (int j = 0; j < neighbors.Length; j++) {
                int w = neighbors[j];
                if (!solList.Contains(w)) score[w] += 1;
            }
            if (!solList.Contains(v)) score[v] += 1;
        }
        smoothCounter++;
        if (smoothCounter >= SMOOTH_INTERVAL) {
            smoothCounter = 0;
            SmoothFrequencies();
        }
    }

    private void SmoothFrequencies() {
        for (int v = 0; v < n; v++) freq[v] = Math.Max(1, freq[v] / 2);
        for (int v = 0; v < n; v++) score[v] = ComputeScoreFromScratch(v);
    }

    // --- V2-specific: Progressive perturbation ---

    private void Perturb(int intensity) {
        if (solList.count == 0) return;
        switch (intensity) {
            case 1: PerturbRandom(rng.Next(3, 8)); break;           // mild: similar to V1
            case 2: PerturbHighFreq(rng.Next(10, 30)); break;      // medium: targeted
            case 3: PerturbBfsCluster(rng.Next(30, 100)); break;   // strong: structural
        }
    }

    private void PerturbRandom(int count) {
        count = Math.Min(count, solList.count);
        for (int i = 0; i < count; i++) {
            if (solList.count == 0) break;
            RemoveVertex(solList.RandomElement(rng));
        }
    }

    private void PerturbHighFreq(int count) {
        count = Math.Min(count, solList.count);
        var candidates = new int[solList.count];
        Array.Copy(solList.items, candidates, solList.count);
        int candCount = solList.count;
        Array.Sort(candidates, 0, candCount, Comparer<int>.Create((a, b) => freq[b].CompareTo(freq[a])));
        for (int i = 0; i < count && i < candCount; i++) {
            if (solList.Contains(candidates[i]))
                RemoveVertex(candidates[i]);
        }
    }

    private void PerturbBfsCluster(int count) {
        if (solList.count == 0) return;
        count = Math.Min(count, solList.count);
        int seed = solList.RandomElement(rng);
        var queue = new Queue<int>();
        var visited = new HashSet<int>();
        queue.Enqueue(seed);
        visited.Add(seed);
        var toRemove = new List<int>(count);
        while (queue.Count > 0 && toRemove.Count < count) {
            int v = queue.Dequeue();
            toRemove.Add(v);
            var neighbors = csr.GetNeighbors(v);
            for (int i = 0; i < neighbors.Length; i++) {
                int u = neighbors[i];
                if (solList.Contains(u) && !visited.Contains(u)) {
                    visited.Add(u);
                    queue.Enqueue(u);
                }
            }
        }
        foreach (int v in toRemove)
            if (solList.Contains(v)) RemoveVertex(v);
    }

    // --- V2-specific: Randomized restart with diversity ---

    private void TriggerRestart(IGraph graph) {
        restartCount++;

        while (solList.count > 0)
            SolRemoveVertex(solList.items[solList.count - 1]);

        uncovList = new SwapList(n);
        for (int v = 0; v < n; v++)
            if (coveredCount[v] == 0) uncovList.Add(v);

        var covered = new BitArray(n, false);
        int covCount = 0;
        var heap = new IndexedMaxHeap(n);
        double diversityWeight = Math.Min(restartCount * 0.5, 5.0);
        int noiseRange = Math.Max(1, n / 1000);

        for (int v = 0; v < n; v++) {
            int deg = csr.GetNeighbors(v).Length + 1;
            int penalty = (int)(diversityWeight * vertexAppearCount[v]);
            int noise = rng.Next(0, noiseRange);
            heap.Insert(v, Math.Max(0, deg - penalty + noise));
        }

        while (!heap.IsEmpty() && covCount < n) {
            int sel = heap.RemoveMax();
            if (covered[sel]) continue;
            SolAddVertex(sel);
            covered[sel] = true;
            covCount++;
            var neighbors = csr.GetNeighbors(sel);
            for (int i = 0; i < neighbors.Length; i++) {
                int nbr = neighbors[i];
                if (!covered[nbr]) { covered[nbr] = true; covCount++; }
            }
        }

        Array.Fill(confChange, true);
        Array.Fill(freq, 1);
        smoothCounter = 0;
        for (int v = 0; v < n; v++)
            score[v] = ComputeScoreFromScratch(v);
    }

    private int ComputeScoreFromScratch(int u) {
        if (!solList.Contains(u)) {
            int sum = 0;
            var neighbors = csr.GetNeighbors(u);
            for (int i = 0; i < neighbors.Length; i++) {
                if (coveredCount[neighbors[i]] == 0) sum += freq[neighbors[i]];
            }
            if (coveredCount[u] == 0) sum += freq[u];
            return sum;
        } else {
            int sum = 0;
            var neighbors = csr.GetNeighbors(u);
            for (int i = 0; i < neighbors.Length; i++) {
                if (coveredCount[neighbors[i]] == 1) sum -= freq[neighbors[i]];
            }
            if (coveredCount[u] == 1) sum -= freq[u];
            return sum;
        }
    }

#if DEBUG
    private void DebugVerifyScores() {
        if (stepCounter % 100 != 0) return;
        for (int v = 0; v < n; v++) {
            int expected = ComputeScoreFromScratch(v);
            Debug.Assert(score[v] == expected,
                $"Score mismatch at vertex {v}: incremental={score[v]}, expected={expected}, inSol={solList.Contains(v)}, cc={coveredCount[v]}");
        }
    }
#endif
}
