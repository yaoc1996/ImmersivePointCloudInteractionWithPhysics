using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPIC
{
    using Base;
    using Internals.Potree;
    using Graphics.PackingFormat;
    using Utils;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine.Networking;

    namespace Internals
    {
        using Node = UPICInternalNode;
        using Subtree = Potree<UPICInternalNode>.Subtree;
        using PointAttributeDescriptor = CloudJs.PointAttributeDescriptor;

        public class LoadPointsJob : AbstractJob
        {
            private static int s_xBit = 1 << (2 - Constants.XAxisIndex);
            private static int s_yBit = 1 << (2 - Constants.YAxisIndex);
            private static int s_zBit = 1 << (2 - Constants.ZAxisIndex);

            public enum JobStage
            {
                Initial,
                Reading,
                Parsing,
                WaitingForParentPoints,
                Batching,
                PostLoading,
            }

            public enum JobState
            {
                Initial,
                Loading,
                Cancelled,
            }

            private byte[] _rawPointData;
            private PosColIntCls20[] _originalPoints;
            private PosColIntCls20[] _parentPoints;
            private int[] _parentPartitionOffsets;

            public JobStage Stage;
            public JobState State;

            public Node Node;
            public float Priority;

            public PosColIntCls20[] AugmentedPoints;
            public int[] PartitionOffsets;

            public Vector3[] Edges;

            public (float, float) IntensityRange;

            public LoadPointsJob(Node node, float p)
            {
                Stage = JobStage.Initial;
                State = JobState.Initial;

                Node = node;
                Priority = p;
            }

            public bool PrepareForDynamicBatching()
            {
                if (Node.Parent == null)
                {
                    return true;
                }

                if (Node.Parent.Points == null)
                {
                    return false;
                }

                _parentPoints = Node.Parent.Points;
                _parentPartitionOffsets = Node.Parent.PartitionOffsets;

                return true;
            }

            public override void Execute(object args)
            {
                switch (Stage)
                {
                    case JobStage.Reading:
                        readPointData(Node);
                        break;
                    case JobStage.Parsing:
                        parsePointData(Node, _rawPointData);
                        break;
                    case JobStage.Batching:
                        dynamicBatching(Node, _originalPoints, _parentPoints);
                        break;
                    case JobStage.PostLoading:
                        computeIntensityRange();
                        computeEdges();
                        break;
                    default:
                        break;
                }
            }

            private void readPointData(Node node)
            {
                Subtree subtree = node.Subtree;

                string dir = subtree.Potree.DataDirectory + subtree.SubDirectory + subtree.FileNamePrefix + node.LocalName + Constants.BinExt;

                if (Application.platform == RuntimePlatform.Android)
                {
                    var loadingRequest = UnityWebRequest.Get(dir);
                    loadingRequest.SendWebRequest();

                    while (!loadingRequest.isDone)
                    {
                        if (loadingRequest.isHttpError || loadingRequest.isNetworkError)
                        {
                            break;
                        }
                    }

                    _rawPointData = loadingRequest.downloadHandler.data;
                }
                else
                {
                    _rawPointData = File.ReadAllBytes(dir);
                }

            }

            private unsafe void parsePointData(Node node,  byte[] rawPointData)
            {
                Vector3 pos;
                byte* p;
                int i, c;

                Potree<Node> potree = node.Subtree.Potree;
                List<PointAttributeDescriptor> attrDesc = potree.Info.PointAttributes;

                int colOffset = (potree.ColorDescriptorIndex != -1) ? attrDesc[potree.ColorDescriptorIndex].Offset : 0;
                int intOffset = (potree.IntensityDescriptorIndex != -1) ? attrDesc[potree.IntensityDescriptorIndex].Offset : 0;
                int clsOffset = (potree.ClassificationDescriptorIndex != -1) ? attrDesc[potree.ClassificationDescriptorIndex].Offset : 0;

                Vector3 center = node.Center;
                Vector3 offset = center - node.Extents;

                float scale = potree.Info.Scale;
                int rawPointStride = potree.PointStride;

                _originalPoints = new PosColIntCls20[node.PointCount];

                fixed (byte* ptr = rawPointData)
                {
                    for (i = 0, p = ptr, c = node.PointCount; i < c; ++i, p += rawPointStride)
                    {
                        pos.x = ((uint*)p)[Constants.XAxisIndex] * scale + offset.x;
                        pos.y = ((uint*)p)[Constants.YAxisIndex] * scale + offset.y;
                        pos.z = ((uint*)p)[Constants.ZAxisIndex] * scale + offset.z;

                        _originalPoints[i].Position = pos;
                        _originalPoints[i].Color = *(Color32*)(p + colOffset);
                        _originalPoints[i].Attributes = (((uint)*(p + clsOffset)) << 16) + *(ushort*)(p + intOffset);

                    }
                }
            }

            private unsafe void dynamicBatching(Node node, PosColIntCls20[] nodePoints, PosColIntCls20[] parentPoints)
            {
                int i, c, o, pointCount;

                Vector3 pos, center;

                center = node.Center;
                pointCount = node.PointCount;

                PartitionOffsets = new int[9];
                int[] partitionCounts = new int[8];

                int parentPartitionOffset = 0, parentPartitionPointCount = 0;

                if (parentPoints != null)
                {
                    parentPartitionOffset = node.Parent.PartitionOffsets[node.OctantId];
                    parentPartitionPointCount = node.Parent.PartitionPointCount(node.OctantId);
                    AugmentedPoints = new PosColIntCls20[pointCount + parentPartitionPointCount];
                }
                else
                {
                    AugmentedPoints = new PosColIntCls20[pointCount];
                }

                int[] octants = new int[AugmentedPoints.Length];

                for (i = 0, c = node.PointCount; i < c; ++i)
                {
                    pos = nodePoints[i].Position;
                    o = ((pos.x > center.x) ? s_xBit : 0) | ((pos.y > center.y) ? s_yBit : 0) | ((pos.z > center.z) ? s_zBit : 0);

                    octants[i] = o;
                    ++partitionCounts[o];
                }

                if (parentPoints != null)
                {
                    for (i = 0, c = parentPartitionPointCount; i < c; ++i)
                    {
                        pos = parentPoints[parentPartitionOffset + i].Position;
                        o = ((pos.x > center.x) ? s_xBit : 0) | ((pos.y > center.y) ? s_yBit : 0) | ((pos.z > center.z) ? s_zBit : 0);

                        octants[i + pointCount] = o;
                        ++partitionCounts[o];
                    }
                }

                for (i = 0; i < 8; ++i)
                {
                    PartitionOffsets[i + 1] = PartitionOffsets[i] + partitionCounts[i];
                    partitionCounts[i] = PartitionOffsets[i];
                }

                for (i = 0, c = node.PointCount; i < c; ++i)
                {
                    AugmentedPoints[partitionCounts[octants[i]]++] = nodePoints[i];
                }

                if (parentPoints != null)
                {
                    for (i = 0, c = parentPartitionPointCount; i < c; ++i)
                    {
                        AugmentedPoints[partitionCounts[octants[i + pointCount]]++] = parentPoints[i + parentPartitionOffset];
                    }
                }
            }

            private void computeIntensityRange()
            {
                if (Node.Subtree.Potree.IntensityDescriptorIndex != -1)
                {

                    float mean = 0;
                    float variance = 0;
                    float diff, std;

                    for (int i = 0, c = _originalPoints.Length; i < c; ++i)
                    {
                        mean += _originalPoints[i].Attributes & 0xFFFF;
                    }

                    mean /= _originalPoints.Length;

                    for (int i = 0, c = _originalPoints.Length; i < c; ++i)
                    {
                        diff = (_originalPoints[i].Attributes & 0xFFFF) - mean;
                        variance += diff * diff;
                    }

                    variance /= _originalPoints.Length;
                    std = Mathf.Sqrt(variance);

                    IntensityRange = (Mathf.Max(mean - 2 * std, 0), Mathf.Min(mean + 2 * std, 65535.0f));
                }
            }


            private unsafe void computeEdges()
            {
                Vector3 min = Node.Center - Node.Extents;
                Vector3 max = Node.Center + Node.Extents;

                Edges = new Vector3[24];

                Edges[0] = new Vector3(min.x, min.y, min.z);
                Edges[1] = new Vector3(max.x, min.y, min.z);
                Edges[2] = new Vector3(min.x, max.y, min.z);
                Edges[3] = new Vector3(max.x, max.y, min.z);
                Edges[4] = new Vector3(min.x, min.y, max.z);
                Edges[5] = new Vector3(max.x, min.y, max.z);
                Edges[6] = new Vector3(min.x, max.y, max.z);
                Edges[7] = new Vector3(max.x, max.y, max.z);
                Edges[8] = new Vector3(min.x, min.y, min.z);
                Edges[9] = new Vector3(min.x, min.y, max.z);
                Edges[10] = new Vector3(max.x, min.y, min.z);
                Edges[11] = new Vector3(max.x, min.y, max.z);
                Edges[12] = new Vector3(min.x, max.y, min.z);
                Edges[13] = new Vector3(min.x, max.y, max.z);
                Edges[14] = new Vector3(max.x, max.y, min.z);
                Edges[15] = new Vector3(max.x, max.y, max.z);
                Edges[16] = new Vector3(min.x, min.y, min.z);
                Edges[17] = new Vector3(min.x, max.y, min.z);
                Edges[18] = new Vector3(max.x, min.y, min.z);
                Edges[19] = new Vector3(max.x, max.y, min.z);
                Edges[20] = new Vector3(min.x, min.y, max.z);
                Edges[21] = new Vector3(min.x, max.y, max.z);
                Edges[22] = new Vector3(max.x, min.y, max.z);
                Edges[23] = new Vector3(max.x, max.y, max.z);
            }
        }
    }
}
