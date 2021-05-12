using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityPIC.Utils;

namespace UnityPIC
{
    using Internals.Potree;

    namespace Internals
    {
        using Subtree = Potree<UPICInternalNode>.Subtree;

        public class LoadSubtreeJob : AbstractJob
        {
            public override void Execute(object args)
            {
                Subtree.Load();
                //System.Threading.Thread.Sleep(400);
            }

            public Subtree Subtree;
        }
    }
}
