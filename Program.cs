using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Reading;
using BSC_DS_MP.Solutions;
using System.Globalization;

// EDIT HERE FOR SIMPLE TESTING
bool printResult = false;
string[] files = { "test.gr", "30z50.gr", "heuristic_001.gr" };
int target = 2;
// test - P 
Console.WriteLine("Hello world!");

string targetTest = files[target];
string projroot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..");
string path = Path.GetFullPath(Path.Combine(projroot, "data",targetTest));

IGraph graph = Reader.DominatingSetReader(new ArrayGraphFactory(),path);
ISolver solver = new GreedyHeap();

RunTest(solver, graph, "Test 1");



void RunTest(ISolver solver,IGraph gr,string id) {
    var ts = DateTime.Now;
    var result = solver.Solve(gr);
    var dt = (DateTime.Now - ts);
    Console.WriteLine("Test \""+id+"\" Delta time: " + dt.ToString());
    if (printResult) {
        Console.WriteLine("Result: ");
        foreach (var node in result) {
            Console.Write(node + ", ");
        }
    }
}

