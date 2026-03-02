using BSC_DS_MP.DataStructures.Graph;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace BSC_DS_MP.DataStructures.Graph;

public class NewGraphFactory : IGraphFactory {
    public IGraph Create(int size) {
        return new NewGraph(size);
    }
}

public class NewGraph : IGraph {
    private HashSet<int>[] edges;
    private int size;
    private HashSet<int> Nodes;

    public NewGraph(int size) {
        this.size = size;
        edges = new HashSet<int>[size];
        Nodes = new HashSet<int>();

        for (int i = 0; i < size; i++) {
            edges[i] = new HashSet<int>();
            Nodes.Add(i);
        }
    }

    public int getSize() => size;

    public void AddNode(int id) {
        if (id >= 0 && id < size) {
            Nodes.Add(id);
        }
    }

    public void AddEdge(int from, int to) {
        if (!Nodes.Contains(from) || !Nodes.Contains(to)) return;
        edges[from].Add(to);
        edges[to].Add(from);
    }

    public IEnumerable<int> GetEdges(int node) {
        if (!Nodes.Contains(node)) return Enumerable.Empty<int>();
        return edges[node];
    }

    public void RemoveNode(int id) {
        if (!Nodes.Contains(id)) return;

        foreach (var neighbor in edges[id]) {
            edges[neighbor].Remove(id);
        }
        edges[id].Clear();
        Nodes.Remove(id);
        size--;
    }

    public IEnumerable<int> GetNodes() {
        return Nodes;
    }

    public IGraph CloneInto(IGraphFactory fac) {
        IGraph ret = fac.Create(size);
        foreach (int node in GetNodes()) {
            ret.AddNode(node);
            foreach (int neighbor in GetEdges(node)) {
                if (node < neighbor) {
                    ret.AddEdge(node, neighbor);
                }
            }
        }
        return ret;
    }
}
