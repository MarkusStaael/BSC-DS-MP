using BSC_DS_MP.DataStructures.Primitives;
using System;
using System.Collections.Generic;
using System.Text;
using BSC_DS_MP.Util;

namespace BSC_DS_MP.DataStructures.Graph;

public interface IGraph {

    //public ISet<int> Nodes { get; }
    //public IDictionary<int, ISet<int>> Edges { get; }
    public IGraph CloneInto(IGraphFactory fac);
    public int getSize();
    public void AddNode(int id);
    public void AddEdge(int from, int to);
    public IEnumerable<int> GetEdges(int node);
    //public IEnumerable<UInt24> GetEdges(int node);
    public void RemoveNode(int id);
    public IEnumerable<int> GetNodes(); 

}

public interface IGraphFactory {
    public IGraph Create(int size);
}