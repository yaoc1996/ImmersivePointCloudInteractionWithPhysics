using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityPIC
{
    using Graphics.PackingFormat;
    using Internals;
    using Unity.Collections;
    using UnityEngine.Profiling;
    using UnityEngine.Rendering;
    using Utils;

    namespace Core
    {
        public delegate void UPICNodeActionDelegate(UPICNode node);

        public class UPICNode
        {
            public readonly UPICInternalNode InternalNode;
            public readonly UPICNode Parent;

            public readonly Bounds Bounds;
            public readonly float Spacing;
            public readonly int PointCount;
            public readonly short Depth;

            public byte CullingMask;
            public ByteMask RibidBodyMask;
            public ByteMask InheritanceMask;

            public PosColIntCls20[] Points;
            public int[] PartitionOffsets;

            public (float, float) IntensityRange;
            public Vector3[] Edges;

            public int PartitionPointCount(int i)
            {
                return PartitionOffsets[i + 1] - PartitionOffsets[i];
            }

            public bool IsVisible
            {
                get
                {
                    return RenderMask > 0;
                }
            }


            public int RenderMask
            {
                get
                {
                    return InheritanceMask & CullingMask;
                }
            }

            private MaterialPropertyBlock _matProps;

            private ComputeBuffer _pointsBuffer;
            //private ComputeBuffer _argsBuffer;
            private ComputeBuffer _edgesBuffer;

            private bool _initialized;

            public UPICNode(UPICInternalNode node)
            {
                InternalNode = node;
                Bounds = new Bounds(node.Center, 2 * node.Extents);
                Spacing = node.Spacing;
                PointCount = node.Points.Length;
                Depth = node.Depth;

                CullingMask = node.CullingMaskSnapshot;
                InheritanceMask = new ByteMask(node.OccupancyMask);


                Points = node.Points;
                PartitionOffsets = node.PartitionOffsets;

                IntensityRange = node.IntensityRange;
                Edges = node.Edges;

                Parent = node.Parent?.UPICNode;

                if (!node.IsLeaf)
                {
                    for (int i = 0; i < 8; ++i)
                    {
                        if (node.Children[i].UPICNode != null)
                        {
                            InheritanceMask -= node.Children[i].OctantId;
                        }
                    }
                }

                _initialized = false;
            }

            public void Initialize()
            {
                _matProps = new MaterialPropertyBlock();

                _pointsBuffer = new ComputeBuffer(PointCount, Marshal.SizeOf<PosColIntCls20>());
                _pointsBuffer.SetData(Points);

                _matProps.SetBuffer("_UPIC_Points", _pointsBuffer);
                _matProps.SetVector("_UPIC_Center", Bounds.center);
                _matProps.SetFloat("_UPIC_Radius", Spacing);

                UploadRenderMask();

                _initialized = true;
            }

            public void Dispose()
            {
                if (_initialized)
                {
                    _pointsBuffer.Dispose();

                    if (_edgesBuffer != null)
                    {
                        _edgesBuffer.Dispose();
                    }
                }

            }

            public void UploadRenderMask()
            {
                _matProps?.SetInt("_UPIC_RenderMask", RenderMask);
            }

            public void AppendDrawPointsCommand(Matrix4x4 transform, Material mat, int pass, CommandBuffer commandBuffer)
            {
                commandBuffer.DrawProcedural(transform, mat, pass, MeshTopology.Points, PointCount, 1, _matProps);
            }

            public void AppendDrawBoundsCommand(Matrix4x4 transform, Material mat, int pass, CommandBuffer commandBuffer)
            {
                if (_edgesBuffer == null)
                {
                    initDrawBounds();
                }

                commandBuffer.DrawProcedural(transform, mat, pass, MeshTopology.Lines, 24, 1, _matProps);
            }

            private void initDrawBounds()
            {
                _edgesBuffer = new ComputeBuffer(24, 12);
                _edgesBuffer.SetData(Edges);
                _matProps.SetBuffer("_UPIC_Edges", _edgesBuffer);
            }
        }
    }

}
