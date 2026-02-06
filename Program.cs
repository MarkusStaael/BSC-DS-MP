using BSC_DS_MP.DataModel;
using BSC_DS_MP.DataModel.Graph;
using BSC_DS_MP.Reading;
using BSC_DS_MP.Solutions;
using System.Globalization;


//string path = "C:\\Users\\marku\\OneDrive\\Dokumenter\\BSC-DS-MP\\test.gr";
string path2 = "C:\\Users\\marku\\Documents\\BSC-DS-MP\\30z50.gr"; //"C:\\Users\\marku\\Documents\\BSC-DS-MP\\heuristic_001.gr\\heuristic_001.gr";


//IGraph arrgraph = new ArrayGraph(30);
//IGraph graph2 = new Graph();


IGraph arrgraph = Reader.DominatingSetReader(path2);
//Reader.DominatingSetReader(path2, graph2);

ISolution solution = new GreedyHeap();

RunTest(solution, arrgraph, "Array Graph");
//RunTest(solution, graph2, "Dictionary");


void RunTest(ISolution sol,IGraph gr,String id) {
    var ts = DateTime.Now;
    var result = sol.Solve(gr);
    var dt = (DateTime.Now - ts);
    Console.WriteLine("Test \""+id+"\" Delta time: " + dt.ToString());
    Console.WriteLine("Result: ");
    foreach (var node in result) {
        Console.Write(node + ", ");
    }
}

