using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPIC
{
    using Graphics.PackingFormat;
    using Internals;
    using Internals.Potree;

    namespace Base
    {
        using Node = UPICInternalNode;

        public abstract class AbstractLoadStrategy : MonoBehaviour
        {
            public UPICInternalNodeActionDelegate OnNodeLoaded;

            public abstract LoadPointsJob Load(Node node, float p);

            public virtual void Initialize() { }
            public virtual void Dispose() { }
            public virtual void UpdatePriority(Node node, float p) { }
            public virtual void Cancel(Node node) { }

            public AbstractLoadStrategy() { } 

        }
    }
}
