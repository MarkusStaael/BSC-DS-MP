using BSC_DS_MP.DataStructures.Graph;
using BSC_DS_MP.DataStructures.Heap;
using BSC_DS_MP.Util;
using System.Collections;
using System.Runtime;

namespace BSC_DS_MP.Solvers;

public class CC2FSSA : CC2FS {
    public CC2FSSA(IGraph graph, string name) : base(graph, name) {
    }

private static Random rng = new Random();

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