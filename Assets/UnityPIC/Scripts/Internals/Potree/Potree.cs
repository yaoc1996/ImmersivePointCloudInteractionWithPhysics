using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityPIC
{
    namespace Internals
    {
        namespace Potree
        {
            #region HRCByte
            [StructLayout(LayoutKind.Explicit, Size = 5, Pack = 1)]
            struct HRCByte
            {
                public HRCByte(int c = 0, byte m = 0)
                {
                    Count = c;
                    Mask = m;
                }

                [FieldOffset(0)] public byte Mask;
                [FieldOffset(1)] public int Count;
            }
            #endregion

            public class BaseNode<NodeType> where NodeType : BaseNode<NodeType>, new()
            {
                public NodeType Parent;
                public NodeType[] Children;

                public Potree<NodeType>.Subtree Subtree;

                public string LocalName;
                public Vector3 Center;
                public Vector3 Extents;
                public float Spacing;
                public byte Depth;
                public byte OctantId;

                public int PointCount;
                public byte DescendantMask;

                public bool IsLeaf { get { return Children == null; } }
                public string FullName { get { return Subtree.FileNamePrefix + LocalName; } }
            }


            public class Potree<NodeType> where NodeType : BaseNode<NodeType>, new()
            {
                public class Subtree
                {
                    public delegate void LoadCallback(Subtree subtree);

                    public Potree<NodeType> Potree;
                    public NodeType Root;
                    public string SubDirectory;
                    public string FileNamePrefix;
                    public bool IsLoaded;

                    public Subtree(Potree<NodeType> potree, NodeType root, string subDirectory, string fileNamePrefix)
                    {
                        Potree = potree;
                        Root = root;

                        SubDirectory = subDirectory;
                        FileNamePrefix = fileNamePrefix;

                        IsLoaded = false;
                    }

                    public Subtree() { }

                    public unsafe void Load()
                    {
                        string directory = Potree.DataDirectory + SubDirectory + FileNamePrefix + Root.LocalName + Constants.HrcExt;

                        if (Application.platform == RuntimePlatform.Android)
                        {
                            var loadingRequest = UnityWebRequest.Get(directory);
                            loadingRequest.SendWebRequest();

                            while (!loadingRequest.isDone)
                            {
                                if (loadingRequest.isNetworkError || loadingRequest.isHttpError)
                                {
                                    break;
                                }
                            }

                            parseRawData(loadingRequest.downloadHandler.data);
                        }
                        else
                        {
                            parseRawData(File.ReadAllBytes(directory));
                        }
                    }

                    public async void LoadAsync(LoadCallback callback)
                    {
                        string directory = Potree.DataDirectory + SubDirectory + FileNamePrefix + Root.LocalName + Constants.HrcExt;

                        byte[] rawData;
                        using (var stream = new FileStream(directory, FileMode.Open, FileAccess.Read))
                        {
                            rawData = new byte[stream.Length];
                            await stream.ReadAsync(rawData, 0, (int)stream.Length);
                        }

                        parseRawData(rawData);
                        callback(this);
                    }

                    private unsafe void parseRawData(byte[] rawData)
                    {
#if UNITYPIC_DEBUG
                        if (IsLoaded)
                            throw new Exception("Subtree already loaded.");
#endif

                        string directoryPrefix = Potree.DataDirectory + SubDirectory + FileNamePrefix;

                        fixed (byte* ptr = rawData)
                        {
                            HRCByte* hrcData = (HRCByte*)ptr;
                            int nodeCount = rawData.Length / sizeof(HRCByte);

                            Queue<NodeType> q = new Queue<NodeType>(nodeCount);
                            q.Enqueue(Root);

                            int i,
                                qCount,
                                hrcDataIndex = 0,
                                depth = 0;

                            Vector3 center, min;
                            Subtree subtree;
                            string localName;

                            Vector3 extents = Root.Extents;
                            float spacing = Root.Spacing;

                            NodeType current, child;


                            while (depth++ < 5)
                            {
                                spacing *= 0.5f;
                                extents.x *= 0.5f;
                                extents.y *= 0.5f;
                                extents.z *= 0.5f;

                                qCount = q.Count;

                                while (qCount-- > 0)
                                {
                                    current = q.Dequeue();

                                    current.DescendantMask = hrcData[hrcDataIndex].Mask;
                                    current.PointCount = hrcData[hrcDataIndex].Count;

                                    if (current.PointCount == 0)
                                    {
                                        current.PointCount = (int)(new FileInfo(directoryPrefix + current.LocalName + Constants.BinExt)).Length / Potree.PointStride;
                                        hrcData[hrcDataIndex].Count = current.PointCount;
                                    }

                                    ++hrcDataIndex;

                                    min.x = current.Center.x - current.Extents.x;
                                    min.y = current.Center.y - current.Extents.y;
                                    min.z = current.Center.z - current.Extents.z;

                                    current.Children = new NodeType[8];

                                    for (i = 0; i < 8; ++i)
                                    {
                                        current.Children[i] = new NodeType();
                                        child = current.Children[i];

                                        center = new Vector3()
                                        {
                                            x = (((i >> (2 - Constants.XAxisIndex)) & 1) + 0.5f) * 2 * extents.x + min.x,
                                            y = (((i >> (2 - Constants.YAxisIndex)) & 1) + 0.5f) * 2 * extents.y + min.y,
                                            z = (((i >> (2 - Constants.ZAxisIndex)) & 1) + 0.5f) * 2 * extents.z + min.z,
                                        };

                                        localName = current.LocalName + (char)('0' + i);

                                        if ((current.DescendantMask & (1 << i)) == 0)
                                        {
                                            // ghost node;
                                            subtree = this;
                                            child.PointCount = -1;
                                        }
                                        else if (current.LocalName.Length == Potree.Info.StepSize - 1)
                                        {
                                            // nodes on depth = root.depth + 5;
                                            subtree = new Subtree();
                                            subtree.Potree = Potree;
                                            subtree.Root = child;
                                            subtree.SubDirectory = SubDirectory + localName + '/';
                                            subtree.FileNamePrefix = FileNamePrefix + localName;
                                            localName = "";
                                        }
                                        else
                                        {
                                            // nodes on depth < root.depth + 5;
                                            subtree = this;
                                            q.Enqueue(child);
                                        }

                                        child.Parent = current;
                                        child.Subtree = subtree;
                                        child.LocalName = localName;
                                        child.Center = center;
                                        child.Extents = extents;
                                        child.Spacing = current.Spacing * 0.5f;
                                        child.Depth = (byte)(current.Depth + 1);
                                        child.OctantId = (byte)i;
                                    }
                                }
                            }

                            if (Root.PointCount == 0 && Root.IsLeaf)
                            {
                                //Debug.Log("Found");
                            }
                        }

                    }
                }

                public NodeType Root;

                public string Directory;
                public string DataDirectory;
                public CloudJs Info;
                public int PointStride;

                public int PositionDescriptorIndex;
                public int ColorDescriptorIndex;
                public int IntensityDescriptorIndex;
                public int ClassificationDescriptorIndex;

                public Potree(string dir)
                {
                    Directory = dir;
                    Info = CloudJs.Load(dir + @"\cloud.js");

                    DataDirectory = Directory + '/' + Info.OctreeDir + '/';

                    ref CloudJs info = ref Info;
                    info.Bounds = new Bounds(
                        new Vector3(Info.Bounds.center[Constants.XAxisIndex], Info.Bounds.center[Constants.YAxisIndex], Info.Bounds.center[Constants.ZAxisIndex]),
                        new Vector3(Info.Bounds.size[Constants.XAxisIndex], Info.Bounds.size[Constants.YAxisIndex], Info.Bounds.size[Constants.ZAxisIndex])
                    );

                    PointStride = 0;
                    ColorDescriptorIndex = -1;
                    IntensityDescriptorIndex = -1;
                    ClassificationDescriptorIndex = -1;

                    for (int i = 0; i < Info.PointAttributes.Count; ++i)
                    {
                        switch (Info.PointAttributes[i].AttributeType)
                        {
                            case CloudJs.AttributeType.POSITION_CARTESIAN:
                                PositionDescriptorIndex = i;
                                break;
                            case CloudJs.AttributeType.COLOR:
                                ColorDescriptorIndex = i;
                                break;
                            case CloudJs.AttributeType.INTENSITY:
                                IntensityDescriptorIndex = i;
                                break;
                            case CloudJs.AttributeType.CLASSIFICATION:
                                ClassificationDescriptorIndex = i;
                                break;
                            default:
                                break;
                        }

                        PointStride += Info.PointAttributes[i].Size;
                    }

                    Root = new NodeType();

                    Root.Subtree = new Subtree(this, Root, "r/", "r");
                    Root.LocalName = "";
                    Root.Center = info.Bounds.center;
                    Root.Extents = info.Bounds.extents;
                    Root.Spacing = info.Spacing;
                    Root.Depth = 0;
                    Root.OctantId = 0;
                    Root.PointCount = 0;

                    Root.Subtree.Load();
                    Root.Subtree.IsLoaded = true;
                }
            }
        }
    }
}
