using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Util;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BSC_DS_MP.Solvers;

/// <summary>
/// CC2FS V2: Same core loop as V1 but with score-guided perturbation,
/// elite solution pool, and restart-from-elite on deep stagnation.
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

    // --- Stagnation ---
    int stepsSinceImprovement;
    int totalStepsSinceBestImproved;
    const int STAGNATION_THRESHOLD = 50_000;
    const int RESTART_THRESHOLD = 500_000;

    const int SMOOTH_INTERVAL = 75_000;
    int smoothCounter;

    // --- Elite pool ---
    const int ELITE_POOL_SIZE = 3;
    int[][] elitePool;
    int[] eliteCounts;
    int eliteUsed;
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
        totalStepsSinceBestImproved = 0;

        elitePool = new int[ELITE_POOL_SIZE][];
        eliteCounts = new int[ELITE_POOL_SIZE];
        eliteUsed = 0;
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
                    totalStepsSinceBestImproved = 0;
                    UpdateElitePool(solList.items, solList.count);
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
            totalStepsSinceBestImproved++;

            // Score-guided perturbation on stagnation
            if (stepsSinceImprovement > STAGNATION_THRESHOLD && solList.count > 0) {
                Perturb();
                stepsSinceImprovement = 0;
            }

            // Restart from elite on deep stagnation
            if (totalStepsSinceBestImproved > RESTART_THRESHOLD && eliteUsed > 0) {
                RestartFromElite();
                stepsSinceImprovement = 0;
                totalStepsSinceBestImproved = 0;
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

    // --- Score-guided perturbation: remove 3-5 easiest-to-remove vertices ---

    private void Perturb() {
        int toRemove = rng.Next(3, 6);
        toRemove = Math.Min(toRemove, solList.count);
        if (toRemove == 0) return;

        // BMS-style sampling: find the vertices with highest score (least costly to remove)
        int[] candidates = new int[toRemove];
        int[] candScores = new int[toRemove];
        Array.Fill(candScores, int.MinValue);

        int sampleSize = Math.Min(BMS_K * 2, solList.count);
        for (int s = 0; s < sampleSize; s++) {
            int v = solList.RandomElement(rng);
            int sc = score[v];
            int worstIdx = 0;
            for (int j = 1; j < toRemove; j++) {
                if (candScores[j] < candScores[worstIdx]) worstIdx = j;
            }
            if (sc > candScores[worstIdx]) {
                candidates[worstIdx] = v;
                candScores[worstIdx] = sc;
            }
        }

        for (int i = 0; i < toRemove; i++) {
            if (candScores[i] != int.MinValue && solList.Contains(candidates[i]))
                RemoveVertex(candidates[i]);
        }
    }

    // --- Elite pool ---

    private void UpdateElitePool(int[] solVertices, int solCount) {
        if (eliteUsed < ELITE_POOL_SIZE) {
            elitePool[eliteUsed] = new int[solCount];
            Array.Copy(solVertices, elitePool[eliteUsed], solCount);
            eliteCounts[eliteUsed] = solCount;
            eliteUsed++;
        } else {
            int worstIdx = 0;
            for (int i = 1; i < ELITE_POOL_SIZE; i++) {
                if (eliteCounts[i] > eliteCounts[worstIdx]) worstIdx = i;
            }
            if (solCount < eliteCounts[worstIdx]) {
                elitePool[worstIdx] = new int[solCount];
                Array.Copy(solVertices, elitePool[worstIdx], solCount);
                eliteCounts[worstIdx] = solCount;
            }
        }
    }

    // --- Restart from a random elite solution ---

    private void RestartFromElite() {
        restartCount++;
        int idx = rng.Next(eliteUsed);

        // Clear current solution
        while (solList.count > 0)
            SolRemoveVertex(solList.items[solList.count - 1]);

        uncovList = new SwapList(n);
        for (int v = 0; v < n; v++)
            if (coveredCount[v] == 0) uncovList.Add(v);

        // Reload elite solution
        for (int i = 0; i < eliteCounts[idx]; i++)
            SolAddVertex(elitePool[idx][i]);

        uncovList = new SwapList(n);
        for (int v = 0; v < n; v++)
            if (coveredCount[v] == 0) uncovList.Add(v);

        // Reset frequencies to break old patterns
        for (int v = 0; v < n; v++)
            freq[v] = Math.Max(1, freq[v] / 4);

        Array.Fill(confChange, true);
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
