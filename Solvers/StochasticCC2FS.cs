using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using BSC_DS_MP.Util;
using System.Collections;
using System.Runtime;

namespace BSC_DS_MP.Solvers;

public class CC2FSStoch : CC2FS {
    public CC2FSStoch(IGraph graph, string name) : base(graph, name) {
    }

private static Random rng = new Random(0); // fixed seed for reproducibility

private int PickRandom(List<int> candidates) {
    return candidates[rng.Next(candidates.Count)];
}

protected override int GetBestRemove(bool forbidList) {
    int k = 5; // top-k candidates to consider

    List<int> addAgainList = new List<int>();
    List<int> topList = new List<int>(5);

    for (int i = 0; i < k && RemoveHeap.Size() > 0; i++) {
        while (true) {
            int target = RemoveHeap.RemoveMax();

            if (forbidList && forbidlist.Contains(target)) {
                addAgainList.Add(target);
            } else {
                InRemoveHeap[target] = false;
                topList.Add(target);
                break;
            }
        }
    }

    // restore skipped ones
    foreach (int u in addAgainList) {
        AddToRemoveHeap(u);
    }

    // pick random from top-k
    int luckyIndex = rng.Next(topList.Count);
    int chosen = topList[luckyIndex];

    // put back the rest
    for (int i = 0; i < topList.Count; i++) {
        if (i != luckyIndex) {
            AddToRemoveHeap(topList[i]);
        }
    }

    return chosen;
}

protected override int GetBestAdd() {
    int k = 5; // top-k

    List<int> topList = new List<int>(5);
    

    for (int i = 0; i < k && AddHeap.Size() > 0; i++) {
        while (true) {
            int target = AddHeap.RemoveMax();
            InHeap[target] = false;

            if (CandidateSol.SolutionContains(target)) continue;
            if (!ConfChange[target]) continue;

            topList.Add(target);
            break;
        }
    }

    int luckyIndex = rng.Next(topList.Count);
    int chosen = topList[luckyIndex];

    // reinsert others
    for (int i = 0; i < topList.Count; i++) {
        if (i != luckyIndex) {
            AddToAddHeap(topList[i]);
        }
    }

    return chosen;
}
}


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