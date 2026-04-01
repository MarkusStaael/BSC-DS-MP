using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Reading;
using BSC_DS_MP.Solvers;
using BSC_DS_MP.Util;
using BSC_DS_MP.Verifier;
using System.Diagnostics;

// EDIT HERE FOR SIMPLE TESTING
bool printResult = false;
bool toFile = false;
string[] files = { "test.gr", "30z50.gr", "heuristic_001.gr", "bremen_subgraph_300.gr" };
int target = 2;
int timelimit = 1200; // seconds

string targetTest = files[target];
string projroot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..");
string path = Path.GetFullPath(Path.Combine(projroot, "data", targetTest));

// Tests 
VerifierTest();


// TESTS

IGraph graph = Reader.DominatingSetReader(new AdjSetLstGraphFactory(), path);

RunTest(new GreedyDecreaseKey(), graph, "Test: Greedy baseline", new AdjSetLstGraphFactory(), false);
RunTest(new SimAnnealRandom(graph, timelimit, "20min"), graph, "Test: Simulated Annealing Random", new AdjSetLstGraphFactory(), false);
RunTest(new SimAnneal(graph, timelimit), graph, "Test: Simulated Annealing", new AdjSetLstGraphFactory(), false);
RunTest(new HybridSimAnneal(graph, timelimit, "20min"), graph, "Test: Hybrid Simulated Annealing", new AdjSetLstGraphFactory(), false);
RunTest(new SimAnneal2(graph, timelimit), graph, "Test: Simulated Annealing 2", new AdjSetLstGraphFactory(), false);
RunTest(new SimAnnealImproved(graph, timelimit, "20min"), graph, "Test: Improved Simulated Annealing", new AdjSetLstGraphFactory(), false);


//RunTest(new CC2FS(graph), graph, "Test: CC2FS", new AdjSetLstGraphFactory(), false);


//RunTest(new CC2FS_Claude(graph), graph, "Test: CC2FS_Claude", new AdjSetLstGraphFactory(), false);


void RunTest(ISolver solver, IGraph gr, string id, IGraphFactory fac, bool popup) {
    Console.WriteLine("---Starting test");

    IGraph clone = gr.CloneInto(fac);

    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timelimit));
    CancellationToken token = cts.Token;

    var ts = DateTime.Now;
    ISolution result = solver.Solve(clone, token);
    var dt = (DateTime.Now - ts);

    bool passed = Verifier.Verify(result, gr);
    int lazycount = 0;

    Console.WriteLine("Test \"" + id + "\" Delta time: " + dt.ToString() + "s . Resulting set size: " + result.Count() + " RESULT ACCEPTED?: " + passed);
    if (!passed) {
        // report the actual vertices in the solution as well
        var solList = new List<int>(result.GetEnumerator());
        solList.Sort();
        Console.WriteLine("Solution vertices count: " + solList.Count + ", min=" + (solList.Count > 0 ? solList[0].ToString() : "N/A") + ", max=" + (solList.Count > 0 ? solList[^1].ToString() : "N/A"));
        Console.WriteLine("First 20 solution nodes: " + string.Join(",", solList.Take(20)));

        // print which nodes remain uncovered
        var coveredSet = new HashSet<int>();
        foreach (int node in solList) {
            coveredSet.Add(node);
            foreach (int nbr in gr.GetEdges(node)) coveredSet.Add(nbr);
        }
        var missing = new List<int>();
        for (int v = 0; v < gr.getSize(); v++) {
            if (!coveredSet.Contains(v)) missing.Add(v);
        }
        Console.WriteLine("Uncovered nodes (0-based): " + string.Join(",", missing.Take(200)) + (missing.Count > 200 ? ",..." : ""));
        Console.WriteLine("Total uncovered count: " + missing.Count);
    }
    if (printResult) {
        Console.WriteLine("Result: ");
        foreach (int i in result.GetEnumerator()) {
            Console.Write((i + 1) + ", ");
        }
    }
    if (popup) {
        string ppath = Path.Combine(AppContext.BaseDirectory, "quickstart.png");

        Process.Start(new ProcessStartInfo {
            FileName = ppath,
            UseShellExecute = true
        });
    }

    if (toFile) {
        Console.WriteLine("Writing to file...");
        string outputDir = Path.Combine(projroot, "SolvedOutput");
        string filePath = Path.Combine(outputDir, "Sol_" + id + ".txt");
        File.Create(filePath).Close();
        using (StreamWriter writer = new StreamWriter(filePath)) {
            writer.WriteLine("Test: " + id);
            writer.WriteLine(lazycount);
            foreach (int i in result.GetEnumerator()) {
                writer.WriteLine((i + 1) + ", ");
            }
        }
        Console.WriteLine("Result written to: " + filePath);
    } else {
        Console.WriteLine("toFile is set to false, skipping file writing.");
    }
}





void VerifierTest() {
    IGraph gr = new AdjSetLstGraph(3);
    gr.AddEdge(0, 1);
    gr.AddEdge(1, 2);
    ISolution sol = new BitArraySolution(3);
    sol.AddVertex(0);

    if (Verifier.Verify(sol, gr) == true) throw new Exception("Verifier test 1 failed: expected false, got true");
    sol.AddVertex(1);
    if (Verifier.Verify(sol, gr) == false) throw new Exception("Verifier test 2 failed: expected true, got false");
    Console.WriteLine("Verifier tests passed");
}