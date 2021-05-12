using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPIC
{
    namespace Internals
    {
        public unsafe class FrustumCuller
        {
            private static int[,] s_neighbors = new int[8, 3]
            {
                { 4, 2, 1 },
                { 5, 3, 0 },
                { 6, 0, 3 },
                { 7, 1, 2 },
                { 0, 6, 5 },
                { 1, 7, 4 },
                { 2, 4, 7 },
                { 3, 5, 6 },
            };

            private static float[,] s_viewPort = new float[4, 2]
            {
                { -1, -1 },
                { -1, 1 },
                { 1, 1 },
                { 1, -1 },
            };

            private float _vp00, _vp01, _vp02, _vp03, _vp10, _vp11, _vp12, _vp13, _vp30, _vp31, _vp32, _vp33;
            private float _ncpDist;

            private float[,] _minmax;
            private float[,] _points;
            private double[,] _ndc;
            private double[,] _convexHull;
            private double[,] _clipped;
            private float[] _zDists;
            private (double, int)[] _visitOrder;
            private double[,] _rotationBuffer1;
            private double[,] _rotationBuffer2;

            private int _nProjected;
            private int _nConvexHull;
            private int _nClipped;

            public (Matrix4x4, float) CameraParams
            {
                set
                {
                    _ncpDist = value.Item2;

                    Vector4 r0 = value.Item1.GetRow(0);
                    Vector4 r1 = value.Item1.GetRow(1);
                    Vector4 r3 = value.Item1.GetRow(3);

                    _vp00 = r0[0];
                    _vp01 = r0[1];
                    _vp02 = r0[2];
                    _vp03 = r0[3];
                    _vp10 = r1[0];
                    _vp11 = r1[1];
                    _vp12 = r1[2];
                    _vp13 = r1[3];
                    _vp30 = r3[0];
                    _vp31 = r3[1];
                    _vp32 = r3[2];
                    _vp33 = r3[3];
                }
            }

            public FrustumCuller()
            {
                _minmax = new float[2, 3];
                _points = new float[8, 3];
                _ndc = new double[24, 2];
                _convexHull = new double[24, 2];
                _clipped = new double[24, 2];
                _zDists = new float[8];
                _visitOrder = new (double, int)[24];
                _rotationBuffer1 = new double[24, 2];
                _rotationBuffer2 = new double[24, 2];
            }

            public bool Cull(Vector3 center, Vector3 extents)
            {
                _nProjected = 0;
                _nConvexHull = 0;
                _nClipped = 0;

                _minmax[0, 0] = center.x - extents.x;
                _minmax[0, 1] = center.y - extents.y;
                _minmax[0, 2] = center.z - extents.z;
                _minmax[1, 0] = center.x + extents.x;
                _minmax[1, 1] = center.y + extents.y;
                _minmax[1, 2] = center.z + extents.z;

                for (int i = 0; i < 8; ++i)
                {
                    _points[i, 0] = _minmax[(i >> 2) & 1, 0];
                    _points[i, 1] = _minmax[(i >> 1) & 1, 1];
                    _points[i, 2] = _minmax[(i >> 0) & 1, 2];
                }

                projectVertices();

                if (_nProjected < 3) return false;

                computeConvexHull();

                if (_nConvexHull < 3) return false;

                clipConvexHull();

                if (_nClipped < 3) return false;

                return true;
            }

            private void projectVertices()
            {
                int i, j, iNbr;
                float x, y, z, f;
                bool hasVisible = false;

                for (i = 0; i < 8; ++i)
                {
                    _zDists[i] = _vp30 * _points[i, 0] + _vp31 * _points[i, 1] + _vp32 * _points[i, 2] + _vp33;
                    hasVisible = hasVisible || _zDists[i] > _ncpDist;
                }

                if (!hasVisible) return;

                for (i = 0; i < 8; ++i)
                {
                    if (_zDists[i] >= 0)
                    {
                        x = _points[i, 0];
                        y = _points[i, 1];
                        z = _points[i, 2];

                        _ndc[_nProjected, 0] = (_vp00 * x + _vp01 * y + _vp02 * z + _vp03) / _zDists[i];
                        _ndc[_nProjected, 1] = (_vp10 * x + _vp11 * y + _vp12 * z + _vp13) / _zDists[i];

                        ++_nProjected;
                    }
                    else
                    {
                        for (j = 0; j < 3; ++j)
                        {
                            iNbr = s_neighbors[i, j];
                            if (_zDists[iNbr] > 0)
                            {
                                f = _zDists[i] / (_zDists[i] - _zDists[iNbr]);

                                x = (_points[iNbr, 0] - _points[i, 0]) * f + _points[i, 0];
                                y = (_points[iNbr, 1] - _points[i, 1]) * f + _points[i, 1];
                                z = (_points[iNbr, 2] - _points[i, 2]) * f + _points[i, 2];

                                _ndc[_nProjected, 0] = (_vp00 * x + _vp01 * y + _vp02 * z + _vp03) / _ncpDist;
                                _ndc[_nProjected, 1] = (_vp10 * x + _vp11 * y + _vp12 * z + _vp13) / _ncpDist;

                                ++_nProjected;
                            }
                        }
                    }
                }
            }

            private void computeConvexHull()
            {
                int i, j;
                int iMinY = 0;
                double x, y, rx, ry, px, py, qx, qy;

                for (i = 1; i < _nProjected; ++i)
                {
                    if (_ndc[iMinY, 1] > _ndc[i, 1])
                    {
                        iMinY = i;
                    }
                }

                for (i = 0, j = 0; i < _nProjected; ++i)
                {
                    if (i == iMinY) continue;

                    x = _ndc[i, 0] - _ndc[iMinY, 0];
                    y = _ndc[i, 1] - _ndc[iMinY, 1];

                    _visitOrder[j] = (x / System.Math.Sqrt(x * x + y * y), i);

                    ++j;
                }

                System.Array.Sort(_visitOrder, 0, _nProjected - 1);

                _convexHull[0, 0] = _ndc[iMinY, 0];
                _convexHull[0, 1] = _ndc[iMinY, 1];
                _convexHull[1, 0] = _ndc[_visitOrder[0].Item2, 0];
                _convexHull[1, 1] = _ndc[_visitOrder[0].Item2, 1];
                _nConvexHull = 2;

                for (i = 1; i < _nProjected - 1; ++i)
                {
                    j = _visitOrder[i].Item2;
                    rx = _ndc[j, 0];
                    ry = _ndc[j, 1];

                    while (_nConvexHull >= 2)
                    {
                        px = _convexHull[_nConvexHull - 2, 0];
                        py = _convexHull[_nConvexHull - 2, 1];
                        qx = _convexHull[_nConvexHull - 1, 0];
                        qy = _convexHull[_nConvexHull - 1, 1];

                        if ((qy - py) * (rx - qx) - (qx - px) * (ry - qy) <= 0)
                        {
                            --_nConvexHull;
                        }
                        else break;
                    }

                    _convexHull[_nConvexHull, 0] = rx;
                    _convexHull[_nConvexHull, 1] = ry;
                    ++_nConvexHull;
                }

                rx = _ndc[iMinY, 0];
                ry = _ndc[iMinY, 1];

                while (_nConvexHull >= 2)
                {
                    px = _convexHull[_nConvexHull - 2, 0];
                    py = _convexHull[_nConvexHull - 2, 1];
                    qx = _convexHull[_nConvexHull - 1, 0];
                    qy = _convexHull[_nConvexHull - 1, 1];

                    if ((qy - py) * (rx - qx) - (qx - px) * (ry - qy) <= 0)
                    {
                        --_nConvexHull;
                    }
                    else break;
                }
            }

            private void clipConvexHull()
            {
                double[,] bufferPrev, bufferNext, temp;
                double txPrev, tyPrev, txNext, tyNext, cxPrev, cyPrev, cxNext, cyNext;
                double a1, a2, b1, b2, c1, c2, d, orientationPrev, orientationNext;
                int i, j, nBufferPrev, nBufferNext;

                bufferPrev = _rotationBuffer1;
                bufferNext = _rotationBuffer2;

                nBufferPrev = _nConvexHull;

                for (i = 0; i < _nConvexHull; ++i)
                {
                    bufferPrev[i, 0] = _convexHull[i, 0];
                    bufferPrev[i, 1] = _convexHull[i, 1];
                }

                cxPrev = s_viewPort[3, 0];
                cyPrev = s_viewPort[3, 1];

                for (i = 0; i < 4; ++i)
                {
                    cxNext = s_viewPort[i, 0];
                    cyNext = s_viewPort[i, 1];
                    a1 = cyNext - cyPrev;
                    b1 = cxPrev - cxNext;
                    c1 = a1 * cxPrev + b1 * cyPrev;

                    txPrev = bufferPrev[nBufferPrev - 1, 0];
                    tyPrev = bufferPrev[nBufferPrev - 1, 1];
                    orientationPrev = (cyNext - cyPrev) * (txPrev - cxNext) - (cxNext - cxPrev) * (tyPrev - cyNext);

                    nBufferNext = 0;

                    for (j = 0; j < nBufferPrev; ++j)
                    {
                        txNext = bufferPrev[j, 0];
                        tyNext = bufferPrev[j, 1];

                        orientationNext = (cyNext - cyPrev) * (txNext - cxNext) - (cxNext - cxPrev) * (tyNext - cyNext);

                        if ((orientationPrev < 0 && orientationNext > 0) || (orientationPrev > 0 && orientationNext < 0))
                        {
                            a2 = tyNext - tyPrev;
                            b2 = txPrev - txNext;
                            c2 = a2 * txPrev + b2 * tyPrev;

                            d = 1.0f / (a1 * b2 - a2 * b1);

                            bufferNext[nBufferNext, 0] = (b2 * c1 - b1 * c2) * d;
                            bufferNext[nBufferNext, 1] = (a1 * c2 - a2 * c1) * d;
                            ++nBufferNext;
                        }

                        if (orientationNext >= 0)
                        {
                            bufferNext[nBufferNext, 0] = txNext;
                            bufferNext[nBufferNext, 1] = tyNext;
                            ++nBufferNext;
                        }

                        txPrev = txNext;
                        tyPrev = tyNext;
                        orientationPrev = orientationNext;
                    }

                    cxPrev = cxNext;
                    cyPrev = cyNext;

                    temp = bufferPrev;
                    bufferPrev = bufferNext;
                    bufferNext = temp;

                    nBufferPrev = nBufferNext;

                    if (nBufferPrev < 3)
                        break;
                }

                for (i = 0; i < nBufferPrev; ++i)
                {
                    _clipped[i, 0] = bufferPrev[i, 0];
                    _clipped[i, 1] = bufferPrev[i, 1];
                }

                _nClipped = nBufferPrev;
            }
        }

        #region Deprecated
        public class FrustumCullerDeprecated
        {
            private static int getMask(params int[] faces)
            {
                int m = 0;
                for (int i = 0; i < faces.Length; ++i)
                {
                    m += 1 << faces[i];
                }
                return m;
            }

            private static int[] s_masks = new int[8]
            {
                getMask(1, 4, 0),
                getMask(1, 4, 2),
                getMask(1, 5, 0),
                getMask(1, 5, 2),
                getMask(3, 4, 0),
                getMask(3, 4, 2),
                getMask(3, 5, 0),
                getMask(3, 5, 2),
            };

            private static int[,] s_lines = new int[12, 2]
            {
                { 0, 1 },
                { 2, 3 },
                { 4, 5 },
                { 6, 7 },
                { 0, 2 },
                { 1, 3 },
                { 4, 6 },
                { 5, 7 },
                { 0, 4 },
                { 1, 5 },
                { 2, 6 },
                { 3, 7 },
            };

            private static int[,] s_neighbors = new int[8, 3]
            {
                { 4, 2, 1 },
                { 5, 3, 0 },
                { 6, 0, 3 },
                { 7, 1, 2 },
                { 0, 6, 5 },
                { 1, 7, 4 },
                { 2, 4, 7 },
                { 3, 5, 6 },
            };

            private static float[,] s_viewPort = new float[4, 2]
            {
                { -1, -1 },
                { 1, -1 },
                { 1, 1 },
                { -1, 1 },
            };

            private float _vp00, _vp01, _vp02, _vp03, _vp10, _vp11, _vp12, _vp13, _vp30, _vp31, _vp32, _vp33;
            private float _ncpDist;

            private Vector3[] _minmax;
            private Vector3[] _points;
            private Vector2[,] _projected;
            private float[] _zDists;
            private bool[] _marked8;
            private bool[] _marked4;

            private Vector2[] _clipped;
            private int[] _clippedMasks;

            public (Matrix4x4, float) CameraParams
            {
                set
                {
                    _ncpDist = value.Item2;

                    Vector4 r0 = value.Item1.GetRow(0);
                    Vector4 r1 = value.Item1.GetRow(1);
                    Vector4 r3 = value.Item1.GetRow(3);

                    _vp00 = r0[0];
                    _vp01 = r0[1];
                    _vp02 = r0[2];
                    _vp03 = r0[3];
                    _vp10 = r1[0];
                    _vp11 = r1[1];
                    _vp12 = r1[2];
                    _vp13 = r1[3];
                    _vp30 = r3[0];
                    _vp31 = r3[1];
                    _vp32 = r3[2];
                    _vp33 = r3[3];
                }
            }

            public FrustumCullerDeprecated()
            {
                _minmax = new Vector3[2];
                _points = new Vector3[8];
                _projected = new Vector2[8, 3];
                _zDists = new float[8];
                _marked8 = new bool[8];
                _marked4 = new bool[8];
                _clipped = new Vector2[8];
                _clippedMasks = new int[8];

                _ncpDist = 0.3f;
                _vp00 = 1;
                _vp11 = 1;
                _vp33 = 1;
            }

            public bool Cull(Vector3 center, Vector3 extents)
            {
                _minmax[0].x = center.x - extents.x;
                _minmax[0].y = center.y - extents.y;
                _minmax[0].z = center.z - extents.z;
                _minmax[1].x = center.x + extents.x;
                _minmax[1].y = center.y + extents.y;
                _minmax[1].z = center.z + extents.z;

                int i, j, k, maxConsec, nConsec, vi1, vi2;
                float d, f;
                Vector2 v1, v2, v3, v4;

                int nClipped = 0;

                for (i = 0; i < 4; ++i) _marked4[i] = false;
                for (i = 0; i < 8; ++i)
                {
                    ref Vector3 p = ref _points[i];

                    p.x = _minmax[(i >> 2) & 1].x;
                    p.y = _minmax[(i >> 1) & 1].y;
                    p.z = _minmax[(i >> 0) & 1].z;

                    _zDists[i] = _vp30 * p.x + _vp31 * p.y + _vp32 * p.z + _vp33 - _ncpDist;
                    _marked8[i] = false;
                }

                for (i = 0; i < 8; ++i)
                {
                    if (_zDists[i] >= 0)
                    {
                        ref Vector3 p = ref _points[i];

                        f = 1 / (_zDists[i] + _ncpDist);

                        v1.x = (_vp00 * p.x + _vp01 * p.y + _vp02 * p.z + _vp03) * f;
                        v1.y = (_vp10 * p.x + _vp11 * p.y + _vp12 * p.z + _vp13) * f;

                        if (-1 < v1.x && v1.x < 1 && -1 < v1.y && v1.y < 1)
                        {
                            Debug.Log("return 1");
                            return true;
                        }

                        _projected[i, 0] = v1;
                    }
                    else
                    {
                        vi1 = i;
                        for (j = 0; j < 3; ++j)
                        {
                            vi2 = s_neighbors[i, j];

                            if (_zDists[vi2] > 0)
                            {
                                ref Vector3 p1 = ref _points[vi1];
                                ref Vector3 p2 = ref _points[vi2];

                                f = 1 / (1 - _zDists[vi2] / _zDists[vi1]);
                                v2.x = p1.x + (p2.x - p1.x) * f;
                                v2.y = p1.y + (p2.y - p1.y) * f;
                                d = p1.z + (p2.z - p1.z) * f;

                                v1.x = (_vp00 * v2.x + _vp01 * v2.y + _vp02 * d + _vp03) / _ncpDist;
                                v1.y = (_vp10 * v2.x + _vp11 * v2.y + _vp12 * d + _vp13) / _ncpDist;

                                if (-1 < v1.x && v1.x < 1 && -1 < v1.y && v1.y < 1)
                                {
                                    Debug.Log("return 2");
                                    return true;
                                }

                                _projected[i, j] = v1;

                                _clipped[nClipped] = v1;
                                _clippedMasks[nClipped] = s_masks[vi1] & s_masks[vi2];
                                ++nClipped;
                            }
                        }
                    }
                }

                for (i = 0; i < 8; ++i)
                {
                    if (_zDists[i] >= 0)
                    {
                        v1 = _projected[i, 0];
                        _marked4[0] = v1.y < -1;
                        _marked4[1] = v1.x > 1;
                        _marked4[2] = v1.y > 1;
                        _marked4[3] = v1.x < -1;

                        for (j = 0; j < 4; ++j)
                        {
                            if (_marked4[j])
                            {
                                if (_marked4[(j + 3) & 3])
                                {
                                    _marked8[((j << 1) + 7) & 7] = true;
                                }
                                else if (_marked4[(j + 1) & 3])
                                {
                                    _marked8[((j << 1) + 1) & 7] = true;
                                }
                                else
                                {
                                    _marked8[j << 1] = true;
                                }

                                break;
                            }
                        }
                    }
                }

                for (i = 0; i < nClipped; ++i)
                {
                    v1 = _clipped[i];
                    _marked4[0] = v1.y < -1;
                    _marked4[1] = v1.x > 1;
                    _marked4[2] = v1.y > 1;
                    _marked4[3] = v1.x < -1;

                    for (j = 0; j < 4; ++j)
                    {
                        if (_marked4[j])
                        {
                            if (_marked4[(j + 3) & 3])
                            {
                                _marked8[((j << 1) + 7) & 7] = true;
                            }
                            else if (_marked4[(j + 1) & 3])
                            {
                                _marked8[((j << 1) + 1) & 7] = true;
                            }
                            else
                            {
                                _marked8[j << 1] = true;
                            }

                            break;
                        }
                    }
                }

                i = 0;
                j = 8;

                while (i < j && !_marked8[i]) ++i;
                while (i < j && !_marked8[j - 1]) --j;

                maxConsec = i + 8 - j;

                if (maxConsec < 3)
                {
                    nConsec = 0;
                    for (; i < j; ++i)
                    {
                        if (_marked8[i])
                        {
                            if (maxConsec < nConsec) maxConsec = nConsec;
                            nConsec = 0;
                        }
                        else
                        {
                            ++nConsec;
                        }
                    }

                    if (maxConsec < nConsec) maxConsec = nConsec;
                    if (maxConsec < 3)
                    {
                        Debug.Log("return 3");
                        return true;
                    }
                }

                Debug.Log(maxConsec);

                for (i = 0; i < nClipped; ++i)
                {
                    for (j = i + 1; j < nClipped; ++j)
                    {
                        if ((_clippedMasks[i] & _clippedMasks[j]) > 0)
                        {
                            for (k = 0; k < 4; ++k)
                            {
                                v3.x = s_viewPort[k, 0];
                                v3.y = s_viewPort[k, 1];
                                v4.x = s_viewPort[(k + 1) & 3, 0];
                                v4.y = s_viewPort[(k + 1) & 3, 1];

                                if (isIntersect(_clipped[i], _clipped[j], v3, v4))
                                {
                                    Debug.Log("return 4");
                                    return true;
                                }
                            }
                        }
                    }
                }

                for (i = 0; i < 12; ++i)
                {
                    vi1 = s_lines[i, 0];
                    vi2 = s_lines[i, 1];

                    if (_zDists[vi1] < 0 && _zDists[vi2] < 0) continue;
                    if (_zDists[vi1] < 0 || _zDists[vi2] < 0)
                    {
                        if (_zDists[vi2] < 0)
                        {
                            vi1 = s_lines[i, 1];
                            vi2 = s_lines[i, 0];
                        }

                        f = 1 / (1 - _zDists[vi2] / _zDists[vi1]);
                        v2.x = _points[vi1].x + (_points[vi2].x - _points[vi1].x) * f;
                        v2.y = _points[vi1].y + (_points[vi2].y - _points[vi1].y) * f;
                        d = _points[vi1].z + (_points[vi2].z - _points[vi1].z) * f;

                        v1.x = (_vp00 * v2.x + _vp01 * v2.y + _vp02 * d + _vp03) / _ncpDist;
                        v1.y = (_vp10 * v2.x + _vp11 * v2.y + _vp12 * d + _vp13) / _ncpDist;
                        v2 = _projected[vi2, 0];
                    }
                    else
                    {
                        v1 = _projected[s_lines[i, 0], 0];
                        v2 = _projected[s_lines[i, 1], 0];
                    }

                    for (j = 0; j < 4; ++j)
                    {
                        v3.x = s_viewPort[j, 0];
                        v3.y = s_viewPort[j, 1];
                        v4.x = s_viewPort[(j + 1) & 3, 0];
                        v4.y = s_viewPort[(j + 1) & 3, 1];

                        if (isIntersect(v1, v2, v3, v4))
                        {
                            Debug.Log("return 5");
                            return true;
                        }
                    }
                }

                Debug.Log("return 6");
                return false;
            }

            private bool getOrientation(Vector2 p0, Vector2 v1, Vector2 v2)
            {
                return ((p0.y - v1.y) * (v2.x - p0.x) + (v1.x - p0.x) * (v2.y - p0.y)) >= 0;
            }

            private bool isIntersect(Vector2 v11, Vector2 v12, Vector2 v21, Vector2 v22)
            {
                return (getOrientation(v11, v12, v21) != getOrientation(v11, v12, v22)) && (getOrientation(v21, v22, v11) != getOrientation(v21, v22, v12));
            }
        }
        #endregion
    }
}
