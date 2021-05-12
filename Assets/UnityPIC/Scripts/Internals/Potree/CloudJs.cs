using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

#pragma warning disable 0649

namespace UnityPIC
{
    using UnityEngine.Networking;
    using Utils;

    namespace Internals
    {

        namespace Potree
        {
            public struct CloudJs
            {
                public enum AttributeType
                {
                    POSITION_CARTESIAN = 0,
                    COLOR = 1,
                    INTENSITY = 2,
                    CLASSIFICATION = 3,
                }

                public enum DataType
                {
                    Int32 = 0,
                    UInt8 = 1,
                    UInt16 = 2,
                    Double = 3,
                }

                public enum VersionNumber
                {
                    v1_7 = 0,
                    v1_8 = 1,
                    v2_0 = 2,
                }

                public struct PointAttributeDescriptor
                {
                    public AttributeType AttributeType;
                    public int Size;
                    public int ElementCount;
                    public int ElementSize;
                    public DataType DataType;
                    public int Offset;
                }

                public VersionNumber Version;
                public string OctreeDir;
                public string Projection;
                public ulong Points;
                public Bounds Bounds;
                public Bounds TightBounds;
                public List<PointAttributeDescriptor> PointAttributes;
                public float Spacing;
                public float Scale;
                public int StepSize;

                public static CloudJs Load(string dir)
                {
                    string text = "";

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

                        text = loadingRequest.downloadHandler.text;
                    }
                    else
                    {
                        text = File.ReadAllText(dir);
                    }

                    CloudJs parsed = default;

                    parsed = parse(text);

                    return parsed;
                }

                private static CloudJs parse(string str)
                {
                    CloudJs cloudJs = new CloudJs();

                    Json json = Json.Parse(str);
                    JsonDict dict = json.Get<JsonDict>();
                    JsonDict bounds = dict["boundingBox"].Get<JsonDict>();
                    JsonDict tightBounds = dict["tightBoundingBox"].Get<JsonDict>();
                    JsonList pointAttributes = dict["pointAttributes"].Get<JsonList>();

                    string v = dict["version"].Get<string>();

                    cloudJs.OctreeDir = dict["octreeDir"].Get<string>();
                    cloudJs.Projection = dict["projection"].Get<string>();
                    cloudJs.Points = (ulong)(long)dict["points"].Get<JsonLong>();

                    cloudJs.Bounds.min = new Vector3(
                        (float)bounds["lx"].Get<JsonDouble>(),
                        (float)bounds["ly"].Get<JsonDouble>(),
                        (float)bounds["lz"].Get<JsonDouble>()
                    );

                    cloudJs.Bounds.max = new Vector3(
                        (float)bounds["ux"].Get<JsonDouble>(),
                        (float)bounds["uy"].Get<JsonDouble>(),
                        (float)bounds["uz"].Get<JsonDouble>()
                    );

                    cloudJs.TightBounds.min = new Vector3(
                        (float)tightBounds["lx"].Get<JsonDouble>(),
                        (float)tightBounds["ly"].Get<JsonDouble>(),
                        (float)tightBounds["lz"].Get<JsonDouble>()
                    );

                    cloudJs.TightBounds.max = new Vector3(
                        (float)tightBounds["ux"].Get<JsonDouble>(),
                        (float)tightBounds["uy"].Get<JsonDouble>(),
                        (float)tightBounds["uz"].Get<JsonDouble>()
                    );

                    cloudJs.Spacing = (float)dict["spacing"].Get<JsonDouble>();
                    cloudJs.Scale = (float)dict["scale"].Get<JsonDouble>();
                    cloudJs.StepSize = (int)dict["hierarchyStepSize"].Get<JsonLong>();

                    cloudJs.PointAttributes = new List<PointAttributeDescriptor>(pointAttributes.Count);

                    int offset = 0;

                    if (v == "1.7")
                    {
                        cloudJs.Version = VersionNumber.v1_7;

                        for (int i = 0; i < pointAttributes.Count; ++i)
                        {
                            PointAttributeDescriptor attribute = new PointAttributeDescriptor();

                            if (pointAttributes[i].Get<string>().ToUpper() == "POSITION_CARTESIAN")
                            {
                                attribute.AttributeType = AttributeType.POSITION_CARTESIAN;
                                attribute.Size = 12;
                                attribute.ElementCount = 3;
                                attribute.ElementSize = 4;
                                attribute.DataType = DataType.Int32;
                                attribute.Offset = offset;
                            }
                            else if (pointAttributes[i].Get<string>().ToUpper() == "COLOR_PACKED")
                            {
                                attribute.AttributeType = AttributeType.COLOR;
                                attribute.Size = 4;
                                attribute.ElementCount = 4;
                                attribute.ElementSize = 1;
                                attribute.DataType = DataType.UInt8;
                                attribute.Offset = offset;
                            }
                            else if (pointAttributes[i].Get<string>().ToUpper() == "INTENSITY")
                            {
                                attribute.AttributeType = AttributeType.INTENSITY;
                                attribute.Size = 2;
                                attribute.ElementCount = 1;
                                attribute.ElementSize = 2;
                                attribute.DataType = DataType.UInt16;
                                attribute.Offset = offset;

                            }
                            else if (pointAttributes[i].Get<string>().ToUpper() == "CLASSIFICATION")
                            {
                                attribute.AttributeType = AttributeType.CLASSIFICATION;
                                attribute.Size = 1;
                                attribute.ElementCount = 1;
                                attribute.ElementSize = 1;
                                attribute.DataType = DataType.UInt8;
                                attribute.Offset = offset;
                            }
                            else
                            {
                                throw new Exception("Unsupported attributed type. Supported attributes are: COLOR_PACKED, INTENSITY, CLASSIFICATION");
                            }

                            offset += attribute.Size;
                            cloudJs.PointAttributes.Add(attribute);
                        }
                    }
                    else if (v == "1.8")
                    {
                        cloudJs.Version = VersionNumber.v1_8;

                        for (int i = 0; i < pointAttributes.Count; ++i)
                        {
                            PointAttributeDescriptor attribute = new PointAttributeDescriptor();
                            JsonDict attrDict = pointAttributes[i].Get<JsonDict>();

                            if (attrDict["name"].Get<string>().ToUpper() == "POSITION_CARTESIAN")
                            {
                                attribute.AttributeType = AttributeType.POSITION_CARTESIAN;
                                attribute.Offset = offset;
                            }
                            else if (attrDict["name"].Get<string>().ToUpper() == "RGBA")
                            {
                                attribute.AttributeType = AttributeType.COLOR;
                                attribute.Offset = offset;
                            }
                            else if (attrDict["name"].Get<string>().ToUpper() == "INTENSITY")
                            {
                                attribute.AttributeType = AttributeType.INTENSITY;
                                attribute.Offset = offset;
                            }
                            else if (attrDict["name"].Get<string>().ToUpper() == "CLASSIFICATION")
                            {
                                attribute.AttributeType = AttributeType.CLASSIFICATION;
                                attribute.Offset = offset;
                            }
                            else
                            {
                                throw new Exception("Unsupported attributed type. Supported attributes are: RGBA, INTENSITY, CLASSIFICATION");
                            }

                            attribute.Size = (int)attrDict["size"].Get<JsonLong>();
                            attribute.ElementCount = (int)attrDict["elements"].Get<JsonLong>();
                            attribute.ElementSize = (int)attrDict["elementSize"].Get<JsonLong>();

                            if (attrDict["type"].Get<string>() == "int32")
                            {
                                attribute.DataType = DataType.Int32;
                            }
                            else if (attrDict["type"].Get<string>() == "uint16")
                            {
                                attribute.DataType = DataType.UInt16;
                            }
                            else if (attrDict["type"].Get<string>() == "uint8")
                            {
                                attribute.DataType = DataType.UInt8;
                            }
                            else if (attrDict["type"].Get<string>() == "double")
                            {
                                attribute.DataType = DataType.Double;
                            }

                            cloudJs.PointAttributes.Add(attribute);
                            offset += attribute.Size;
                        }
                    }
                    else if (v == "2.0")
                    {
                        cloudJs.Version = VersionNumber.v2_0;
                        throw new Exception("PotreeConverter version not supported.");
                    }
                    else
                    {
                        throw new Exception("PotreeConverter version not supported.");
                    }

                    return cloudJs;
                }
            }
        }
    }
}
