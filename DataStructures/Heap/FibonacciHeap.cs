using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;

namespace BSC_DS_MP.DataStructures.Heap;

// CODE FROM:
// https://github.com/sqeezy/FibonacciHeap/tree/main

/// <summary>
/// Fibonacci Heap realization. Uses generic type T for data storage and TKey as a key type.
/// </summary>
/// <typeparam name="T">Type of the stored objects.</typeparam>
/// <typeparam name="TKey">Type of the object key. Should implement IComparable.</typeparam>
public class FibonacciHeap<T, TKey> where TKey : IComparable<TKey> {
    private readonly TKey _maxKeyValue;

    /// <summary>
    /// Maximum (starting) node of the heap.
    /// </summary>
    private FibonacciHeapNode<T, TKey> _maxNode;

    /// <summary>
    /// The nodes quantity.
    /// </summary>
    private int _nNodes;

    /// <summary>
    /// Initializes the new instance of the Heap.
    /// </summary>
    /// <param name="maxKeyValue">Maximum value of the key - to be used for comparing.</param>
    public FibonacciHeap(TKey maxKeyValue) {
        _maxKeyValue = maxKeyValue;
    }

    /// <summary>
    /// Identifies whatever heap is empty.
    /// </summary>
    /// <returns>true if heap is empty - contains no elements.</returns>
    public bool IsEmpty() {
        return _maxNode == null;
    }

    /// <summary>
    /// Removes all the elements from the heap.
    /// </summary>
    public void Clear() {
        _maxNode = null;
        _nNodes = 0;
    }

    public void IncreaseKey(FibonacciHeapNode<T, TKey> x, TKey k) {
        if (k.CompareTo(x.Key) < 0)
            throw new ArgumentException("new key is smaller than current key");

        // If key unchanged, nothing to do
        if (k.CompareTo(x.Key) == 0)
            return;

        x.Key = k;

        // If x has children, increasing x.Key may violate heap order
        // (parent.Key >= child.Key). Cut any child whose key is larger
        // than the new x.Key and move it to the root list.
        FibonacciHeapNode<T, TKey> child = x.Child;
        if (child != null) {
            int numChildren = x.Degree;
            FibonacciHeapNode<T, TKey> curr = child;
            for (int i = 0; i < numChildren; i++) {
                FibonacciHeapNode<T, TKey> next = curr.Right;
                if (curr.Key.CompareTo(x.Key) > 0) {
                    Cut(curr, x);
                    if (_maxNode == null || curr.Key.CompareTo(_maxNode.Key) > 0) {
                        _maxNode = curr;
                    }
                }
                curr = next;
            }
        }

        // If x was the max node, it may no longer be the maximum — recompute.
        if (_maxNode == x) {
            if (_maxNode != null) {
                var start = _maxNode;
                var curr = _maxNode.Right;
                FibonacciHeapNode<T, TKey> max = _maxNode;

                while (curr != start) {
                    if (curr.Key.CompareTo(max.Key) > 0)
                        max = curr;
                    curr = curr.Right;
                }

                _maxNode = max;
            }
        }
    }


    /// <summary>
    /// Decreses the key of a node.
    /// O(1) amortized.
    /// </summary>
    public void DecreaseKey(FibonacciHeapNode<T, TKey> x, TKey k) {
        if (k.CompareTo(x.Key) > 0) {
            throw new ArgumentException("decreaseKey() got larger key value");
        }

        x.Key = k;

        FibonacciHeapNode<T, TKey> y = x.Parent;

        if (y != null && x.Key.CompareTo(y.Key) > 0) {
            Cut(x, y);
            CascadingCut(y);
        }

        if (x.Key.CompareTo(_maxNode.Key) > 0) {
            _maxNode = x;
        }
    }

    /// <summary>
    /// Deletes a node from the heap.
    /// O(log n)
    /// </summary>
    public void Delete(FibonacciHeapNode<T, TKey> x) {
        // make newParent as large as possible
        IncreaseKey(x, _maxKeyValue);

        // remove the largest, which decreases n also
        RemoveMax();
    }

    /// <summary>
    /// Inserts a new node with its key.
    /// O(1)
    /// </summary>
    public void Insert(FibonacciHeapNode<T, TKey> node) {
        // concatenate node into max list
        if (_maxNode != null) {
            node.Left = _maxNode;
            node.Right = _maxNode.Right;
            _maxNode.Right = node;
            node.Right.Left = node;

            if (node.Key.CompareTo(_maxNode.Key) > 0) {
                _maxNode = node;
            }
        } else {
            _maxNode = node;
        }

        _nNodes++;
    }

    /// <summary>
    /// Returns the largest node of the heap.
    /// O(1)
    /// </summary>
    /// <returns></returns>
    public FibonacciHeapNode<T, TKey> Max() {
        return _maxNode;
    }

    /// <summary>
    /// Removes the largest node of the heap.
    /// O(log n) amortized
    /// </summary>
    /// <returns></returns>
    public FibonacciHeapNode<T, TKey> RemoveMax() {
        FibonacciHeapNode<T, TKey> maxNode = _maxNode;

        if (maxNode != null) {
            int numKids = maxNode.Degree;
            FibonacciHeapNode<T, TKey> oldMaxChild = maxNode.Child;

            // for each child of maxNode do...
            while (numKids > 0) {
                FibonacciHeapNode<T, TKey> tempRight = oldMaxChild.Right;

                // remove oldMaxChild from child list
                oldMaxChild.Left.Right = oldMaxChild.Right;
                oldMaxChild.Right.Left = oldMaxChild.Left;

                // add oldMaxChild to root list of heap
                oldMaxChild.Left = _maxNode;
                oldMaxChild.Right = _maxNode.Right;
                _maxNode.Right = oldMaxChild;
                oldMaxChild.Right.Left = oldMaxChild;

                // set parent[oldMaxChild] to null
                oldMaxChild.Parent = null;
                oldMaxChild = tempRight;
                numKids--;
            }

            // remove maxNode from root list of heap
            maxNode.Left.Right = maxNode.Right;
            maxNode.Right.Left = maxNode.Left;

            if (maxNode == maxNode.Right) {
                _maxNode = null;
            } else {
                _maxNode = maxNode.Right;
                Consolidate();
            }

            // decrement size of heap
            _nNodes--;
        }

        return maxNode;
    }

    /// <summary>
    /// The number of nodes. O(1)
    /// </summary>
    /// <returns></returns>
    public int Size() {
        return _nNodes;
    }

    /// <summary>
    /// Joins two heaps. O(1)
    /// </summary>
    /// <param name="h1"></param>
    /// <param name="h2"></param>
    /// <returns></returns>
    public static FibonacciHeap<T, TKey> Union(FibonacciHeap<T, TKey> h1, FibonacciHeap<T, TKey> h2) {
        var h = new FibonacciHeap<T, TKey>(h1._maxKeyValue.CompareTo(h2._maxKeyValue) > 0
            ? h1._maxKeyValue
            : h2._maxKeyValue);

        if (h1 != null && h2 != null) {
            h._maxNode = h1._maxNode;

            if (h._maxNode != null) {
                if (h2._maxNode != null) {
                    h._maxNode.Right.Left = h2._maxNode.Left;
                    h2._maxNode.Left.Right = h._maxNode.Right;
                    h._maxNode.Right = h2._maxNode;
                    h2._maxNode.Left = h._maxNode;

                    if (h2._maxNode.Key.CompareTo(h1._maxNode.Key) > 0) {
                        h._maxNode = h2._maxNode;
                    }
                }
            } else {
                h._maxNode = h2._maxNode;
            }

            h._nNodes = h1._nNodes + h2._nNodes;
        }

        return h;
    }

    /// <summary>
    /// Performs a cascading cut operation. This cuts newChild from its parent and then
    /// does the same for its parent, and so on up the tree.
    /// </summary>
    private void CascadingCut(FibonacciHeapNode<T, TKey> y) {
        FibonacciHeapNode<T, TKey> z = y.Parent;

        // if there's a parent...
        if (z != null) {
            // if newChild is unmarked, set it marked
            if (!y.Mark) {
                y.Mark = true;
            } else {
                // it's marked, cut it from parent
                Cut(y, z);

                // cut its parent as well
                CascadingCut(z);
            }
        }
    }

    // EDIT
    static double Phi = Math.Log((1 + Math.Sqrt(5)) / 2);


    private void Consolidate() {
        int arraySize = (int)Math.Floor(Math.Log(_nNodes) / Phi) + 1;

        var array = new List<FibonacciHeapNode<T, TKey>>(arraySize);

        // Initialize degree array
        for (var i = 0; i < arraySize; i++) {
            array.Add(null);
        }

        // Find the number of root nodes.
        var numRoots = 0;
        FibonacciHeapNode<T, TKey> x = _maxNode;

        if (x != null) {
            numRoots++;
            x = x.Right;

            while (x != _maxNode) {
                numRoots++;
                x = x.Right;
            }
        }

        // For each node in root list do...
        while (numRoots > 0) {
            // Access this node's degree..
            int d = x.Degree;
            FibonacciHeapNode<T, TKey> next = x.Right;

            // ..and see if there's another of the same degree.
            for (; ; )
            {
                FibonacciHeapNode<T, TKey> y = array[d];
                if (y == null) {
                    // Nope.
                    break;
                }

                // There is, make one of the nodes a child of the other.
                // Do this based on the key value.
                if (x.Key.CompareTo(y.Key) < 0) {
                    FibonacciHeapNode<T, TKey> temp = y;
                    y = x;
                    x = temp;
                }

                // FibonacciHeapNode<T> newChild disappears from root list.
                Link(y, x);

                // We've handled this degree, go to next one.
                array[d] = null;
                d++;
            }

            // Save this node for later when we might encounter another
            // of the same degree.
            array[d] = x;

            // Move forward through list.
            x = next;
            numRoots--;
        }

        // Set max to null (effectively losing the root list) and
        // reconstruct the root list from the array entries in array[].
        _maxNode = null;

        for (var i = 0; i < arraySize; i++) {
            FibonacciHeapNode<T, TKey> y = array[i];
            if (y == null) {
                continue;
            }

            // We've got a live one, add it to root list.
            if (_maxNode != null) {
                // First remove node from root list.
                y.Left.Right = y.Right;
                y.Right.Left = y.Left;

                // Now add to root list, again.
                y.Left = _maxNode;
                y.Right = _maxNode.Right;
                _maxNode.Right = y;
                y.Right.Left = y;

                // Check if this is a new max.
                if (y.Key.CompareTo(_maxNode.Key) > 0) {
                    _maxNode = y;
                }
            } else {
                _maxNode = y;
            }
        }
    }

    /// <summary>
    /// The reverse of the link operation: removes newParent from the child list of newChild.
    /// This method assumes that min is non-null.
    /// Running time: O(1)
    /// </summary>
    private void Cut(FibonacciHeapNode<T, TKey> x, FibonacciHeapNode<T, TKey> y) {
        // remove newParent from childlist of newChild and decrement degree[newChild]
        x.Left.Right = x.Right;
        x.Right.Left = x.Left;
        y.Degree--;

        // reset newChild.child if necessary
        if (y.Child == x) {
            y.Child = x.Right;
        }

        if (y.Degree == 0) {
            y.Child = null;
        }

        // add newParent to root list of heap
        x.Left = _maxNode;
        x.Right = _maxNode.Right;
        _maxNode.Right = x;
        x.Right.Left = x;

        // set parent[newParent] to nil
        x.Parent = null;

        // set mark[newParent] to false
        x.Mark = false;
    }

    /// <summary>
    /// Makes newChild a child of Node newParent.
    /// O(1)
    /// </summary>
    private static void Link(FibonacciHeapNode<T, TKey> newChild, FibonacciHeapNode<T, TKey> newParent) {
        // remove newChild from root list of heap
        newChild.Left.Right = newChild.Right;
        newChild.Right.Left = newChild.Left;

        // make newChild a child of newParent
        newChild.Parent = newParent;

        if (newParent.Child == null) {
            newParent.Child = newChild;
            newChild.Right = newChild;
            newChild.Left = newChild;
        } else {
            newChild.Left = newParent.Child;
            newChild.Right = newParent.Child.Right;
            newParent.Child.Right = newChild;
            newChild.Right.Left = newChild;
        }

        // increase degree[newParent]
        newParent.Degree++;

        // set mark[newChild] false
        newChild.Mark = false;
    }
}