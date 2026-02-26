using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Primitives;
using BSC_DS_MP.Reading;
using BSC_DS_MP.Solvers;
using BSC_DS_MP.Util;
using BSC_DS_MP.Verifier;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

// EDIT HERE FOR SIMPLE TESTING
bool printResult = false;
bool toFile = false;
string[] files = { "test.gr", "30z50.gr", "heuristic_001.gr" };
int target = 1;

string targetTest = files[target];
string projroot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..");
string path = Path.GetFullPath(Path.Combine(projroot, "data",targetTest));

// "UNIT" tests 
VerifierTest();


// TESTS

IGraph graph = Reader.DominatingSetReader(new AdjSetLstGraphFactory(), path);


RunTest(new GreedyLazyHeap(), graph, "Test 1: GreedyLazy", new AdjSetLstGraphFactory());
RunTest(new CC2FS(), graph, "CC2FS", new AdjSetLstGraphFactory());
//RunTest(new CC2FS(),        Reader.DominatingSetReader(new AdjLstGraphFactory(), path), "Test 2: CC2FS");

// REMEMBER TO +1 WHEN PRINTING RESULTS

void RunTest(ISolver solver,IGraph gr,string id,IGraphFactory fac) {
    Console.WriteLine("---Starting test");

    IGraph clone = gr.CloneInto(fac);

    var ts = DateTime.Now;
    ISolution result = solver.Solve(clone);
    var dt = (DateTime.Now - ts);

    bool passed = Verifier.Verify(result, gr);
    int lazycount = 0;

    Console.WriteLine("Test \""+id+"\" Delta time: " + dt.ToString() + "s . Resulting set size: " + result.Count() + " RESULT ACCEPTED?: "+passed);
    if (printResult) {
        Console.WriteLine("Result: ");
        foreach(int i in result.GetEnumerator()) {
            Console.Write((i + 1) + ", ");
        }
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

