using System;
using System.Collections.Generic;
using System.Text;


namespace BSC_DS_MP.DataStructures.Graph;

public class ArrayGraphFactory : IGraphFactory {
    public IGraph Create(int size) {
        return new ArrayGraph(size);
    }
}

public class ArrayGraph : IGraph {

    public ArrayGraph(int size) {
        Nodes = new();
        Edges = new HashSet<int>[size+1];
        for (int i = 0; i <= size; i++)
            Edges[i] = new HashSet<int>();
    }

    HashSet<int> Nodes = new();
    public HashSet<int>[] Edges; // Lowkey just merge

    public void AddEdge(int from, int to) {
        Edges[from].Add(to);
        Edges[to].Add(from);
    }

    public void AddNode(int id) {
        Nodes.Add(id);
    }

    public IGraph Clone() {
        IGraph graph = new ArrayGraph(Edges.Length);
        foreach(int node in Nodes) {
            graph.AddNode(node);

            foreach (int to in Edges[node]) {
                graph.AddEdge(node, to);
            }
        }
        
        return graph;
    }

    public void RemoveNode(int id) {
        Nodes.Remove(id);
        foreach (int to in Edges[id]) {
            Edges[to].Remove(id);
        }
        Edges[id].Clear();
    }

    public IEnumerable<int> GetEdges(int node) {
        return Edges[node];
    }

    public IEnumerable<int> GetNodes() {
        return Nodes; 
    }

    //public override string ToString() {
    //    StringBuilder sb = new StringBuilder();
    //    foreach(KeyValuePair<int,ISet<int>> edge in Edges) {
    //        sb.Append(edge.Key + ": ");
    //        foreach(int to in edge.Value) {
    //            sb.Append(to + " ");
    //        }
    //        sb.Append(", ");
    //    }
    //    return sb.ToString();
    //}
}
