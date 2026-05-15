using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Reading;
using BSC_DS_MP.Solvers;
using BSC_DS_MP.Util;
using BSC_DS_MP.Util.Reduction;
using BSC_DS_MP.Verifier;
using System.Diagnostics;

// EDIT HERE FOR SIMPLE TESTING
bool printResult = false;
bool toFile = false;
bool useReduction = true;
string[] files = { "heuristic_001.gr", "heuristic_003.gr", "heuristic_004.gr", "heuristic_011.gr", "heuristic_013.gr", 
                    "heuristic_021.gr", "heuristic_030.gr", "heuristic_033.gr", "heuristic_057.gr", "heuristic_058.gr",
                    "heuristic_063.gr", "heuristic_071.gr", "heuristic_072.gr", "heuristic_074.gr", "heuristic_077.gr",
                    "heuristic_083.gr", "heuristic_088.gr", "heuristic_093.gr", "heuristic_094.gr", "heuristic_098.gr" };
int target = 0;
int timelimit = 900; // seconds
bool testSuite = true; 

string projroot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..");

if(testSuite) {
    foreach (string f in files) {
    string path = Path.Combine(projroot, "data", f);
    if (!File.Exists(path)) {
        Console.WriteLine("File not found: " + path);
        continue;
    }
    try {
        for (int i = 1; i < 4; i++) {
        RunTest(new CC2FSFactory(), "CC2FS " + f + " " + i, path);
        }
        Console.WriteLine("\n");

        for (int i = 1; i < 4; i++)
        {
            RunTest(new PCC2FSFactory(
            removePermCount: g => 10,//(int)(0.0001 * g.getSize()),
            perturbProbability: () => 0.999
        ), "P-CC2FS 10-999 " + f + " " + i.ToString(), 
        path);
        }
        Console.WriteLine("\n");
    } catch (Exception ex) {
        Console.WriteLine("Error during test \"" + f + "\": " + ex.Message);
        }
    }
}else {
    string targetTest = files[target];
    string path = Path.GetFullPath(Path.Combine(projroot, "data", targetTest));

    // TESTS
    RunTest(new CC2FSFactory(),"CC2FS", path);
    RunTest(new PCC2FSFactory(
        removePermCount: g => 10,//(int)(0.0001 * g.getSize()),
        perturbProbability: () => 0.999
    ), "P-CC2FS 10-999", 
    path);
}



//RunTest(new PCC2FSFactory(
//    removePermCount: g => 10,
//    perturbProbability: () => 0.95
//), "P-CC2FS 10-95");
//RunTest(new PCC2FSFactory(
//    removePermCount: g => 10,
//    perturbProbability: () => 0.99
//), "P-CC2FS 10-99");




void RunTest(ISolverFactory solverFactory, string id, string path) {
    Console.WriteLine("---Preparing test \"" + id + "\" ---");

    MDSReduction reduction = new MDSReduction();
    
    AdjLstWithSolGraph graph;
    if (useReduction) {
        graph = reduction.Reduce(Reader.DominatingSetReader(path));
        //Console.WriteLine("Reduced graph: " + reduction.OriginalSize + " -> " + reduction.ReducedSize + " vertices, " + reduction.ForcedVertices.Count + " forced into DS during reduction");
    } else {
        graph = Reader.DominatingSetReader(path);
        //Console.WriteLine("Running without reduction, graph size: " + graph.getSize());
    }
    //Console.WriteLine("New covered vertices: " + graph.GetCoveredSum());
    Console.WriteLine("Starting now "+DateTime.Now);

    CancellationToken token = new CancellationTokenSource(TimeSpan.FromSeconds(timelimit)).Token;
    var ts = DateTime.Now;
    ISolution reducedResult = solverFactory.Create(graph, token, id).GetSolution();
    ISolution result;
    if (useReduction) {
        //Console.WriteLine("CC2FS found " + reducedResult.Count() + " vertices on reduced graph, " + reduction.ForcedVertices.Count + " forced, reconstructing...");
        result = reduction.Reconstruct(reducedResult);
    } else {
        result = reducedResult;
    }
    var dt = (DateTime.Now - ts);
    //Console.WriteLine("Finished, now verifying");
    bool passed = Verifier.Verify(result, Reader.DominatingSetReader(path));
    int lazycount = 0;

    Console.WriteLine("Test \"" + id + "\" Delta time: " + dt.ToString() + "s . Resulting set size: " + result.Count() + " RESULT ACCEPTED?: " + passed);
    //if (!passed) {
    //    // report the actual vertices in the solution as well
    //    var solList = new List<int>(result.GetEnumerator());
    //    solList.Sort();
    //    Console.WriteLine("Solution vertices count: " + solList.Count + ", min=" + (solList.Count > 0 ? solList[0].ToString() : "N/A") + ", max=" + (solList.Count > 0 ? solList[^1].ToString() : "N/A"));
    //    Console.WriteLine("First 20 solution nodes: " + string.Join(",", solList.Take(20)));
    //
    //    // print which nodes remain uncovered
    //    var coveredSet = new HashSet<int>();
    //    foreach (int node in solList) {
    //        coveredSet.Add(node);
    //        foreach (int nbr in clone.GetEdges(node)) coveredSet.Add(nbr);
    //    }
    //    var missing = new List<int>();
    //    for (int v = 0; v < clone.getSize(); v++) {
    //        if (!coveredSet.Contains(v)) missing.Add(v);
    //    }
    //    Console.WriteLine("Uncovered nodes (0-based): " + string.Join(",", missing.Take(200)) + (missing.Count > 200 ? ",..." : ""));
    //    Console.WriteLine("Total uncovered count: " + missing.Count);
    //}
    //if (printResult) {
    //    Console.WriteLine("Result: ");
    //    foreach (int i in result.GetEnumerator()) {
    //        Console.Write((i + 1) + ", ");
    //    }
    //}
    //if (popup) {
    //    string ppath = Path.Combine(AppContext.BaseDirectory, "quickstart.png");
    //
    //    Process.Start(new ProcessStartInfo {
    //        FileName = ppath,
    //        UseShellExecute = true
    //    });
    //}
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
        //Console.WriteLine("toFile is set to false, skipping file writing.");
    }
}





void VerifierTest() {
    IGraph gr = new AdjLstGraph(3);
    gr.AddEdge(0, 1);
    gr.AddEdge(1, 2);
    ISolution sol = new BitArraySolution(3);
    sol.AddVertex(0);

    if (Verifier.Verify(sol, gr) == true) throw new Exception("Verifier test 1 failed: expected false, got true");
    sol.AddVertex(1);
    if (Verifier.Verify(sol, gr) == false) throw new Exception("Verifier test 2 failed: expected true, got false");
    Console.WriteLine("Verifier tests passed");
}