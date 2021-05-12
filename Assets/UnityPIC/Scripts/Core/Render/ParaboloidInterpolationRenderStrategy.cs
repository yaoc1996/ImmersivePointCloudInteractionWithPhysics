using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPIC
{
    using Base;
    using UnityEngine.Rendering;
    using UnityPIC.Internals;

    namespace Core
    {
        namespace Render
        {
            public class ParaboloidInterpolationRenderStrategy : AbstractRenderStrategy
            {
                private CommandBuffer _commandsSC;
                private RenderTexture _sceneRTSC;
                private RenderTexture _depthRTSC;

                private CommandBuffer _commands;
                private RenderTexture _sceneRT;
                private RenderTexture _depthRT;
                private Material _mat;
                private Material _lineMat;

                private HashSet<UPICNode> _activeNodes;

                private bool _initialized;

                private bool _updated;
                private Vector3 _prevCameraPosition;
                private Quaternion _prevCameraRotation;

                private static Matrix4x4 identity = Matrix4x4.identity;

                public ParaboloidInterpolationRenderStrategy()
                {
                    _activeNodes = new HashSet<UPICNode>();
                }

                public override void OnNodeRemove(UPICNode node)
                {
                    _activeNodes.Remove(node);
                    _updated = true;
                    //Debug.LogFormat("{0} Removed", node.InternalNode.FullName);
                }

                public override void OnNodeRender(UPICNode node)
                {
                    _activeNodes.Add(node);
                    _updated = true;
                    //Debug.LogFormat("{0} Rendered", node.InternalNode.FullName);
                }

                public override void OnNodeUpdate(UPICNode node)
                {
                    _updated = true;
                    //Debug.LogFormat("{0} Updated", node.InternalNode.FullName);
                }

                public override void Initialize(Camera camera, Vector4 s)
                {
                    //try
                    //{
                    //    GameObject.Find("Cube").GetComponent<MeshRenderer>().material.color = Color.blue;
                    //}
                    //catch (Exception e)
                    //{
                    //    GameObject.Find("Cube").GetComponent<MeshRenderer>().material.color = Color.red;
                    //}

                    base.Initialize(camera, s);

                    _commands = new CommandBuffer();
                    _commandsSC = new CommandBuffer();

                    _sceneRT = new RenderTexture(camera.pixelWidth, camera.pixelHeight, 0, RenderTextureFormat.ARGB32, 0);
                    _depthRT = new RenderTexture(camera.pixelWidth, camera.pixelHeight, 32, RenderTextureFormat.Depth, 0);

                    Shader.SetGlobalTexture("_UPIC_SceneColor", _sceneRT);
                    Shader.SetGlobalTexture("_UPIC_SceneDepth", _depthRT);

                    _mat = new Material(Shader.Find("UnityPIC/ParaboloidInterpolation"));
                    _mat.SetVector("_UPIC_Shift", s);

                    _lineMat = new Material(Shader.Find("UnityPIC/Line"));
                    _lineMat.SetVector("_UPIC_Shift", s);
                    _lineMat.SetColor("_UPIC_Color", Color.white);

                    _initialized = true;

                    camera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, _commands);
                }

                public override void Dispose()
                {
                    if (_initialized)
                    {
                        Destroy(_sceneRT);
                        Destroy(_depthRT);
                        Destroy(_mat);
                        Destroy(_lineMat);

                        if (targetCamera != null)
                        {
                            targetCamera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, _commands);
                        }

                        _commands.Dispose();

                        _initialized = false;

                        if (_commandsSC != null)
                        {
                            _commandsSC.Dispose();
                        }
                    }
                }

                private void Start()
                {
                }

                //bool lol = false;
                private void Update()
                {
                    //_commands.Clear();

                    _updated = _updated || _prevCameraPosition != targetCamera.transform.position || _prevCameraRotation != targetCamera.transform.rotation;

                    _prevCameraPosition = targetCamera.transform.position;
                    _prevCameraRotation = targetCamera.transform.rotation;

                    _commands.Clear();

                    //if (_updated)
                    {
                        if (targetCamera.pixelWidth != _sceneRT.width || targetCamera.pixelHeight != _sceneRT.height)
                        {
                            Destroy(_sceneRT);
                            Destroy(_depthRT);

                            _sceneRT = new RenderTexture(targetCamera.pixelWidth, targetCamera.pixelHeight, 0, RenderTextureFormat.ARGB32, 0);
                            _depthRT = new RenderTexture(targetCamera.pixelWidth, targetCamera.pixelHeight, 32, RenderTextureFormat.Depth, 0);

                            Shader.SetGlobalTexture("_UPIC_SceneColor", _sceneRT);
                            Shader.SetGlobalTexture("_UPIC_SceneDepth", _depthRT);
                        }

                        _commands.SetRenderTarget(_sceneRT, _depthRT);
                        _commands.ClearRenderTarget(true, true, targetCamera.backgroundColor);

                        //if (lol)
                        //{
                        //    _commands.ClearRenderTarget(true, true, targetCamera.backgroundColor);
                        //}
                        //else
                        //{

                        //    _commands.ClearRenderTarget(true, true, Color.red);
                        //}
                        //lol = !lol;
                        allocateDrawCalls(_commands);

                        _updated = false;
                    }
                }

                private void OnRenderObject()
                {
                    if (Camera.current.name == "SceneCamera")
                    {
                        _commandsSC.Clear();

                        allocateDrawCalls(_commandsSC);
                        UnityEngine.Graphics.ExecuteCommandBuffer(_commandsSC);
                    }
                }

                private void OnDestroy()
                {
                    Dispose();
                }

                private void allocateDrawCalls(CommandBuffer commands)
                {

                    UPICNode node;

                    float minIntensity = 0;
                    float maxIntensity = 0;
                    int pointCount = 0;

                    using (var enumerator = _activeNodes.GetEnumerator())
                    {
                        while (enumerator.MoveNext())
                        {
                            node = enumerator.Current;
                            //if (node.InternalNode.FullName != "r6024301")
                            //    continue;

                            minIntensity += node.IntensityRange.Item1 * node.PointCount / 65535;
                            maxIntensity += node.IntensityRange.Item2 * node.PointCount / 65535;
                            pointCount += node.PointCount;

                            if (node.IsVisible)
                            {
                                node.AppendDrawPointsCommand(identity, _mat, 0, commands);
                                //node.AppendDrawBoundsCommand(identity, _lineMat, 0, _commands);
                            }

                            //node.AppendDrawBoundsCommand(identity, _lineMat, 0, commands);
                        }
                    }

                    if (pointCount > 0)
                    {
                        _mat.SetFloat("_UPIC_MinIntensity", minIntensity / pointCount);
                        _mat.SetFloat("_UPIC_MaxIntensity", maxIntensity / pointCount);
                    }
                }
            }
        }
    }
}
