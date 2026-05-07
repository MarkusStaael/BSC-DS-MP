using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Util.Solution;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;


namespace BSC_DS_MP.DataStructures.Graph;

public class AdjLstWithSolGraphFactory : IGraphFactory {
    public IGraph Create(int size) {
        return new AdjLstWithSolGraph(size);
    }
}

public class AdjLstWithSolGraph : IGraph {

    int Size;

    // GRAPH
    public List<int>[] Edges;

    // SOLUTION
    public bool[] VerticesInS;
    public int SolutionCount;

    // Coverage tracking
    public HashSet<int> UncoveredVertices; // Maintained as a set for O(1) add/remove, and to avoid duplicates
    public int[] CoveredCount { get; }
    public int TotalDominatedVertices;
    
    public AdjLstWithSolGraph(int size) {
        this.Size = size;
        SolutionCount = 0;
        UncoveredVertices = new();

        Edges = new List<int>[size];
        CoveredCount = new int[size];
        VerticesInS = new bool[size];

        for (int i = 0; i < size; i++) {
            Edges[i] = new();
            CoveredCount[i] = 0;
        }
            

        TotalDominatedVertices = 0;
    }

    // SOLUTION

    private bool IsInS(int v) {
        return VerticesInS[v] == true;
    }
    private void AddToS(int v) {
        VerticesInS[v] = true;
    }
    private void RemoveFromS(int v) {
        VerticesInS[v] = false;
    }

    public int GetSolutionCount() {
        return SolutionCount;
    }
    public bool IsSolutionValid() {
        return TotalDominatedVertices == getSize();
    }
    public bool SolutionContains(int v) {
        return IsInS(v);
    }
    public int GetCoveredSum() {
        return TotalDominatedVertices;
    }
    public int Covered(int v) {
        return CoveredCount[v];
    }
    public bool IsCovered(int v) {
        return CoveredCount[v] > 0;
    }
    public RetSol GetAsRetSol() {
        var ret = new RetSol(getSize());
        ret.Solution = new BitArray(VerticesInS);
        ret.count = SolutionCount;
        return ret;
    }
    public void AddVertexToSol(int v) {
        if (IsInS(v)) throw new Exception("DOUBLE ADD: vertex " + v + " already in solution, coveredCount=" + CoveredCount[v]);
        SolutionCount++;
        AddToS(v);
        CoveredCount[v] += 1;
        if (CoveredCount[v] == 1) {
            TotalDominatedVertices += 1;
            UncoveredVertices.Remove(v);
        }

        foreach (int neighbor in GetEdges(v)) {
            CoveredCount[neighbor] += 1;
            if (CoveredCount[neighbor] == 1) {
                TotalDominatedVertices += 1;
                UncoveredVertices.Remove(neighbor);
            }
        }
    }
    public void RemoveVertexFromSol(int v) {
        if (!IsInS(v)) throw new Exception("Vertex not in solution");
        SolutionCount--;
        RemoveFromS(v);
        CoveredCount[v] -= 1;
        if (CoveredCount[v] < 0) throw new Exception("NEGATIVE COVER: vertex " + v + " coveredCount=" + CoveredCount[v]);
        if (CoveredCount[v] == 0) {
            TotalDominatedVertices -= 1;
            UncoveredVertices.Add(v);
        }

        foreach (int neighbor in GetEdges(v)) {
            CoveredCount[neighbor] -= 1;
            if (CoveredCount[neighbor] < 0) throw new Exception("NEGATIVE COVER: neighbor " + neighbor + " of " + v + " coveredCount=" + CoveredCount[neighbor]);
            if (CoveredCount[neighbor] == 0) {
                TotalDominatedVertices -= 1;
                UncoveredVertices.Add(neighbor);
            }
        }
    }

    // GRAPH

    public int getSize() {
        return Size; 
    }

    public void AddEdge(int from, int to) {
        Edges[from].Add(to);
        Edges[to].Add(from);
    }

    public void AddNode(int id) {
    }

    public void RemoveNode(int id) {
        return;
    }

    public IEnumerable<int> GetEdges(int node) {
        return Edges[node];
    }

    public IEnumerable<int> GetNodes() {
        return Enumerable.Range(0, Size);
    }

    public IGraph CloneInto(IGraphFactory fac) {
        IGraph ret = fac.Create(Size);
        foreach (int v in GetNodes())
            foreach (int u in GetEdges(v))
                if (u > v) ret.AddEdge(v, u);
        return ret;
    }
}
