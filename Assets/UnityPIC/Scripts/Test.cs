using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using UnityPIC.Graphics.PackingFormat;
using UnityPIC.Internals;

[ExecuteAlways]
public class Test : MonoBehaviour
{
    private CommandBuffer _command;
    private ComputeBuffer _compute;
    private Material _mat;
    private MaterialPropertyBlock _matProp;

    Camera mainCamera;
    FrustumCuller _frustumCuller;

    private void Update()
    {
        //Debug.Log(SceneView.GetAllSceneCameras()[0].pixelHeight);
        //Debug.Log(SceneView.GetAllSceneCameras()[0].pixelWidth);

        //Graphics.DrawProcedural(_mat, new Bounds(), MeshTopology.Lines, 2, 1, Camera.current, _matProp);

        //if (mainCamera != null)
        //{
        //    _frustumCuller.CameraParams = (mainCamera.projectionMatrix * mainCamera.worldToCameraMatrix, mainCamera.nearClipPlane);
        //    Debug.Log(_frustumCuller.Cull(transform.position, Vector3.one * factor));
        //}
    }

    private void OnEnable()
    {
        init();
    }

    private void OnRenderObject()
    {
        if (Camera.current.name == "SceneCamera")
        {
            _command.Clear();
            _command.DrawProcedural(transform.localToWorldMatrix, _mat, -1, MeshTopology.Lines, _compute.count, 1, _matProp);
            Graphics.ExecuteCommandBuffer(_command);
        }

        if (Camera.current.name == "Main Camera")
        {
            _command.Clear();
            _command.DrawProcedural(transform.localToWorldMatrix, _mat, -1, MeshTopology.Lines, _compute.count, 1, _matProp);

            if (mainCamera == null)
            {
                mainCamera = Camera.current;
                Camera.current.AddCommandBuffer(CameraEvent.AfterForwardAlpha, _command);
            }

        }
    }

    public static int factor = 1;

    private void init()
    {
        _frustumCuller = new FrustumCuller();

        if (_command == null)
        {
            _command = new CommandBuffer();
        }

        if (_compute == null)
        {
            _compute = new ComputeBuffer(24, 16);

            PosCol16[] points = new PosCol16[24];

            points[0] = new PosCol16() { Position = new Vector3(-factor, -factor, -factor), Color = new Color32(255, 255, 255, 255), };
            points[1] = new PosCol16() { Position = new Vector3(-factor, factor, -factor), Color = new Color32(255, 255, 255, 255), };
            points[2] = new PosCol16() { Position = new Vector3(factor, factor, -factor), Color = new Color32(255, 255, 255, 255), };
            points[3] = new PosCol16() { Position = new Vector3(factor, -factor, -factor), Color = new Color32(255, 255, 255, 255), };

            points[4] = new PosCol16() { Position = new Vector3(-factor, factor, -factor), Color = new Color32(255, 255, 255, 255), };
            points[5] = new PosCol16() { Position = new Vector3(factor, factor, -factor), Color = new Color32(255, 255, 255, 255), };
            points[6] = new PosCol16() { Position = new Vector3(factor, -factor, -factor), Color = new Color32(255, 255, 255, 255), };
            points[7] = new PosCol16() { Position = new Vector3(-factor, -factor, -factor), Color = new Color32(255, 255, 255, 255), };

            points[8] = new PosCol16() { Position = new Vector3(-factor, -factor, factor), Color = new Color32(255, 255, 255, 255), };
            points[9] = new PosCol16() { Position = new Vector3(-factor, factor, factor), Color = new Color32(255, 255, 255, 255), };
            points[10] = new PosCol16() { Position = new Vector3(factor, factor, factor), Color = new Color32(255, 255, 255, 255), };
            points[11] = new PosCol16() { Position = new Vector3(factor, -factor, factor), Color = new Color32(255, 255, 255, 255), };

            points[12] = new PosCol16() { Position = new Vector3(-factor, factor, factor), Color = new Color32(255, 255, 255, 255), };
            points[13] = new PosCol16() { Position = new Vector3(factor, factor, factor), Color = new Color32(255, 255, 255, 255), };
            points[14] = new PosCol16() { Position = new Vector3(factor, -factor, factor), Color = new Color32(255, 255, 255, 255), };
            points[15] = new PosCol16() { Position = new Vector3(-factor, -factor, factor), Color = new Color32(255, 255, 255, 255), };

            points[16] = new PosCol16() { Position = new Vector3(-factor, -factor, -factor), Color = new Color32(255, 255, 255, 255), };
            points[17] = new PosCol16() { Position = new Vector3(-factor, -factor, factor), Color = new Color32(255, 255, 255, 255), };
            points[18] = new PosCol16() { Position = new Vector3(-factor, factor, -factor), Color = new Color32(255, 255, 255, 255), };
            points[19] = new PosCol16() { Position = new Vector3(-factor, factor, factor), Color = new Color32(255, 255, 255, 255), };

            points[20] = new PosCol16() { Position = new Vector3(factor, -factor, -factor), Color = new Color32(255, 255, 255, 255), };
            points[21] = new PosCol16() { Position = new Vector3(factor, -factor, factor), Color = new Color32(255, 255, 255, 255), };
            points[22] = new PosCol16() { Position = new Vector3(factor, factor, -factor), Color = new Color32(255, 255, 255, 255), };
            points[23] = new PosCol16() { Position = new Vector3(factor, factor, factor), Color = new Color32(255, 255, 255, 255), };

            _compute.SetData(points);

            if (_matProp == null)
            {
                _matProp = new MaterialPropertyBlock();
            }

            _matProp.SetBuffer("_Vertices", _compute);
        }

        if (_mat == null)
        {
            _mat = new Material(Shader.Find("UnityPIC/CB_Unlit"));
        }

        if (_matProp == null)
        {
            _matProp = new MaterialPropertyBlock();
            _matProp.SetBuffer("_Vertices", _compute);
        }
    }

    private void deinit(Scene current=new Scene())
    {
        if (mainCamera != null && _command != null)
        {
            mainCamera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, _command);
            mainCamera = null;
        }

        _command?.Dispose();
        _compute?.Dispose();

        if (_mat != null)
        {
            DestroyImmediate(_mat);
            _mat = null;
        }

        _command = null;
        _compute = null;
    }

    private void OnDisable()
    {
        deinit();
    }

    private void Reset()
    {
        deinit();
    }
}
