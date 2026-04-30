using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Util; 
public class BitArraySolution : ISolution {

    BitArray arr;
    int count;
    public BitArraySolution(int size) {
        arr = new BitArray(size, false);
        count = 0;
    }

    public void AddVertex(int v) {
        arr[v] = true;
        count++;
    }

    public int Count() {
        return count;
    }

    public IEnumerable<int> GetEnumerator() {
        for (int i = 0; i < arr.Length; i++) {
            if (arr[i])
                yield return i;
        }
    }
}
