using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityPIC
{
    namespace Utils
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 1)]
        public struct ByteMask
        {
            private byte _byte;

            public static ByteMask None = new ByteMask() { _byte = 0 };

            public bool IsEmpty { get { return _byte == 0; } }
            public static implicit operator byte(ByteMask f) => f._byte;

            public ByteMask(byte b)
            {
                _byte = b;
            }

            public static ByteMask operator +(ByteMask bits, byte bit)
            {
                bits._byte = (byte)(bits._byte | (1 << bit));
                return bits;
            }

            public static ByteMask operator -(ByteMask bits, byte bit)
            {
                bits._byte = (byte)(bits._byte & (~(1 << bit)));
                return bits;
            }

            public static bool operator ==(ByteMask f1, ByteMask f2)
            {
                return f1._byte == f2._byte;
            }

            public static bool operator !=(ByteMask f1, ByteMask f2)
            {
                return !(f1 == f2);
            }

            public bool Has(byte bit)
            {
                return (_byte & (1 << bit)) > 0;
            }

            public override bool Equals(object obj)
            {
                return this == (ByteMask)obj;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public override string ToString()
            {
                return _byte.ToString();
            }
        }
    }
}
