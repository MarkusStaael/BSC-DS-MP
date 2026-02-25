using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Primitives;
using BSC_DS_MP.Reading;
using BSC_DS_MP.Solvers;
using System.Globalization;
using System.IO;

// EDIT HERE FOR SIMPLE TESTING
bool printResult = true;
bool toFile = false;
string[] files = { "test.gr", "30z50.gr", "heuristic_001.gr" };
int target = 1;

string targetTest = files[target];
string projroot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..");
string path = Path.GetFullPath(Path.Combine(projroot, "data",targetTest));

//IGraph graph = Reader.DominatingSetReader(new ArrayGraphFactory(),path);

//ISolver solver = new GreedyHeap();

RunTest(new GreedyHeap2(),  Reader.DominatingSetReader(new AdjLstGraphFactory(), path), "Test 1: Greedy");
//RunTest(new CC2FS(),        Reader.DominatingSetReader(new AdjLstGraphFactory(), path), "Test 2: CC2FS");

// REMEMBER TO +1 WHEN PRINTING RESULTS

void RunTest(ISolver solver,IGraph gr,string id) {
    var ts = DateTime.Now;
    var result = solver.Solve(gr);
    var dt = (DateTime.Now - ts);
    Console.WriteLine("Test \""+id+"\" Delta time: " + dt.ToString() + ". Resulting set size: " + result.Count);
    if (printResult) {
        Console.WriteLine("Result: ");
        foreach (var node in result) {
            Console.Write((node+1) + ", ");
        }
    }

    
    if (toFile) {
        Console.WriteLine("Writing to file...");
        string outputDir = Path.Combine(projroot, "SolvedOutput");
        string filePath = Path.Combine(outputDir, "Sol_" + id + ".txt");
        File.Create(filePath).Close();
        using (StreamWriter writer = new StreamWriter(filePath)) {
            writer.WriteLine("Test: " + id);
            writer.WriteLine(result.Count);
            foreach (var node in result) {
                writer.WriteLine(node);
            }
        }
        Console.WriteLine("Result written to: " + filePath);
    }else {
        Console.WriteLine("toFile is set to false, skipping file writing.");
    }
}

