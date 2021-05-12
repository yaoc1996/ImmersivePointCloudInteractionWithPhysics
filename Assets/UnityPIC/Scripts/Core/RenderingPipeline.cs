using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPIC
{
    using Internals;
    using Internals.Potree;

    namespace Core
    {
        using Base;
        using Load;
        using Traversal;
        using Render;

        using NodeStatus = UPICInternalNode.NodeStatus;
        using System.Runtime.InteropServices;
        using System;

        public class RenderingPipeline : MonoBehaviour
        {
            private object _lock = new object();
            private object _lockMain = new object();

            private Potree<UPICInternalNode> _potree;

            private AbstractTraversalStrategy _traversalStrategy;
            private AbstractLoadStrategy _loadStrategy;
            private AbstractRenderStrategy _renderStrategy;

            private Queue<UPICInternalNode> _renderQueue;
            private Queue<UPICInternalNode> _removeQueue;
            private Queue<UPICInternalNode> _updateQueue;
            private List<UPICNode> _nodeList;

            private static int _renderThreshold = 5;
            private static int _removeThreshold = 20;

            private Vector3 _shift;

            public UPICNodeActionDelegate OnNodeRendered;
            public UPICNodeActionDelegate OnNodeRemoved;
            public UPICNodeActionDelegate OnNodeUpdated;

            public Vector3 Shift { get => _shift; }

            public RenderingPipeline()
            {
                _renderQueue = new Queue<UPICInternalNode>();
                _removeQueue = new Queue<UPICInternalNode>();
                _updateQueue = new Queue<UPICInternalNode>();
                _nodeList = new List<UPICNode>();
            }

            public void Initialize(Camera renderCamera, Camera traversalCamera, string dir, TraversalParams traversalParams)
            {
                Application.targetFrameRate = 60;

                _potree = new Potree<UPICInternalNode>(dir);
                _shift = _potree.Root.Center;

                _traversalStrategy = gameObject.AddComponent<ConcurrentTraversalStrategy>();
                _loadStrategy = gameObject.AddComponent<ConcurrentLoadStrategy>();
                _renderStrategy = gameObject.AddComponent<ParaboloidInterpolationRenderStrategy>();

                _traversalStrategy.OnNodeAdd += onNodeAdd;
                _traversalStrategy.OnNodeRemove += onNodeRemove;
                _traversalStrategy.OnNodeUpdatePriority += onNodeUpdatePriority;
                _traversalStrategy.OnNodeUpdateCullingMask += onNodeUpdateCullingMask;

                _loadStrategy.OnNodeLoaded += onNodeLoaded;

                _traversalStrategy.PointBudget = traversalParams.PointBudget;
                _traversalStrategy.ProjectionParams = (0.5f, 0.5f);

                _traversalStrategy.Initialize(traversalCamera, _potree, _shift);
                _loadStrategy.Initialize();
                _renderStrategy.Initialize(renderCamera, _shift);

                OnNodeRendered += _renderStrategy.OnNodeRender;
                OnNodeRemoved += _renderStrategy.OnNodeRemove;
                OnNodeUpdated += _renderStrategy.OnNodeUpdate;

                renderCamera.farClipPlane = _potree.Root.Extents.x * 4;
                renderCamera.depthTextureMode = DepthTextureMode.Depth;
                renderCamera.gameObject.AddComponent<SceneIntegration>();

                //traversalCamera.farClipPlane = _potree.Root.Extents.x * 4;
            }

            private void Update()
            {
                processRenderQueue();
                processRemoveQueue();
                processUpdateQueue();
            }

            private void OnDestroy()
            {
                _traversalStrategy.Dispose();
                _loadStrategy.Dispose();

                while (_renderQueue.Count > 0) processRenderQueue();
                while (_removeQueue.Count > 0) processRemoveQueue();

                _renderStrategy.Dispose();
            }

            private void onNodeAdd(UPICInternalNode node)
            {
                        switch (node.Status)
                        {
                            case NodeStatus.PendingRemove:
                                node.Status = NodeStatus.CancelledRemove;
                                break;
                            case NodeStatus.CancelledRender:
                                node.Status = NodeStatus.PendingRender;
                                break;
                            case NodeStatus.Traversal:
                                node.Status = NodeStatus.Load;
                                node.LoadPointsJob = _loadStrategy.Load(node, node.Priority);
                                node.CullingMaskSnapshot = node.CullingMask;
                                _traversalStrategy.OnNodeLoading(node);
                                break;
#if UNITYPIC_DEBUG
                            default:
                                throw new System.Exception();
#endif
                        }
            }

            private void onNodeRemove(UPICInternalNode node)
            {
                        switch (node.Status)
                        {
                            case NodeStatus.Rendered:
                                _removeQueue.Enqueue(node);
                                node.Status = NodeStatus.PendingRemove;
                                break;
                            case NodeStatus.PendingRender:
                                node.Status = NodeStatus.CancelledRender;
                                break;
                            case NodeStatus.Load:
                                _loadStrategy.Cancel(node);
                                node.LoadPointsJob = null;
                                node.Status = NodeStatus.Traversal;
                                _traversalStrategy.OnNodeCancelled(node);
                                break;
                            case NodeStatus.CancelledRemove:
                                node.Status = NodeStatus.PendingRemove;
                                break;
#if UNITYPIC_DEBUG
                            default:
                                throw new System.Exception();
#endif
                        }
            }

            private void onNodeUpdateCullingMask(UPICInternalNode node)
            {
                        node.CullingMaskSnapshot = node.CullingMask;

                        switch (node.Status)
                        {
                            case NodeStatus.Rendered:
                                if (!node.IsPendingUpdate)
                                {
                                    _updateQueue.Enqueue(node);
                                    node.IsPendingUpdate = true;
                                }
                                break;
                            default:
                                break;
                        }
            }

            private void onNodeUpdatePriority(UPICInternalNode node)
            {
                        switch (node.Status)
                        {
                            case NodeStatus.Load:
                                _loadStrategy.UpdatePriority(node, node.Priority);
                                break;
                            default:
                                break;
                        }
            }

            private void onNodeLoaded(UPICInternalNode node)
            {
                        switch (node.Status)
                        {
                            case NodeStatus.Load:
                                node.Status = NodeStatus.PendingRender;
                                _renderQueue.Enqueue(node);
                                break;
                            case NodeStatus.Traversal:
                                if (node.LoadPointsJob != null && node.LoadPointsJob.State == LoadPointsJob.JobState.Cancelled)
                                {
                                    node.Points = null;
                                }
                                break;
#if UNITYPIC_DEBUG
                            default:
                                throw new System.Exception();
#endif
                        }

                        node.LoadPointsJob = null;
            }

            private void processRenderQueue()
            {
                UPICInternalNode node;
                int i, c;

                    c = Mathf.Min(_renderQueue.Count, _renderThreshold);
                    while (c-- > 0)
                    {
                        node = _renderQueue.Dequeue();

                        switch (node.Status)
                        {
                            case NodeStatus.PendingRender:
                                node.Status = NodeStatus.Rendered;
                                node.UPICNode = new UPICNode(node);
                                _nodeList.Add(node.UPICNode);
                                _traversalStrategy.OnNodeRendered(node);
                                break;
                            case NodeStatus.CancelledRender:
                                node.UPICNode = null;
                                node.Points = null;
                                node.Status = NodeStatus.Traversal;
                                _traversalStrategy.OnNodeCancelled(node);
                                break;
#if UNITYPIC_DEBUG
                            default:
                                throw new System.Exception();
#endif
                        }
                    }

                for (i = 0, c = _nodeList.Count; i < c; ++i)
                {
                    _nodeList[i].Initialize();
                    addInheritance(_nodeList[i]);
                    OnNodeRendered?.Invoke(_nodeList[i]);
                }

                _nodeList.Clear();
            }

            private void processRemoveQueue()
            {
                UPICInternalNode node;
                int i, c;


                    c = Mathf.Min(_removeQueue.Count, _removeThreshold);
                    while (c-- > 0)
                    {
                        node = _removeQueue.Dequeue();

                        switch (node.Status)
                        {
                            case NodeStatus.PendingRemove:
                                _nodeList.Add(node.UPICNode);

                                node.UPICNode = null;
                                node.Points = null;
                                node.Status = NodeStatus.Traversal;

                                break;
                            case NodeStatus.CancelledRemove:
                                node.UPICNode.CullingMask = node.CullingMaskSnapshot;
                                node.Status = NodeStatus.Rendered;
                                break;
#if UNITYPIC_DEBUG
                            default:
                                throw new System.Exception();
#endif
                        }
                    }

                for (i = 0, c = _nodeList.Count; i < c; ++i)
                {
                    OnNodeRemoved?.Invoke(_nodeList[i]);
                    removeInheritance(_nodeList[i]);
                    _nodeList[i].Dispose();
                }

                _nodeList.Clear();
            }

            private void processUpdateQueue()
            {
                UPICInternalNode node;
                int c;

                    c = _updateQueue.Count;
                    while (c-- > 0)
                    {
                        node = _updateQueue.Dequeue();
                        node.IsPendingUpdate = false;

                        if (node.UPICNode != null)
                        {
                            node.UPICNode.CullingMask = node.CullingMaskSnapshot;
                            node.UPICNode.UploadRenderMask();
                            OnNodeUpdated?.Invoke(node.UPICNode);
                        }
                    }
            }

            private void addInheritance(UPICNode node)
            {
                if (node.Parent != null)
                {
                    node.Parent.InheritanceMask -= node.InternalNode.OctantId;
                    node.Parent.UploadRenderMask();
                    OnNodeUpdated?.Invoke(node.Parent);
                }
            }

            private void removeInheritance(UPICNode node)
            {
                if (node.Parent != null)
                {
                    node.Parent.InheritanceMask += node.InternalNode.OctantId;
                    node.Parent.UploadRenderMask();
                    OnNodeUpdated?.Invoke(node.Parent);
                }
            }

            private delegate void ManagedCallback();

            [DllImport("UnityPICNative")]
            private extern static void execute(ManagedCallback cb);
        }
    }
}
