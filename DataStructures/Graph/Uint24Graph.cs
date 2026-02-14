using BSC_DS_MP.DataStructures.Primitives;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;


namespace BSC_DS_MP.DataStructures.Graph;

public class Uint24GraphFactory : IGraphFactory {
    public IGraph Create(int size) {
        return new Uint24Graph(size);
    }
}

public class Uint24Graph : IGraph {

    public Uint24Graph(int size) {
        Nodes = new();
        Edges = new List<UInt24>[size + 1];
        for (int i = 0; i < size; i++)
            Edges[i] = new List<UInt24>();
    }

    HashSet<int> Nodes = new();
    public List<UInt24>[] Edges; // Lowkey just merge

    public void AddEdge(int from, int to) {
        Edges[from].Add(new UInt24(((uint)to)));
        Edges[to].Add(new UInt24(((uint)from)));
    }

    public void AddNode(int id) {
        Nodes.Add(id);
    }

    public IGraph Clone() {
        throw new NotImplementedException();
        //IGraph graph = new ArrayGraph(Edges.Length);
        //foreach (int node in Nodes) {
        //    graph.AddNode(node);
        //
        //    foreach (UInt24 to in Edges[node]) {
        //        graph.AddEdge(node, to);
        //    }
        //}
        //
        //return graph;
    }

    public void RemoveNode(int id) {
        Nodes.Remove(id);
        foreach (UInt24 to in Edges[id]) {
            Edges[to.Value].Remove(new UInt24(((uint)id)));
        }
        Edges[id].Clear();
    }

    public IEnumerable<int> GetEdges(int node) {
        return Edges[node].Select(x => (int)x.Value);
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
