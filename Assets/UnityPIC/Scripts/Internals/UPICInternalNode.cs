using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace UnityPIC
{
    using Core;
    using Graphics.PackingFormat;
    using Internals.Potree;
    using Unity.Collections;
    using Utils;

    namespace Internals
    {
        using NodePQRef = PriorityQueue<UPICInternalNode, float>.Ref;

        public delegate void UPICInternalNodeActionDelegate(UPICInternalNode node);

        public unsafe class UPICInternalNode : BaseNode<UPICInternalNode>
        {
            public enum NodeStatus
            {
                Traversal,
                Load,
                Rendered,
                PendingRender,
                PendingRemove,
                CancelledRender,
                CancelledRemove,
            }

            public int PartitionPointCount(int i)
            {
                return PartitionOffsets[i + 1] - PartitionOffsets[i];
            }

            public int AugmentedPointCount { get => PartitionOffsets[8]; }

            public ByteMask OccupancyMask
            {
                get
                {
                    ByteMask m = new ByteMask();

                    for (int i = 0; i < 8; ++i)
                    {
                        if (PartitionOffsets[i + 1] - PartitionOffsets[i] > 0)
                        {
                            m += (byte)i;
                        }
                    }

                    return m;
                }
            }

            public UPICInternalNode()
            {
                Status = NodeStatus.Traversal;
            }

            public override string ToString()
            {
                return FullName;
            }

            public NodeStatus Status;

            // For traversal
            public long UpdatedTimeStamp;
            public float Priority;
            public ByteMask StatusFlags;
            public ByteMask TraversalMask;
            public ByteMask CullingMask;
            public NodePQRef NodePQRef;

            // For point loading
            public PosColIntCls20[] Points;
            public int[] PartitionOffsets;
            public Vector3[] Edges;

            public LoadPointsJob LoadPointsJob;

            // For rendering
            public (float, float) IntensityRange;
            public UPICNode UPICNode;
            public bool IsPendingUpdate;
            public ByteMask CullingMaskSnapshot;
        }
    }
}
