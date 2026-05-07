using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using BSC_DS_MP.Util;
using System.Collections;
using System.Runtime;

namespace BSC_DS_MP.Solvers;
//
//public class CC2FSSA : CC2FSOpt {
//    public CC2FSSA(IGraph graph, string name) : base(graph, name) { }
//
//    private static readonly Random rng = new Random(0);
//    private const int k = 5;
//    // Pre-allocated buffers — reused every call to avoid GC pressure.
//    private int[] _peekBuf = new int[k * 4];
//    private int[] _candidateBuf = new int[k];
//
//    protected override int GetBestRemove(bool forbidList) {
//        // Peek enough entries to have k valid (non-forbidden) candidates.
//        int peekCount = forbidList
//            ? Math.Min(k + forbidlist.Count + 1, RemoveHeap.Size())
//            : Math.Min(k, RemoveHeap.Size());
//
//        if (peekCount == 0) return base.GetBestRemove(forbidList);
//
//        if (_peekBuf.Length < peekCount)
//            _peekBuf = new int[peekCount * 2];
//
//        int found = RemoveHeap.PeekTopK(peekCount, _peekBuf);
//
//        int candidateCount = 0;
//        for (int i = 0; i < found && candidateCount < k; i++) {
//            int node = _peekBuf[i];
//            if (!forbidList || !forbidlist.Contains(node))
//                _candidateBuf[candidateCount++] = node;
//        }
//
//        if (candidateCount == 0) return base.GetBestRemove(forbidList);
//
//        int chosen = _candidateBuf[rng.Next(candidateCount)];
//        RemoveHeap.Remove(chosen);
//        InRemoveHeap[chosen] = false;
//        return chosen;
//    }
//
//    protected override int GetBestAdd() {
//        int peekCount = Math.Min(k, AddHeap.Size());
//        if (peekCount == 0) return base.GetBestAdd();
//
//        int found = AddHeap.PeekTopK(peekCount, _peekBuf);
//
//        int candidateCount = 0;
//        for (int i = 0; i < found && candidateCount < k; i++) {
//            int node = _peekBuf[i];
//            if (!CandidateSol.SolutionContains(node) && ConfChange[node])
//                _candidateBuf[candidateCount++] = node;
//        }
//
//        if (candidateCount == 0) return base.GetBestAdd();
//
//        int chosen = _candidateBuf[rng.Next(candidateCount)];
//        AddHeap.Remove(chosen);
//        InHeap[chosen] = false;
//        return chosen;
//    }
//}