using BSC_DS_MP.DataStructures.Graph;
using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Reading; 
public static class Reader {

    public static IGraph DominatingSetReader(IGraphFactory factory, string path) {

        Console.WriteLine("Reading Dominating Set from file: " + path);
        IGraph graph;

        using (StreamReader reader = new StreamReader(path)) {
            string line;
            while(true) {
                line = reader.ReadLine();
                string[] strings = line.Split(' ');
                if (strings[0].Equals("c")) {
                    Console.WriteLine("Comment: " + line);
                    continue;
                }
                if (strings[0].Equals("p")) {
                    Console.WriteLine("Problem: " + line);
                    graph = factory.Create(int.Parse(strings[2]));
                    break;
                }
                throw new Exception("Invalid file format: missing problem line");
            }

            while ((line = reader.ReadLine()) != null) {

                string[] strings = line.Split(' ');
                if (strings[0].Equals("c")) {
                    Console.WriteLine("Comment: " + line);
                    continue;
                }
                int v = int.Parse(strings[0])-1, e = int.Parse(strings[1])-1;

                graph.AddNode(v);
                graph.AddEdge(v,e);
            }
        }

        return graph;
    }

}
