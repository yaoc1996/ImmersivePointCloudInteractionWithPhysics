using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace UnityPIC
{   
    static class UPICDebug
    {
        public delegate void TimeTask();
        public static double GetElapsed(TimeTask task)
        {
            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            task();
            timer.Stop();
            return timer.Elapsed.TotalSeconds;
        }

        public static void LogElapsed(TimeTask task)
        {
            Debug.Log(GetElapsed(task));
        }

        //public static int GetSizeInMemory<T>(T obj)
        //{
        //    using (var ms = new MemoryStream())
        //    {
        //        BinaryFormatter format = new BinaryFormatter();
        //        format.Serialize(ms, obj);
        //        return (int)ms.Length;
        //    }
        //}
    }
}
