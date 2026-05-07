using BSC_DS_MP.DataStructures.Graph;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;


namespace BSC_DS_MP.DataStructures.Graph;

public class AdjSetLstGraphFactory : IGraphFactory {
    public IGraph Create(int size) {
        return new AdjSetLstGraph(size);
    }
}

public class AdjSetLstGraph : IGraph {
    public HashSet<int>[] Edges; // Lowkey just merge
    public int[] CoveredCount { get; }
    int Size;
    public AdjSetLstGraph(int size) {
        this.Size = size;
        Edges = new HashSet<int>[size + 1];
        for (int i = 0; i < size; i++)
            Edges[i] = new();
        CoveredCount = new int[size];
    }

    public int getSize() {
        return Size;
    }

    public void AddEdge(int from, int to) {
        Edges[from].Add(to);
        Edges[to].Add(from);
    }

    public void RemoveNode(int id) {
        foreach(var neighbor in Edges[id]) {
            Edges[neighbor].Remove(id);
        }
        //Edges[id].Clear();
        Edges[id] = null; 
        return;
    }

    public IEnumerable<int> GetEdges(int node) {
        return Edges[node];
    }

    public IEnumerable<int> GetNodes() {
        // the previous implementation excluded the last index; include full range
        return Enumerable.Range(0, Size);
    }
    public IGraph CloneInto(IGraphFactory fac) {
        IGraph ret = fac.Create(Size);
        foreach (int key in GetNodes()) {
            foreach (int to in GetEdges(key)) {
                ret.AddEdge(key, to);
            }
        }

        return ret;
    }

    public void AddNode(int id) {
    }
}
