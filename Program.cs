using BSC_DS_MP.DataModel;
using BSC_DS_MP.Reading;
using BSC_DS_MP.Solutions;
using System.Globalization;

IGraph graph = new Graph();

string path = "C:\\Users\\marku\\OneDrive\\Dokumenter\\BSC-DS-MP\\test.gr";
Reader.DominatingSetReader(path, graph);

ISolution solution = new Greedy();

Console.WriteLine("Solution for: "+graph);
var ts = DateTime.Now;
//Console.WriteLine(DateTime.Now.CompareTo);

var uut = solution.Solve(graph);

var dt = (DateTime.Now - ts);

Console.WriteLine("Delta time: "+dt.ToString());

Console.WriteLine("Result: ");
foreach(var node in uut) {
    Console.Write(node + ", ");
}