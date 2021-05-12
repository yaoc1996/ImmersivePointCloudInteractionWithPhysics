using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPIC
{
    namespace Core
    {
        public class SceneIntegration : MonoBehaviour
        {
            private Material _mat;

            // Start is called before the first frame update
            private void Start()
            {
                _mat = new Material(Shader.Find("UnityPIC/SceneIntegration"));
            }

            private void OnRenderImage(RenderTexture source, RenderTexture destination)
            {
                UnityEngine.Graphics.Blit(source, destination, _mat);
            }

            private void OnDestroy()
            {
                Destroy(_mat);
            }
        }
    }
}
