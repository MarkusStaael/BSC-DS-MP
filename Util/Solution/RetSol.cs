using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Util.Solution; 
public class RetSol : ISolution {

    public BitArray Solution;
    public int count;
    public RetSol(int size) {
        Solution = new BitArray(size);
    }
    public void AddVertex(int v) {
    }

    public int Count() {
        return count;
    }

    public IEnumerable<int> GetEnumerator() {
        for (int i = 0; i < Solution.Length; i++) {
            if (Solution.Get(i)) yield return i;
        }
    }
}
