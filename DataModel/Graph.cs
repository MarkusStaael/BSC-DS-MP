using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.DataModel;

public class Graph : IGraph {

    public Graph() {
        Nodes = new();
        Edges = new();
    }

    public HashSet<int> Nodes;

    public Dictionary<int, ISet<int>> Edges; // Lowkey just merge

    public void AddEdge(int from, int to) {
        Edges[from].Add(to);
        if(!Nodes.Contains(to)) AddNode(to); 
        Edges[to].Add(from);
    }

    public void AddNode(int id) {
        Nodes.Add(id);
        if(!Edges.ContainsKey(id)) Edges.Add(id, new HashSet<int>());
    }

    public IGraph Clone() {
        Graph graph = new Graph();
        foreach(int node in Nodes) {
            graph.AddNode(node);
        }
        foreach(KeyValuePair<int,ISet<int>> edge in Edges) {
            foreach(int to in edge.Value) {
                graph.AddEdge(edge.Key, to);
            }
        }
        return graph;
    }

    public void RemoveNode(int id) {
        Nodes.Remove(id);
        Edges.Remove(id);
    }

    public IEnumerable<int> GetEdges(int node) {
        return Edges[node];
    }

    public IEnumerable<int> GetNodes() {
        return Nodes; 
    }

    public override string ToString() {
        StringBuilder sb = new StringBuilder();
        foreach(KeyValuePair<int,ISet<int>> edge in Edges) {
            sb.Append(edge.Key + ": ");
            foreach(int to in edge.Value) {
                sb.Append(to + " ");
            }
            sb.Append(", ");
        }
        return sb.ToString();
    }
}
