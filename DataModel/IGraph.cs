using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.DataModel; 
internal interface IGraph {

    public IEnumerable<INode> Nodes { get; }
    public IEnumerable<IEdge> Edges { get; }

    public INode AddNode(string label);
    public IEdge AddEdge(INode from, INode to, string label);

}
