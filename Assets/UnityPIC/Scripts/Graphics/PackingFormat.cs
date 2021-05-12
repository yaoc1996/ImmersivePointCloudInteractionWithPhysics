using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityPIC
{
    namespace Graphics
    {
        namespace PackingFormat
        {
            [StructLayout(LayoutKind.Explicit, Size = 16)]
            public struct PosCol16
            {
                [FieldOffset(0)] public Vector3 Position;
                [FieldOffset(12)] public Color32 Color;
            }

            [StructLayout(LayoutKind.Explicit, Size = 20)]
            public struct PosColIntCls20
            {
                [FieldOffset(0)] public Vector3 Position;
                [FieldOffset(12)] public Color32 Color;
                [FieldOffset(16)] public uint Attributes;
            }
        }

    }
}
