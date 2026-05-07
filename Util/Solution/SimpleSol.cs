using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.Util.Solution;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Util; 
public class SimpleSol {

    public bool[] VerticesInS;
    public HashSet<int> uncoveredVertices;
    public int[] coveredCount;
    public int coveredSum;
    private int SolutionCount;
    IGraph graph;

    public SimpleSol(IGraph graph) {
        VerticesInS = new bool[graph.getSize()]; // Default of value is false
        coveredCount = graph.CoveredCount;
        this.graph = graph;
        // Initialize coveredSum and uncoveredVertices from any pre-existing coverage
        // (e.g. set by the graph reducer before handing the graph to a solver).
        uncoveredVertices = new();
        coveredSum = 0;
        foreach (int v in graph.GetNodes()) {
            if (coveredCount[v] > 0) coveredSum++;
            else uncoveredVertices.Add(v);
        }
    }

    public void InitFromSol(ISolution sol) {
        foreach (int i in sol.GetEnumerator()) {
            AddVertex(i);
        }
        foreach (int i in graph.GetNodes()) {
            if (!IsCovered(i)) {
                uncoveredVertices.Add(i);
            }
        }
    }

    private bool IsInS(int v) {
        return VerticesInS[v] == true;
    }
    private void AddToS(int v) {
        VerticesInS[v] = true;
    }
    private void RemoveFromS(int v) {
        VerticesInS[v] = false;
    }

    public void AddVertex(int v) {
        if (IsInS(v)) throw new Exception("DOUBLE ADD: vertex " + v + " already in solution, coveredCount=" + coveredCount[v]);
        SolutionCount++;
        AddToS(v);
        coveredCount[v] += 1;
        if (coveredCount[v] == 1) {
            coveredSum += 1;
            uncoveredVertices.Remove(v);
        }

        foreach (int neighbor in graph.GetEdges(v)) {
            coveredCount[neighbor] += 1;
            if (coveredCount[neighbor] == 1) {
                coveredSum += 1;
                uncoveredVertices.Remove(neighbor);
            }
        }
    }
    public void RemoveVertex(int v) {
        if (!IsInS(v)) throw new Exception("Vertex not in solution");
        SolutionCount--;
        RemoveFromS(v);
        coveredCount[v] -= 1;
        if (coveredCount[v] < 0) throw new Exception("NEGATIVE COVER: vertex " + v + " coveredCount=" + coveredCount[v]);
        if (coveredCount[v] == 0) {
            coveredSum -= 1;
            uncoveredVertices.Add(v);
        }

        foreach (int neighbor in graph.GetEdges(v)) {
            coveredCount[neighbor] -= 1;
            if (coveredCount[neighbor] < 0) throw new Exception("NEGATIVE COVER: neighbor " + neighbor + " of " + v + " coveredCount=" + coveredCount[neighbor]);
            if (coveredCount[neighbor] == 0) {
                coveredSum -= 1;
                uncoveredVertices.Add(neighbor);
            }
        }
    }
    public int GetSolutionCount() {
        return SolutionCount;
    }
    public bool IsSolutionValid() {
        return coveredSum == graph.getSize();
    }
    public bool SolutionContains(int v) {
        return IsInS(v);
    }
    public int GetCoveredSum() {
        return coveredSum;
    }
    public int Covered(int v) {
        return coveredCount[v];
    }
    public bool IsCovered(int v) {
        return coveredCount[v] > 0;
    }
    public RetSol GetAsRetSol() {
        var ret = new RetSol(graph.getSize());
        ret.Solution = new BitArray(VerticesInS);
        ret.count = SolutionCount;
        return ret;
    }
}