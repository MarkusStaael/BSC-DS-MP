using BSC_DS_MP.DataStructures.Graph;
using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.Util; 
internal class SimpleSol {
    public HashSet<int> vertices; // 1m ->
    public ushort[] coveredCount; // 1m -> 1.91 MB
    public bool solutionIsValid;
    public int coveredSum;
    IGraph graph;
    /**
     * TREE TO QUICKCHECK? 
     * 
     *      F
     *    /   \
     *   F     T
     *  / \   / \
     * T   F T   T
     * 
     */

    public SimpleSol(IGraph graph) {
        vertices = new HashSet<int>();
        coveredCount = new ushort[graph.getSize()];
        foreach(int node in graph.GetNodes()) {
            coveredCount[node] = 0;
        }
        solutionIsValid = false;
        this.graph = graph;
        coveredSum = 0;
    }
    public void AddVertex(int v) {
        vertices.Add(v);
        foreach (int neighbor in graph.GetEdges(v)) {
            coveredCount[neighbor] += 1;
            if (coveredCount[neighbor] == 1) {
                coveredSum += 1;
            }
        }
    }
    public void RemoveVertex(int v) {
        vertices.Remove(v);
        foreach (int neighbor in graph.GetEdges(v)) {
            coveredCount[neighbor] -= 1;
            if (coveredCount[neighbor] == 0) {
                solutionIsValid = false;
                coveredSum -= 1;
            }
        }
    }
    public bool IsSolutionValid() {
        return coveredSum==(graph.getSize());
    }
    public IEnumerable<int> GetSolution() {
        return vertices;
    }
    public bool SolutionContains(int v) {
        return vertices.Contains(v);
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
}