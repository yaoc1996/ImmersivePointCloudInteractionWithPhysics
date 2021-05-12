using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPIC
{
    using Graphics.PackingFormat;
    using Internals;
    using Utils;

    namespace Core
    {
        using Node = UPICInternalNode;

        namespace PostProcessing
        {
            // Not Done

            public class ComputeIntensityDynamicRangeJob : AbstractJob
            {
                public override void Execute(object args)
                {
                }

                public Node Node;
                public PosColIntCls20[] Points;
                public (float, float) Range;
            }

            public class ConcurrentIntensityNormalizer : MonoBehaviour
            {
                private JobThread<ComputeIntensityDynamicRangeJob> _jobThread;
                private bool _initialized;

                public delegate void OnCompleteCallback(Node node, (float, float) range);

                //public OnCompleteCallback OnComputation

                public ConcurrentIntensityNormalizer()
                {
                    _jobThread = new JobThread<ComputeIntensityDynamicRangeJob>();
                    _initialized = false;
                }

                public void Initialize()
                {
                    _jobThread.Start();
                }

                public void ComputeIntensityDynamicRange(Node node)
                {

                }

                private void OnDestroy()
                {
                    if (_initialized)
                    {
                        _jobThread.Join();
                    }
                }
            }
        }
    }
}
