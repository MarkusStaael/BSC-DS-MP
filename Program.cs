using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Primitives;
using BSC_DS_MP.Reading;
using BSC_DS_MP.Solvers;
using BSC_DS_MP.Verifier;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

// EDIT HERE FOR SIMPLE TESTING
bool printResult = false;
bool toFile = false;
string[] files = { "test.gr", "30z50.gr", "heuristic_001.gr" };
int target = 2;

string targetTest = files[target];
string projroot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..");
string path = Path.GetFullPath(Path.Combine(projroot, "data",targetTest));

// "UNIT" tests 
VerifierTest();


// TESTS

RunTest(new GreedyHeap2(),  Reader.DominatingSetReader(new AdjSetLstGraphFactory(), path), "Test 1: Greedy", new AdjSetLstGraphFactory());
//RunTest(new CC2FS(),        Reader.DominatingSetReader(new AdjLstGraphFactory(), path), "Test 2: CC2FS");

// REMEMBER TO +1 WHEN PRINTING RESULTS

void RunTest(ISolver solver,IGraph gr,string id,IGraphFactory fac) {
    Console.WriteLine("---Starting test");

    IGraph clone = gr.CloneInto(fac);

    var ts = DateTime.Now;
    var result = solver.Solve(clone);
    var dt = (DateTime.Now - ts);

    bool passed = Verifier.Verify(result, gr);
    int lazycount = 0;
    for (int i = 0; i < result.Length; i++) {
        if (result[i]) lazycount++;
    }

    Console.WriteLine("Test \""+id+"\" Delta time: " + dt.ToString() + "s . Resulting set size: " + lazycount + " RESULT ACCEPTED?: "+passed);
    if (printResult) {
        Console.WriteLine("Result: ");
        for (int i = 0; i < result.Length; i++) {
            if (result[i]) Console.Write((i + 1) + ", ");
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
            for (int i = 0; i < result.Length; i++) {
                if (result[i]) writer.WriteLine((i + 1) + ", ");
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
    BitArray arr = new BitArray(3, false);
    arr[0] = true;

    if (Verifier.Verify(arr, gr) == true) throw new Exception("Verifier test 1 failed: expected false, got true");
    arr[1] = true;
    if (Verifier.Verify(arr, gr) == false) throw new Exception("Verifier test 2 failed: expected true, got false");
    Console.WriteLine("Verifier tests passed");
}

