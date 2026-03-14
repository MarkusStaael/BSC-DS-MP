# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

C# (.NET 10.0) application for solving the **Minimum Dominating Set Problem** — an NP-hard graph optimization problem. Implements multiple algorithms (greedy baselines, CC2FS local search, simulated annealing) to find minimal dominating sets in undirected graphs.

## Build and Run

```bash
dotnet build
dotnet run
```

Run configuration is controlled by constants in `Program.cs`: `target` selects the input graph file, `timelimit` sets solver time in seconds, `printResult`/`toFile` control output.

## Architecture

**Interface-driven design** with three core abstractions:
- `IGraph` — graph representation (adjacency list, set+list hybrid, CSR)
- `ISolver` — algorithm interface: `Solve(IGraph, CancellationToken?)`
- `ISolution` — solution storage (bit array, hash set, list variants)

**Data flow**: DIMACS `.gr` file → `Reader.DominatingSetReader()` → `IGraph` → `ISolver.Solve()` → `ISolution` → `Verifier.Verify()`

**Key solver implementations**:
- `GreedyDecreaseKey` — greedy baseline using indexed max heap, O(m log n)
- `CC2FS_Claude` — optimized Configuration Checking with 2-level Fast Selection; uses incremental scoring, BMS selection (k=50), frequency-weighted scoring, stagnation-based perturbation, and swap-remove lists for O(1) operations
- `SimAnneal` — simulated annealing with adaptive temperature schedule and candidate frontier sets

**Graph representations** in `DataStructures/Graph/`:
- `AdjSetLstGraph` — hybrid adjacency set+list (default)
- `CsrGraph` — Compressed Sparse Row for cache-efficient neighbor iteration via `ReadOnlySpan<int>`

**Visualization**: ScottPlot 5.x generates PNG plots of solution quality over time.

## Input Format (DIMACS)

```
p ds <vertices> <edges>
v1 v2
```
Files are 1-indexed; the parser converts to 0-indexed. Test data lives in `Data/`.

## Testing

No unit test framework — correctness is verified via `Verifier.Verify()` which checks that every vertex is either in the solution or adjacent to a solution vertex. `Program.cs` acts as a test harness running multiple solvers on a selected graph and comparing results.

## Key Performance Patterns

- CSR graph uses `ReadOnlySpan<int>` for zero-allocation neighbor iteration
- CC2FS_Claude maintains scores incrementally rather than recomputing
- SwapList (in Util) provides O(1) add/remove/contains/random-access
- Unsafe blocks are enabled in the project for potential optimizations
- `#if DEBUG` assertions in CC2FS_Claude verify score consistency
