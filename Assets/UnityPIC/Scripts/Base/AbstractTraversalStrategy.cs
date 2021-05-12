using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPIC
{
    using Internals;
    using Internals.Potree;
    using System;
    using Utils;

    namespace Base
    {
        using Node = UPICInternalNode;
        using Subtree = Potree<UPICInternalNode>.Subtree;

        public abstract class AbstractTraversalStrategy : MonoBehaviour
        {
            private static float s_sqrt3 = Mathf.Sqrt(3);

            private enum Stage
            {
                Initiate,
                ReprojectRenderedCutQueue,
                ReprojectTraversalQueue,
                Traversal,
            }

            private static class TraversalFlags
            {
                public static byte InTraversal = 0;
                public static byte OnRenderedCut = 1;
                public static byte SubtreeLoading = 2;
            }

            private class ReverseFloatComparer : IComparer<float>
            {
                public int Compare(float x, float y)
                {
                    return -x.CompareTo(y);
                }
            }

            private Potree<Node> _potree;
            private FrustumCuller _frustumCuller;

            private PriorityQueue<Node, float> _renderedCutQueue;
            private PriorityQueue<Node, float> _traversalQueue;

            private Queue<Node> _removeQueue;
            private Queue<Node> _reprojectQueue;

            private Queue<(Subtree, bool)> _loadedSubtrees;

            private Vector4 _vpRow0;
            private Vector4 _vpRow1;
            private Vector4 _vpRow2;
            private Vector4 _vpRow3;
            private Vector4 _shift;
            private float _ncpDist;
            private long _currentCameraTimeStamp;
            private float _sigma_xy, _sigma_z;

            private int _pointBudget;
            private int _pointCount;

            private Stage _stage;

            private bool _initialized = false;

            protected Camera targetCamera;

            public UPICInternalNodeActionDelegate OnNodeAdd;
            public UPICInternalNodeActionDelegate OnNodeRemove;
            public UPICInternalNodeActionDelegate OnNodeUpdatePriority;
            public UPICInternalNodeActionDelegate OnNodeUpdateCullingMask;

            public virtual void OnNodeLoading(Node node) { }
            public virtual void OnNodeRendered(Node node) { }
            public virtual void OnNodeCancelled(Node node) { }

            protected bool isEndOfTraversal
            {
                get
                {
                    if (_stage == Stage.Traversal)
                    {
                        if (_traversalQueue.Count > 0)
                        {
                            Node node = _traversalQueue.Peek();

                            bool isTraversalEnd = node.Priority == 0 && _loadedSubtrees.Count == 0;
                            bool isAtBudgetLimit = node.PointCount + _pointCount > _pointBudget;
                            bool isMaximized = _renderedCutQueue.Count > 0 && _renderedCutQueue.Peek().Priority >= node.Priority;

                            if (isTraversalEnd || (isAtBudgetLimit && isMaximized))
                            {
                                return true;
                            }
                        }
                        else
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            public virtual int PointBudget
            {
                get => _pointBudget;
                set
                {
                    if (_pointBudget != value)
                    {
                        _stage = Stage.Initiate;
                    }

                    _pointBudget = value;
                }
            }

            public virtual (float, float) ProjectionParams
            {
                set
                {
                    _sigma_xy = Mathf.Sqrt(1 - value.Item1);
                    _sigma_z = Mathf.Sqrt(1 - value.Item2);
                }
            }

            public virtual (Matrix4x4, float) CameraParams
            {
                set
                {
                    _frustumCuller.CameraParams = value;
                    _vpRow0 = value.Item1.GetRow(0);
                    _vpRow1 = value.Item1.GetRow(1);
                    _vpRow2 = value.Item1.GetRow(2);
                    _vpRow3 = value.Item1.GetRow(3);
                    _ncpDist = value.Item2;
                    _currentCameraTimeStamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    _stage = Stage.Initiate;
                }
            }

            protected virtual void OnDestroy()
            {
                Dispose();
            }

            public virtual void Initialize(Camera cam, Potree<Node> potree, Vector3 shift)
            {
                targetCamera = cam;

                _potree = potree;

                _frustumCuller = new FrustumCuller();

                _renderedCutQueue = new PriorityQueue<Node, float>(new ReverseFloatComparer());
                _traversalQueue = new PriorityQueue<Node, float>();

                _removeQueue = new Queue<Node>();
                _reprojectQueue = new Queue<Node>();

                _loadedSubtrees = new Queue<(Subtree, bool)>();

                _shift = shift;
                _currentCameraTimeStamp = 0;
                _pointCount = 0;

                _potree.Root.Priority = float.MaxValue;
                addToTraversalQueue(_potree.Root);

                _stage = Stage.Initiate;

                _initialized = true;
            }

            public virtual void Dispose()
            {
                if (_initialized)
                {
#if UNITYPIC_DEBUG
                    sanityCheck();
#endif

                    if (!_potree.Root.StatusFlags.Has(TraversalFlags.InTraversal))
                    {
                        removeNode(_potree.Root);
                    }

                    _initialized = false;
                }
            }

            protected abstract void loadSubtree(Subtree subtree);

            //private void InitiateNewTraversal()
            //{
            //    reprojectPriorityQueue(_renderedCutQueue);

            //    while (_renderedCutQueue.Count > 0 && _renderedCutQueue.Peek().Priority == 0)
            //    {
            //        removeNode(_renderedCutQueue.Peek());
            //    }

            //    reprojectPriorityQueue(_traversalQueue);

            //    while (_pointCount > _pointBudget)
            //    {
            //        removeNode(_renderedCutQueue.Peek());
            //    }
            //}

            //private void reprojectPriorityQueue(PriorityQueue<Node, float> pq)
            //{
            //    using (IEnumerator<Node> enumerator = pq.GetEnumerator())
            //    {
            //        while (enumerator.MoveNext())
            //        {
            //            _reprojectList.Add(enumerator.Current);
            //        }
            //    }

            //    pq.Clear();

            //    for (int i = 0, c = _reprojectList.Count; i < c; ++i)
            //    {
            //        Node node = _reprojectList[i];
            //        projectNode(node);
            //        node.NodePQRef = pq.Enqueue(node, node.Priority);
            //    }

            //    _reprojectList.Clear();
            //}

            protected void traverse(int iterations)
            {
                collectLoadedSubtrees();

                Node node;
                int c;

                switch (_stage)
                {
                    case Stage.Initiate:
                        _reprojectQueue.Clear();

                        using (var enumerator = _renderedCutQueue.GetEnumerator())
                        {
                            while (enumerator.MoveNext())
                            {
                                _reprojectQueue.Enqueue(enumerator.Current);
                            }
                        }

                        _stage = Stage.ReprojectRenderedCutQueue;
                        break;

                    case Stage.ReprojectRenderedCutQueue:
                        c = Mathf.Min(64, _reprojectQueue.Count);

                        while (c-- > 0)
                        {
                            node = _reprojectQueue.Dequeue();

                            if (node.StatusFlags.Has(TraversalFlags.OnRenderedCut))
                            {
                                projectNode(node);

                                if (node.Priority == 0)
                                {
                                    removeNode(node);
                                }
                                else
                                {
                                    _renderedCutQueue.UpdatePriority(ref node.NodePQRef, node.Priority);
                                }
                            }
                        }

                        if (_reprojectQueue.Count == 0)
                        {
                            using (var enumerator = _traversalQueue.GetEnumerator())
                            {
                                while (enumerator.MoveNext())
                                {
                                    _reprojectQueue.Enqueue(enumerator.Current);
                                }
                            }

                            _stage = Stage.ReprojectTraversalQueue;
                        }

                        break;
                    case Stage.ReprojectTraversalQueue:
                        c = Mathf.Min(128, _reprojectQueue.Count);

                        while (c-- > 0)
                        {
                            node = _reprojectQueue.Dequeue();
                            projectNode(node);
                            _traversalQueue.UpdatePriority(ref node.NodePQRef, node.Priority);
                        }

                        if (_reprojectQueue.Count == 0)
                        {
                            _stage = Stage.Traversal;
                        }
                        break;
                    case Stage.Traversal:
                        while (_traversalQueue.Count > 0 && iterations-- > 0 && _traversalQueue.Peek().Priority > 0) {
                            node = _traversalQueue.Peek();

                            if (!node.Subtree.IsLoaded)
                            {
                                _traversalQueue.UpdatePriority(ref node.NodePQRef, 0);

                                if (!node.StatusFlags.Has(TraversalFlags.SubtreeLoading))
                                {
                                    node.StatusFlags += TraversalFlags.SubtreeLoading;
                                    loadSubtree(node.Subtree);
                                }

                                return;
                            }

                            if (node.PointCount + _pointCount > _pointBudget)
                            {
                                if (_renderedCutQueue.Count > 0 && _renderedCutQueue.Peek().Priority < node.Priority)
                                {
                                    removeNode(_renderedCutQueue.Peek());
                                    continue;
                                }
                                else
                                {
                                    return;
                                }
                            }

                            addNode(node);
                        }

                        break;
                }
            }

            protected void subtreeLoadingCallback(Subtree subtree, bool isSuccess)
            {
                _loadedSubtrees.Enqueue((subtree, isSuccess));
            }

            private void collectLoadedSubtrees()
            {
                while (_loadedSubtrees.Count > 0)
                {
                    (Subtree subtree, bool isSuccess) = _loadedSubtrees.Dequeue();

                    Node node = subtree.Root;

                    node.Subtree.IsLoaded = isSuccess;
                    node.StatusFlags -= TraversalFlags.SubtreeLoading;

                    if (node.StatusFlags.Has(TraversalFlags.InTraversal))
                    {
                        _traversalQueue.UpdatePriority(ref node.NodePQRef, node.Priority);
                    }
                }
            }


            private void projectNode(Node node)
            {
                if (node.UpdatedTimeStamp != _currentCameraTimeStamp)
                {
                    node.UpdatedTimeStamp = _currentCameraTimeStamp;

                    Vector3 center;
                    center.x = node.Center.x - _shift.x;
                    center.y = node.Center.y - _shift.y;
                    center.z = node.Center.z - _shift.z;

                    if (_frustumCuller.Cull(center, node.Extents))
                    {
                        float w = _vpRow3.x * center.x + _vpRow3.y * center.y + _vpRow3.z * center.z + _vpRow3.w - node.Extents.x * s_sqrt3;
                        float x = (_vpRow0.x * center.x + _vpRow0.y * center.y + _vpRow0.z * center.z + _vpRow0.w) / w;
                        float y = (_vpRow1.x * center.x + _vpRow1.y * center.y + _vpRow1.z * center.z + _vpRow1.w) / w;
                        float z = (_vpRow2.x * center.x + _vpRow2.y * center.y + _vpRow2.z * center.z + _vpRow2.w) / w;

                        x = (x < -1) ? -1 : ((x > 1) ? 1 : x);
                        y = (y < -1) ? -1 : ((y > 1) ? 1 : y);
                        z = (z < 0) ? 0 : ((z > 1) ? 1 : z);

                        float w1 = Mathf.Max(Mathf.Sqrt(2) - (_sigma_xy * _sigma_xy * (x * x + y * y)), 0);
                        float w2 = Mathf.Max(1 - (_sigma_z * _sigma_z * z * z), 0);

                        float priority = node.Spacing / ((w < _ncpDist) ? _ncpDist : w) * w1 * w2;

                        if (node.Parent != null)
                        {
                            Node parent = node.Parent;

                            priority = Mathf.Min(priority, parent.Priority);

                            if (priority > 0 && node.Priority == 0)
                            {
                                parent.CullingMask += node.OctantId;
                            }
                            else if (priority == 0 && node.Priority > 0)
                            {
                                parent.CullingMask -= node.OctantId;
                            }

                            OnNodeUpdateCullingMask?.Invoke(parent);
                        }

                        node.Priority = priority;

                        //Debug.LogFormat("{0} {1} {2} {3}", node.FullName, node.Center - (Vector3)_shift, node.Extents, node.Priority);
                    }
                    else
                    {
                        node.Priority = 0;
                    }

                    if (node.StatusFlags.Has(TraversalFlags.OnRenderedCut))
                    {
                        OnNodeUpdatePriority?.Invoke(node);
                    }
                }
            }

            private void addToRenderedCutQueue(Node node)
            {
                //Debug.LogFormat("{0} added to rendered cut", node.FullName);

                node.NodePQRef = _renderedCutQueue.Enqueue(node, node.Priority);
                node.StatusFlags += TraversalFlags.OnRenderedCut;
            }

            private void removeFromRenderedCutQueue(Node node)
            {
                //Debug.LogFormat("{0} removed from rendered cut", node.FullName);

                _renderedCutQueue.Remove(ref node.NodePQRef);
                node.StatusFlags -= TraversalFlags.OnRenderedCut;
            }

            private void addToTraversalQueue(Node node)
            {
                node.NodePQRef = _traversalQueue.Enqueue(node, node.Priority);
                node.StatusFlags += TraversalFlags.InTraversal;

                //Debug.LogFormat("{0} added to traversal", node.FullName);

                if (node.Parent != null)
                {
                    node.Parent.TraversalMask += node.OctantId;
                }
            }

            private void removeFromTraversalQueue(Node node)
            {
                //Debug.LogFormat("{0} removed from traversal", node.FullName);

                _traversalQueue.Remove(ref node.NodePQRef);
                node.StatusFlags -= TraversalFlags.InTraversal;

                if (node.Parent != null)
                {
                    Node parent = node.Parent;

                    parent.TraversalMask -= node.OctantId;

                    if (parent.TraversalMask.IsEmpty) // all children are in rendered queue;
                    {
                        removeFromRenderedCutQueue(node.Parent);
                    }
                }
            }

            private void addNode(Node node)
            {
                removeFromTraversalQueue(node);
                addToRenderedCutQueue(node);

                if (!node.IsLeaf) // only render internal nodes, all leaf nodes are sentinel nodes;
                {
                    _pointCount += node.PointCount;
                    OnNodeAdd?.Invoke(node);

                    Node child;

                    for (int i = 0; i < 8; ++i)
                    {
                        child = node.Children[i];
                        projectNode(child);
                        addToTraversalQueue(child);
                    }
                }
            }

            private void removeNode(Node node)
            {
#if UNITYPIC_DEBUG
                if (node.StatusFlags.Has(TraversalFlags.InTraversal))
                {
                    throw new Exception();
                }
#endif
                Node n = node;

                while (node.Parent != null)
                {
                    Node parent = node.Parent;

                    projectNode(parent);

                    if (parent.Priority > 0)
                    {
                        break;
                    }
                    else
                    {
                        node = parent;
                    }
                }

                if (node.StatusFlags.Has(TraversalFlags.InTraversal))
                {
                    throw new Exception();
                }

                _removeQueue.Enqueue(node);

                while (_removeQueue.Count > 0)
                {
                    Node current = _removeQueue.Dequeue();

                    if (current.StatusFlags.Has(TraversalFlags.InTraversal))
                    {
                        removeFromTraversalQueue(current);
                    }
                    else
                    {
                        if (current.StatusFlags.Has(TraversalFlags.OnRenderedCut) && current.PointCount == -1)
                        {
                            removeFromRenderedCutQueue(current);
                        }

                        if (current.Subtree.IsLoaded && !current.IsLeaf)
                        {
                            _pointCount -= current.PointCount;
                            OnNodeRemove?.Invoke(current);

                            for (int i = 0; i < 8; ++i)
                            {
                                _removeQueue.Enqueue(current.Children[i]);
                            }
                        }
                        else
                        {
                        }
                    }
                }

                if (node.Parent != null)
                {
                    Node parent = node.Parent;

                    if (!parent.StatusFlags.Has(TraversalFlags.OnRenderedCut)) {
                        projectNode(node.Parent);
                        addToRenderedCutQueue(node.Parent);
                    }
                }

                addToTraversalQueue(node);

#if UNITYPIC_DEBUG
                if (!node.IsLeaf)
                {
                    for (int i = 0; i < 8; ++i)
                    {
                        ByteMask statusTraversalFlags = node.Children[i].StatusFlags;
                        if (statusTraversalFlags.Has(TraversalFlags.InTraversal) || statusTraversalFlags.Has(TraversalFlags.OnRenderedCut))
                        {
                            throw new Exception();
                        }
                    }
                }
#endif
            }

#if UNITYPIC_DEBUG
            private void sanityCheck()
            {
                Queue<Node> q = new Queue<Node>();

                q.Enqueue(_potree.Root);

                while (q.Count > 0)
                {
                    Node node = q.Dequeue();

                    if (node.Subtree.IsLoaded && node.IsLeaf)
                    {
                        if (!node.StatusFlags.Has(TraversalFlags.InTraversal) && !node.StatusFlags.Has(TraversalFlags.OnRenderedCut))
                        {
                            throw new Exception();
                        }
                    }

                    if (node.StatusFlags.Has(TraversalFlags.InTraversal))
                    {
                    }
                    else
                    {
                        if (node.StatusFlags.Has(TraversalFlags.OnRenderedCut))
                        {
                            if (!node.IsLeaf)
                            {
                                if (node.TraversalMask == ByteMask.None)
                                {
                                    throw new Exception();
                                }
                                else
                                {
                                    for (int i = 0; i < 8; ++i)
                                    {
                                        if (node.TraversalMask.Has((byte)i))
                                        {
                                            if (!node.Children[i].StatusFlags.Has(TraversalFlags.InTraversal))
                                            {
                                                throw new Exception();
                                            }
                                        }
                                    }
                                }

                                for (int i = 0; i < 8; ++i)
                                {
                                    q.Enqueue(node.Children[i]);
                                }
                            }
                            else
                            {
                                if (node.TraversalMask != ByteMask.None)
                                {
                                    throw new Exception();
                                }
                            }
                        }

                    }
                }
            }
#endif
        }
    }
}
