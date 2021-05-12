using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityPIC.Utils;

namespace UnityPIC
{
    using Base;
    using Internals;
    using Internals.Potree;

    namespace Core
    {
        namespace Traversal
        {
            using Node = UPICInternalNode;
            using Subtree = Potree<UPICInternalNode>.Subtree;

            public class ConcurrentTraversalStrategy : AbstractTraversalStrategy
            {
                private object _lock = new object();

                //private Thread _traversalThread;
                //JobThread<LoadSubtreeJob> _loadThread;

                private List<Subtree> _loadedSubtrees;
                private List<Subtree> _cancelledSubtrees;

                private (Matrix4x4, float) _targetCameraParams;
                private (float, float) _projectionParams;
                private int _pointBudget;

                private int _pendingSubtreeCount;
                private int _pendingNodeCount;

                private bool _updatedTraversalParams = false;
                private bool _updatedCameraState = false;
                private bool _initialized = false;

                private Vector3 _prevCameraPosition;
                private Quaternion _prevCameraRotation;

                public override int PointBudget
                {
                    get
                    {
                        lock (_lock)
                        {
                            return _pointBudget;
                        }
                    }
                    set
                    {
                        lock (_lock)
                        {
                            _updatedTraversalParams = _pointBudget != value;
                            _pointBudget = value;

                            if (_updatedTraversalParams)
                            {
                                //_traversalThread.Signal();
                            }
                        }
                    }
                }

                public override (float, float) ProjectionParams
                {
                    set
                    {
                        lock (_lock)
                        {
                            _updatedTraversalParams = _projectionParams != value;
                            _projectionParams = value;

                            if (_updatedTraversalParams)
                            {
                                //_traversalThread.Signal();
                            }
                        }
                    }
                }

                private void Update()
                {
                    if (targetCamera.transform.position != _prevCameraPosition || targetCamera.transform.rotation != _prevCameraRotation)
                    {
                        _prevCameraPosition = targetCamera.transform.position;
                        _prevCameraRotation = targetCamera.transform.rotation;

                        lock (_lock)
                        {
                            _updatedCameraState = true;
                            _targetCameraParams = (targetCamera.projectionMatrix * targetCamera.worldToCameraMatrix, targetCamera.nearClipPlane);
                            //_traversalThread.Signal();
                        }
                    }

                    threadMain(null);
                }

                protected override void OnDestroy()
                {
                    Dispose();
                }

                public ConcurrentTraversalStrategy()
                {
                    //_traversalThread = new Thread(threadMain);
                    //_loadThread = new JobThread<LoadSubtreeJob>();

                    _loadedSubtrees = new List<Subtree>();
                    _cancelledSubtrees = new List<Subtree>();

                    //_loadThread.OnJobComplete += onSubtreeLoaded;
                    //_loadThread.OnJobCancel += onSubtreeCancelled;

#if UNITY_EDITOR
                    //_traversalThread.Name = "UnityPIC::TraversalThread";
#endif
                }

                public override void Initialize(Camera targetCamera, Potree<Node> potree, Vector3 shift)
                {
                    base.Initialize(targetCamera, potree, shift);

                    //_loadThread.Start();
                    //_traversalThread.Start();

                    _initialized = true;
                }

                public override void Dispose()
                {
                    if (_initialized)
                    {
                        //_loadThread.Join();
                        //_traversalThread.Join();

                        base.Dispose();

                        _initialized = false;
                    }
                }

                public override void OnNodeLoading(Node node)
                {
                    lock (_lock)
                    {
                        ++_pendingNodeCount;
                    }
                }

                public override void OnNodeRendered(Node node)
                {
                    lock (_lock)
                    {
                        --_pendingNodeCount;
                        //_traversalThread.Signal();
                    }
                }
                public override void OnNodeCancelled(Node node)
                {
                    lock (_lock)
                    {
                        --_pendingNodeCount;
                        //_traversalThread.Signal();
                    }
                }

                protected override void loadSubtree(Subtree subtree)
                {

                    lock (_lock)
                    {
                        LoadSubtreeJob job = new LoadSubtreeJob() { Subtree = subtree };
                        subtree.Load();
                        onSubtreeLoaded(job);

                        ++_pendingSubtreeCount;
                    }
                }

                // executed by job thread on complete
                private void onSubtreeLoaded(LoadSubtreeJob job)
                {
                    lock (_lock)
                    {
                        _loadedSubtrees.Add(job.Subtree);
                        //_traversalThread.Signal();
                        --_pendingSubtreeCount;
                    }
                }

                private void onSubtreeCancelled(LoadSubtreeJob job)
                {
                    _cancelledSubtrees.Add(job.Subtree);

                    lock (_lock)
                    {
                        --_pendingSubtreeCount;
                    }
                }

                private void threadMain(object obj)
                {
                    bool updated;

                    lock (_lock)
                    {
                        updated = _updatedCameraState || _updatedTraversalParams;

                        for (int i = 0, c = _loadedSubtrees.Count; i < c; ++i)
                        {
                            subtreeLoadingCallback(_loadedSubtrees[i], true);
                        }

                        _loadedSubtrees.Clear();

                        if (_updatedTraversalParams)
                        {
                            base.PointBudget = _pointBudget;
                            base.ProjectionParams = _projectionParams;
                            _updatedTraversalParams = false;
                        }

                        if (_updatedCameraState)
                        {
                            base.CameraParams = _targetCameraParams;
                            _updatedCameraState = false;
                        }

                        if (isEndOfTraversal || _pendingNodeCount > 10)
                        {
                            //_traversalThread.Suspend();

                            if (_pendingSubtreeCount == 0 && _pendingNodeCount == 0)
                            {
                                // End of scene
                            }

                            //Debug.LogFormat("End of traversal {0} {1}", isEndOfTraversal, _pendingNodeCount);

                            return;
                        }
                    }

                    if (updated)
                    {
                        //_loadThread.Clear();

                        for (int i = 0, c = _cancelledSubtrees.Count; i < c; ++i)
                        {
                            subtreeLoadingCallback(_cancelledSubtrees[i], false);
                        }

                        _cancelledSubtrees.Clear();
                    }

                    int c2 = 10;

                    while (c2-- > 0)
                        traverse(1);
                }
            }
        }
    }
}