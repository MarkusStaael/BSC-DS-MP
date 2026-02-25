using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Util;

public class HashSetSolution : ISolution {

    HashSet<int> vertices;
    public HashSetSolution(int size) {
        vertices = new HashSet<int>(size/3); // TODO: OPTIMAL SET SIZE ????
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
