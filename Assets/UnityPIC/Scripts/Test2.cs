using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using UnityEngine;

using UnityPIC;
using UnityPIC.Core;
using UnityPIC.Graphics.PackingFormat;
using UnityPIC.Internals;
using UnityPIC.Internals.Potree;
using UnityPIC.Core.Load;
using UnityPIC.Core.Traversal;
using UnityPIC.Utils;



using Ref = UnityPIC.Utils.PriorityQueue<int, int>.Ref;

public class Test2 : MonoBehaviour
{

    //private string _directory = @"F:\potree-1.6\pointclouds\nyc";
    //private string _directory = @"F:\datasets\DublinCityLazIntensity";
    //private string _directory = @"F:\datasets\LightHouse1.8";
    //private string _directory = @"F:\datasets\NEONDSSampleLiDARPointCloud";

    private RenderingPipeline _renderingPipeline;
    private System.Random _rand;
    private Vector3 _shift;
    private Dictionary<UPICNode, GameObject[]> _colliderObjects;

    private Queue<GameObject> _inactiveGOs;
    private GameObject _colliders;
    private GameObject _inactives;

    // Start is called before the first frame update
    void Start()
    {
        string _directory;

        if (Application.platform == RuntimePlatform.Android)
        {
            _directory = "jar:file://" + Application.dataPath + "!/assets" + "/NEONDSSampleLiDARPointCloud";
        }
        else
        {
            _directory = Application.dataPath + @"\StreamingAssets\NEONDSSampleLiDARPointCloud";
        }

        _rand = new System.Random();

        TraversalParams p = new TraversalParams()
        {
            PointBudget = 3000000,
        };

        //traversalCam = GameObject.Find("TraversalCamera").GetComponent<Camera>();
        //traversalCam.enabled = false;
        //traversalCam.fieldOfView = 60;

        _renderingPipeline = gameObject.AddComponent<RenderingPipeline>();
        _renderingPipeline.OnNodeRendered += onNodeRendered;
        _renderingPipeline.OnNodeRemoved += onNodeRemoved;
        _renderingPipeline.OnNodeUpdated += onNodeUpdated;

        _renderingPipeline.Initialize(Camera.main, Camera.main, _directory, p);

        _shift = _renderingPipeline.Shift;
        _colliderObjects = new Dictionary<UPICNode, GameObject[]>();
        _inactiveGOs = new Queue<GameObject>();

        _colliders = new GameObject();
        _inactives = new GameObject();
        _colliders.name = "Colliders";
        _inactives.name = "Inactive Colliders";

        _colliders.transform.position = Vector3.zero;

        player = GameObject.Find("Player");

    }

    private void OnDestroy()
    {
        _renderingPipeline.OnNodeRendered -= onNodeRendered;
        _renderingPipeline.OnNodeRemoved -= onNodeRemoved;
        _renderingPipeline.OnNodeUpdated -= onNodeUpdated;
        //Destroy(_renderingPipeline);

        //while (_inactiveGOs.Count > 0)
        //{
        //    Destroy(_inactiveGOs.Dequeue());
        //}

        //Destroy(_colliders);
        //Destroy(_inactives);
    }

    GameObject player;
    Camera traversalCam;

    void LateUpdate()
    {
        //Vector3 playerPos = player.transform.position - Camera.main.transform.forward * 4;
        //traversalCam.transform.position = playerPos;

        //playerPos = player.transform.position;
        //playerPos.y += 2;

        //Camera.main.transform.position = playerPos;
        //traversalCam.transform.rotation = Camera.main.transform.rotation;

    }

    private void onNodeRendered(UPICNode node)
    {
        ransac(node, node.Points);
    }

    private void onNodeRemoved(UPICNode node)
    {
        _colliderObjects.TryGetValue(node, out GameObject[] gos);
        _colliderObjects.Remove(node);

        if (gos != null)
        {
            for (int i = 0; i < 8; ++i)
            {
                if (gos[i] != null)
                {
                    gos[i].SetActive(false);
                    gos[i].name = "";
                    _inactiveGOs.Enqueue(gos[i]);
                    gos[i].transform.SetParent(_inactives.transform);
                }
            }
        }
    }

    private void onNodeUpdated(UPICNode node)
    {
        _colliderObjects.TryGetValue(node, out GameObject[] gos);

        int mask = node.RenderMask;

        if (gos != null)
        {
            for (int i = 0; i < 8; ++i)
            {
                if (gos[i] != null)
                {
                    if ((mask & (1 << i)) > 0)
                    {
                        gos[i].SetActive(true);

                    }
                    else
                    {
                        gos[i].SetActive(false);
                    }
                }

            }
        }
    }

    private List<Vector3> _samples = new List<Vector3>();
    private int trials = 0;
    private int success = 0;

    private void ransac(UPICNode node, PosColIntCls20[] points)
    {
        Vector3 p1, p2, p3, crossProd, v1, v2;
        float deg;
        GameObject go;

        GameObject[] gos = new GameObject[8];

        int mask = node.RenderMask;

        for (int oi = 0; oi < 8; ++oi)
        {
            int partitionCount = node.PartitionPointCount(oi);
            int partitionOffset = node.PartitionOffsets[oi];

            if (partitionCount < 10) continue;

            _samples.Clear();

            int nTrials = 500;
            int nSuccess = 0;

            Vector3 normal = Vector3.zero;

            for (int i = 0; i < nTrials; ++i)
            {
                p1 = points[partitionOffset + _rand.Next() % partitionCount].Position - _shift;
                p2 = points[partitionOffset + _rand.Next() % partitionCount].Position - _shift;
                p3 = points[partitionOffset + _rand.Next() % partitionCount].Position - _shift;

                v1 = p2 - p1;
                v2 = p3 - p1;

                crossProd = Vector3.Cross(v1, v2).normalized;

                deg = Mathf.Acos(Vector3.Dot(crossProd, Vector3.up)) * 180 / Mathf.PI;
                deg = (deg > 90) ? (180 - deg) : deg;

                if (deg < 35)
                {
                    normal += crossProd;
                    ++nSuccess;

                    _samples.Add(p1);
                    _samples.Add(p2);
                    _samples.Add(p3);
                }
            }

            normal = (normal / nSuccess).normalized;
            deg = Mathf.Acos(Vector3.Dot(normal, Vector3.up)) * 180 / Mathf.PI;
            deg = (deg > 90) ? (180 - deg) : deg;

            if (nSuccess > .3f * nTrials && deg < 35)
            {
                ++success;

                _samples.Sort(delegate (Vector3 vec1, Vector3 vec2)
                {
                    return vec1.y.CompareTo(vec2.y);
                });

                Vector3 median = _samples[Mathf.FloorToInt(_samples.Count / 2)];

                float xMin = float.MaxValue;
                float xMax = float.MinValue;
                float yMin = float.MaxValue;
                float yMax = float.MinValue;
                float zMin = float.MaxValue;
                float zMax = float.MinValue;
                float yExtent = 0;

                for (int i = 0, c = _samples.Count; i < c; ++i)
                {
                    //error += Mathf.Abs(_samples[i].y - median.y);
                    xMin = Mathf.Min(xMin, _samples[i].x);
                    yMin = Mathf.Min(yMin, _samples[i].y);
                    zMin = Mathf.Min(zMin, _samples[i].z);
                    xMax = Mathf.Max(xMax, _samples[i].x);
                    yMax = Mathf.Max(yMax, _samples[i].y);
                    zMax = Mathf.Max(zMax, _samples[i].z);
                    yExtent += Mathf.Abs(_samples[i].y - median.y);
                }

                yExtent /= _samples.Count;

                //error /= _samples.Count;

                //Debug.LogFormat("{0}/{1}: error - {2}", success, trials, error);


                if (_inactiveGOs.Count > 0)
                {
                    go = _inactiveGOs.Dequeue();
                    go.SetActive(true);
                }
                else
                {
                    go = new GameObject();
                }


                if (_colliderObjects.ContainsKey(node))
                {
                    throw new Exception();
                }

                BoxCollider collider = go.GetComponent<BoxCollider>();

                if (collider == null)
                {
                    collider = go.AddComponent<BoxCollider>();
                }

                go.transform.position = new Vector3((xMax + xMin) * 0.5f, median.y, (zMax + zMin) * 0.5f);
                collider.size = new Vector3(xMax - xMin, yExtent * 2, zMax - zMin) * 1.2f;

                go.SetActive(((mask & (1 << oi)) > 0));
                go.name = node.InternalNode.FullName + '-' + oi;
                go.transform.SetParent(_colliders.transform);

                if (normal.y < 0)
                {
                    normal *= -1;
                }

                go.transform.up = normal;

                gos[oi] = go;
            }
        }

        _colliderObjects[node] = gos;
    }
}
