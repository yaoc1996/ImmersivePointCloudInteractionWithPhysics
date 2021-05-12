using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPIC
{
    using Core;

    namespace Base
    {
        using Node = UPICNode;

        public abstract class AbstractRenderStrategy : MonoBehaviour
        {
            protected Camera targetCamera;
            protected Vector4 shift;

            public abstract void OnNodeRender(Node node);
            public abstract void OnNodeRemove(Node node);
            public abstract void OnNodeUpdate(Node node);

            public virtual void Initialize(Camera camera, Vector4 s)
            {
                targetCamera = camera;
                shift = s;
            }

            public virtual void Dispose() { }
        }
    }
}
