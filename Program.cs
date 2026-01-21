using BSC_DS_MP.DataModel;
using BSC_DS_MP.Reading;
using BSC_DS_MP.Solutions;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

IGraph graph = new Graph();

string path = "C:\\Users\\marku\\Documents\\BSC-DS-MP\\test.gr";
Reader.DominatingSetReader(path, graph);

ISolution solution = new Greedy();

Console.WriteLine("Solution for: "+graph);
Console.WriteLine(DateTime.Now.ToString("HH:mm:ss"));

var uut = solution.Solve(graph);

Console.WriteLine(DateTime.Now);

Console.WriteLine("Result: ");
foreach(var node in uut) {
    Console.Write(node + ", ");
}