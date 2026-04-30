using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Util; 
public class ListSolution : ISolution {
    List<int> vertices;
    public ListSolution(int size) {
        vertices = new List<int>();
    }
    public void AddVertex(int v) {
        vertices.Add(v);
    }
    public int Count() {
        return vertices.Count;
    }
    public IEnumerable<int> GetEnumerator() {
        return vertices;
    }
}
