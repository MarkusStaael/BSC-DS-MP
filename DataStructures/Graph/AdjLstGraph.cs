using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;


namespace BSC_DS_MP.DataStructures.Graph;

public class AdjLstGraphFactory : IGraphFactory {
    public IGraph Create(int size) {
        return new AdjLstGraph(size);
    }
}

public class AdjLstGraph : IGraph {
    int Size;
    public AdjLstGraph(int size) {
        this.Size = size;
        Edges = new List<int>[size+1];
        for (int i = 0; i < size; i++)
            Edges[i] = new();
    }

    public int getSize() {
        return Size; 
    }

    public List<int>[] Edges; // Lowkey just merge

    public void AddEdge(int from, int to) {
        Edges[from].Add(to);
        Edges[to].Add(from);
    }

    public void AddNode(int id) {
    }

    public IGraph CloneInto(IGraphFactory fac) {
        IGraph ret = fac.Create(Size);
        foreach(int key in GetNodes()) {
            foreach(int to in GetEdges(key)) {
                ret.AddEdge(key, to);
            }
        }

        return ret;
    }

    public void RemoveNode(int id) {
        // TODO: IMPLEMENT
        return;
    }

    public IEnumerable<int> GetEdges(int node) {
        return Edges[node];
    }

    public IEnumerable<int> GetNodes() {
        return Enumerable.Range(0, Size); 
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
