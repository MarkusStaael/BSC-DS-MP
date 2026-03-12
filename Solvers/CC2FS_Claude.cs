using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Util;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BSC_DS_MP.Solvers;

/// <summary>
/// CC2FS with incremental scoring, BMS selection, timestamp tiebreaking,
/// CSR graph, bool[] ConfChange, frequency smoothing, and stagnation perturbation.
/// </summary>
internal class CC2FS_Claude : ISolver {

    // --- Swap-remove indexed list for O(1) random access + contains ---
    struct SwapList {
        public int[] items;
        public int[] position; // position[v] = index in items, or -1
        public int count;

        public SwapList(int capacity) {
            items = new int[capacity];
            position = new int[capacity];
            count = 0;
            Array.Fill(position, -1);
        }

        public void Add(int v) {
            if (position[v] != -1) return; // already present
            position[v] = count;
            items[count] = v;
            count++;
        }

        public void Remove(int v) {
            int pos = position[v];
            if (pos == -1) return; // not present
            count--;
            int last = items[count];
            items[pos] = last;
            position[last] = pos;
            position[v] = -1;
        }

        public bool Contains(int v) => position[v] != -1;

        public int RandomElement(Random rng) => items[rng.Next(count)];
    }

    // --- Solution state (replaces SimpleSol with swap-remove lists) ---
    int n;
    SwapList solList;
    SwapList uncovList;
    int[] coveredCount;
    int coveredSum;

    // --- Incremental scoring ---
    int[] score;
    int[] freq;
    int[] timestamp;
    int stepCounter;

    // --- Configuration checking ---
    bool[] confChange;

    // --- Graph ---
    CsrGraph csr;
    CsrTwoLevel twoLevel;
    IGraph originalGraph;

    // --- BMS ---
    const int BMS_K = 50;
    Random rng;

    // --- Forbid list (bool[] instead of HashSet for zero-hash O(1)) ---
    bool[] forbid;
    int[] forbidBuf;
    int forbidCount;

    // --- Stagnation detection ---
    int stepsSinceImprovement;
    const int STAGNATION_THRESHOLD = 100000;

    // --- Frequency smoothing ---
    const int SMOOTH_INTERVAL = 100000;
    int smoothCounter;

    public CC2FS_Claude(IGraph graph) {
        this.originalGraph = graph;
        this.n = graph.getSize();
        forbid = new bool[graph.getSize()];
        forbidBuf = new int[graph.getSize()];
        rng = new Random(42);
    }

    public ISolution Solve(IGraph graph, CancellationToken? token) {
        if (token == null) throw new Exception("CC2FS_Claude needs a CancellationToken");

        var size_plot = new List<int>(1024 * 64);
        var time_plot = new List<long>(1024 * 64);

        // --- Phase 4: Build CSR graph ---
        csr = new CsrGraph(graph);
        twoLevel = new CsrTwoLevel(graph, csr);

        // --- Initialize state ---
        n = csr.NodeCount;
        solList = new SwapList(n);
        uncovList = new SwapList(n);
        coveredCount = new int[n];
        coveredSum = 0;

        confChange = new bool[n];
        Array.Fill(confChange, true); // CC2-R1

        freq = new int[n];
        Array.Fill(freq, 1);

        score = new int[n];
        timestamp = new int[n];
        stepCounter = 0;
        smoothCounter = 0;
        stepsSinceImprovement = 0;

        // --- Get initial greedy solution ---
        ISolution init = new GreedyDecreaseKey().Solve(graph.CloneInto(new AdjSetLstGraphFactory()), null);

        // Add initial solution vertices
        foreach (int v in init.GetEnumerator()) {
            SolAddVertex(v);
        }
        // Mark uncovered vertices
        for (int v = 0; v < n; v++) {
            if (coveredCount[v] == 0) {
                uncovList.Add(v);
            }
        }

        // --- Phase 1: Initialize score array from scratch ---
        for (int v = 0; v < n; v++) {
            score[v] = ComputeScoreFromScratch(v);
        }

        // Clone best solution
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
                // Valid solution
                if (solList.count < bestCount) {
                    bestCount = solList.count;
                    bestSolVertices = new int[solList.count];
                    Array.Copy(solList.items, bestSolVertices, solList.count);
                    stepsSinceImprovement = 0;
                }
                int v = GetBestRemoveBMS(useForbidList: false);
                RemoveVertex(v);
            } else {
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

                // Clear forbid flags
                for (int i = 0; i < forbidCount; i++) forbid[forbidBuf[i]] = false;
            }

            stepsSinceImprovement++;

            // --- Phase 5: Perturbation on stagnation ---
            if (stepsSinceImprovement > STAGNATION_THRESHOLD && solList.count > 0) {
                Perturb();
                stepsSinceImprovement = 0;
            }
        }

        // Build return solution
        var ret = new BitArraySolution(n);
        for (int i = 0; i < bestSolVertices.Length; i++) {
            ret.AddVertex(bestSolVertices[i]);
        }

        // Plot
        if (size_plot.Count > 0) {
            long[] ys = time_plot.ToArray();
            int[] xs = size_plot.ToArray();
            var plt = new Plot();
            plt.Add.Scatter(ys, xs);
            double itps = 60.0 * size_plot.Count / ((double)time_plot[time_plot.Count - 1]);
            plt.Title("Iterations: " + size_plot.Count + ". It/s: " + itps);
            plt.SavePng("quickstart.png", 2000, 700);
        }

        return ret;
    }

    // --- Low-level solution manipulation (no score updates) ---
    private void SolAddVertex(int v) {
        solList.Add(v);
        coveredCount[v]++;
        if (coveredCount[v] == 1) {
            coveredSum++;
            uncovList.Remove(v);
        }
        var neighbors = csr.GetNeighbors(v);
        for (int i = 0; i < neighbors.Length; i++) {
            int u = neighbors[i];
            coveredCount[u]++;
            if (coveredCount[u] == 1) {
                coveredSum++;
                uncovList.Remove(u);
            }
        }
    }

    private void SolRemoveVertex(int v) {
        solList.Remove(v);
        coveredCount[v]--;
        if (coveredCount[v] == 0) {
            coveredSum--;
            uncovList.Add(v);
        }
        var neighbors = csr.GetNeighbors(v);
        for (int i = 0; i < neighbors.Length; i++) {
            int u = neighbors[i];
            coveredCount[u]--;
            if (coveredCount[u] == 0) {
                coveredSum--;
                uncovList.Add(u);
            }
        }
    }

    // --- Phase 1: Incremental score maintenance ---

    private void AddVertex(int v) {
        // Capture transitions: coveredCount will increase for v and N(v)
        // We need to check before and after for each affected vertex

        // Before add: snapshot transitions for v and neighbors
        var neighbors = csr.GetNeighbors(v);

        // Apply the add
        SolAddVertex(v);

        // Phase 3: timestamp
        timestamp[v] = stepCounter++;

        // Delta updates for v itself
        // coveredCount[v] just increased
        int ccV = coveredCount[v];
        if (ccV == 1) {
            // 0→1: v became covered
            // All non-solution neighbors of v lose freq[v] from their add-score
            for (int i = 0; i < neighbors.Length; i++) {
                int w = neighbors[i];
                if (!solList.Contains(w)) score[w] -= freq[v];
            }
            if (!solList.Contains(v)) score[v] -= freq[v]; // v is in solution now, but logically...
        } else if (ccV == 2) {
            // 1→2: v became doubly covered
            // Solution neighbors of v: their remove cost increases (less negative = better to remove)
            for (int i = 0; i < neighbors.Length; i++) {
                int w = neighbors[i];
                if (solList.Contains(w)) score[w] += freq[v];
            }
            if (solList.Contains(v)) score[v] += freq[v];
        }

        // Delta updates for each neighbor u of v
        for (int i = 0; i < neighbors.Length; i++) {
            int u = neighbors[i];
            int ccU = coveredCount[u];
            if (ccU == 1) {
                // 0→1: u became covered
                var uNeighbors = csr.GetNeighbors(u);
                for (int j = 0; j < uNeighbors.Length; j++) {
                    int w = uNeighbors[j];
                    if (!solList.Contains(w)) score[w] -= freq[u];
                }
                if (!solList.Contains(u)) score[u] -= freq[u];
            } else if (ccU == 2) {
                // 1→2: u became doubly covered
                var uNeighbors = csr.GetNeighbors(u);
                for (int j = 0; j < uNeighbors.Length; j++) {
                    int w = uNeighbors[j];
                    if (solList.Contains(w)) score[w] += freq[u];
                }
                if (solList.Contains(u)) score[u] += freq[u];
            }
        }

        // v switched roles: recompute its score directly
        score[v] = ComputeScoreFromScratch(v);

        // CC2 updates: set ConfChange true for two-level neighborhood
        var twoLvl = twoLevel.GetNeighbors(v);
        for (int i = 0; i < twoLvl.Length; i++) {
            confChange[twoLvl[i]] = true;
        }

#if DEBUG
        DebugVerifyScores();
#endif
    }

    private void RemoveVertex(int v) {
        var neighbors = csr.GetNeighbors(v);

        // Apply the remove
        SolRemoveVertex(v);
        confChange[v] = false; // CC2 rule

        // Delta updates for v itself
        int ccV = coveredCount[v];
        if (ccV == 0) {
            // 1→0: v became uncovered
            for (int i = 0; i < neighbors.Length; i++) {
                int w = neighbors[i];
                if (!solList.Contains(w)) score[w] += freq[v];
            }
            if (!solList.Contains(v)) score[v] += freq[v];
        } else if (ccV == 1) {
            // 2→1: v became singly covered
            for (int i = 0; i < neighbors.Length; i++) {
                int w = neighbors[i];
                if (solList.Contains(w)) score[w] -= freq[v];
            }
            if (solList.Contains(v)) score[v] -= freq[v];
        }

        // Delta updates for each neighbor u of v
        for (int i = 0; i < neighbors.Length; i++) {
            int u = neighbors[i];
            int ccU = coveredCount[u];
            if (ccU == 0) {
                // 1→0: u became uncovered
                var uNeighbors = csr.GetNeighbors(u);
                for (int j = 0; j < uNeighbors.Length; j++) {
                    int w = uNeighbors[j];
                    if (!solList.Contains(w)) score[w] += freq[u];
                }
                if (!solList.Contains(u)) score[u] += freq[u];
            } else if (ccU == 1) {
                // 2→1: u became singly covered
                var uNeighbors = csr.GetNeighbors(u);
                for (int j = 0; j < uNeighbors.Length; j++) {
                    int w = uNeighbors[j];
                    if (solList.Contains(w)) score[w] -= freq[u];
                }
                if (solList.Contains(u)) score[u] -= freq[u];
            }
        }

        // v switched roles: recompute its score directly
        score[v] = ComputeScoreFromScratch(v);

        // CC2 updates: set ConfChange true for two-level neighborhood
        var twoLvl = twoLevel.GetNeighbors(v);
        for (int i = 0; i < twoLvl.Length; i++) {
            confChange[twoLvl[i]] = true;
        }

#if DEBUG
        DebugVerifyScores();
#endif
    }

    // --- Phase 2: BMS selection ---

    private int GetBestAddBMS() {
        int bestVertex = -1;
        int bestScore = int.MinValue;
        int bestTimestamp = int.MaxValue;

        for (int sample = 0; sample < BMS_K; sample++) {
            if (uncovList.count == 0) break;

            // Pick a random uncovered vertex, consider it and its neighbors
            int uncov = uncovList.RandomElement(rng);

            // Check uncov itself
            if (!solList.Contains(uncov) && confChange[uncov]) {
                int s = score[uncov];
                if (s > bestScore || (s == bestScore && timestamp[uncov] < bestTimestamp)) {
                    bestScore = s;
                    bestVertex = uncov;
                    bestTimestamp = timestamp[uncov];
                }
            }

            // Check neighbors of uncov
            var neighbors = csr.GetNeighbors(uncov);
            for (int i = 0; i < neighbors.Length; i++) {
                int cand = neighbors[i];
                if (!solList.Contains(cand) && confChange[cand]) {
                    int s = score[cand];
                    if (s > bestScore || (s == bestScore && timestamp[cand] < bestTimestamp)) {
                        bestScore = s;
                        bestVertex = cand;
                        bestTimestamp = timestamp[cand];
                    }
                }
            }
        }

        // Fallback: if BMS found nothing (all CC=false), scan for any eligible vertex
        if (bestVertex == -1) {
            for (int v = 0; v < n; v++) {
                if (!solList.Contains(v) && confChange[v]) {
                    int s = score[v];
                    if (s > bestScore || (s == bestScore && timestamp[v] < bestTimestamp)) {
                        bestScore = s;
                        bestVertex = v;
                        bestTimestamp = timestamp[v];
                    }
                }
            }
        }

        // Last resort: ignore ConfChange
        if (bestVertex == -1) {
            for (int v = 0; v < n; v++) {
                if (!solList.Contains(v)) {
                    int s = score[v];
                    if (s > bestScore || (s == bestScore && timestamp[v] < bestTimestamp)) {
                        bestScore = s;
                        bestVertex = v;
                        bestTimestamp = timestamp[v];
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
                bestScore = s;
                bestVertex = cand;
                bestTimestamp = timestamp[cand];
            }
        }

        // Fallback: scan all if BMS found nothing (e.g., everything forbidden)
        if (bestVertex == -1) {
            for (int i = 0; i < solList.count; i++) {
                int cand = solList.items[i];
                if (useForbidList && forbid[cand]) continue;
                int s = score[cand];
                if (s > bestScore || (s == bestScore && timestamp[cand] < bestTimestamp)) {
                    bestScore = s;
                    bestVertex = cand;
                    bestTimestamp = timestamp[cand];
                }
            }
        }

        // If still nothing (all forbidden), pick any
        if (bestVertex == -1 && solList.count > 0) {
            bestVertex = solList.items[0];
        }

        return bestVertex;
    }

    // --- Frequency updates ---

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

    // --- Phase 5: Frequency smoothing ---
    private void SmoothFrequencies() {
        for (int v = 0; v < n; v++) {
            freq[v] = Math.Max(1, freq[v] / 2);
        }
        // Recompute all scores from scratch after smoothing
        for (int v = 0; v < n; v++) {
            score[v] = ComputeScoreFromScratch(v);
        }
    }

    // --- Phase 5: Perturbation on stagnation ---
    private void Perturb() {
        int toRemove = Math.Min(rng.Next(3, 6), solList.count);
        for (int i = 0; i < toRemove; i++) {
            if (solList.count == 0) break;
            int v = solList.RandomElement(rng);
            RemoveVertex(v);
        }
    }

    // --- Score computation from scratch (for init and debug) ---
    private int ComputeScoreFromScratch(int u) {
        if (!solList.Contains(u)) {
            // Add-score: sum of freq[v] for uncovered v in {u} ∪ N(u)
            int sum = 0;
            var neighbors = csr.GetNeighbors(u);
            for (int i = 0; i < neighbors.Length; i++) {
                int v = neighbors[i];
                if (coveredCount[v] == 0)
                    sum += freq[v];
            }
            if (coveredCount[u] == 0)
                sum += freq[u];
            return sum;
        } else {
            // Remove-score: negative sum of freq[v] for singly-covered v in {u} ∪ N(u)
            int sum = 0;
            var neighbors = csr.GetNeighbors(u);
            for (int i = 0; i < neighbors.Length; i++) {
                int v = neighbors[i];
                if (coveredCount[v] == 1)
                    sum -= freq[v];
            }
            if (coveredCount[u] == 1)
                sum -= freq[u];
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
