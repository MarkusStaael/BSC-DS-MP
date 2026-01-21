using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.DataModel; 
public interface IGraph {

    //public ISet<int> Nodes { get; }
    //public IDictionary<int, ISet<int>> Edges { get; }

    public IGraph Clone();

    public void AddNode(int id);
    public void AddEdge(int from, int to);
    public IEnumerable<int> GetEdges(int node);
    public void RemoveNode(int id);
    public IEnumerable<int> GetNodes(); 

}
