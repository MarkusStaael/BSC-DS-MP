using BSC_DS_MP.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Reading; 
public static class Reader {

    public static IGraph DominatingSetReader(string path, IGraph graph) {

        Console.WriteLine("Reading Dominating Set from file: " + path);

        using (StreamReader reader = new StreamReader(path)) {
            string line;
            while ((line = reader.ReadLine()) != null) {

                string[] strings = line.Split(' ');
                if (strings[0].Equals("c")) {
                    Console.WriteLine("Comment: " + line);
                    continue;
                }
                if (strings[0].Equals("p")) {
                    Console.WriteLine("Problem: " + line);
                    continue;
                }

                int v = int.Parse(strings[0]), e = int.Parse(strings[1]);

                graph.AddNode(v);
                graph.AddEdge(v,e);
            }
        }

        return graph;
    }

}
