namespace BSC_DS_MP.TestHub;

internal static class ResultFormatter
{
    public static void PrintTable(List<BenchmarkResult> results)
    {
        if (results.Count == 0)
        {
            Console.WriteLine("No results.");
            return;
        }

        int wFile = Math.Max(4, results.Max(r => r.File.Length));
        int wSolver = Math.Max(6, results.Max(r => r.Solver.Length));

        string header = string.Format(
            "{0}  {1}  {2,6}  {3,7}  {4,7}  {5,9}  {6,2}",
            "File".PadRight(wFile),
            "Solver".PadRight(wSolver),
            "Size",
            "Optimal",
            "Gap%",
            "Time",
            "OK");

        Console.WriteLine();
        Console.WriteLine(header);
        Console.WriteLine(new string('-', header.Length));

        foreach (var r in results)
        {
            string optimal = r.OptimalSize.HasValue ? r.OptimalSize.Value.ToString() : "-";
            string gap = r.GapPct.HasValue ? r.GapPct.Value.ToString("F1") + "%" : "-";
            string time = r.Elapsed.TotalSeconds < 1
                ? r.Elapsed.TotalMilliseconds.ToString("F0") + "ms"
                : r.Elapsed.TotalSeconds.ToString("F2") + "s";

            Console.WriteLine(
                "{0}  {1}  {2,6}  {3,7}  {4,7}  {5,9}  {6,2}",
                r.File.PadRight(wFile),
                r.Solver.PadRight(wSolver),
                r.SolutionSize,
                optimal,
                gap,
                time,
                r.Verified ? "Y" : "N");
        }

        Console.WriteLine();
    }

    public static void WriteCsv(List<BenchmarkResult> results, string outputPath)
    {
        using var writer = new StreamWriter(outputPath);
        writer.WriteLine("file,solver,solution_size,optimal_size,gap_pct,elapsed_ms,verified");

        foreach (var r in results)
        {
            writer.WriteLine(string.Join(",",
                r.File,
                r.Solver,
                r.SolutionSize,
                r.OptimalSize?.ToString() ?? "",
                r.GapPct?.ToString("F2") ?? "",
                r.Elapsed.TotalMilliseconds.ToString("F0"),
                r.Verified ? "true" : "false"));
        }

        Console.WriteLine($"Results written to {outputPath}");
    }
}
