using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace UnityPIC
{
    namespace Utils
    {
        public class Thread
        {
            private System.Threading.Thread _thread;
            private ManualResetEvent _mre;
            private ParameterizedThreadStart _main;
            private bool _isRunning;
            private object _params;
            private object _lock = new object();

#if UNITY_EDITOR
            public string Name
            {
                get { return _thread.Name; }
                set { _thread.Name = value; }
            }
#endif

            public Thread(ParameterizedThreadStart threadStart)
            {
                _thread = new System.Threading.Thread(threadMain);
                _main = threadStart;
                _mre = new ManualResetEvent(false);
            }

            ~Thread()
            {
                Join();
            }

            public void Start(object obj=null)
            {
                if (_thread != null)
                {
                    _isRunning = true;
                    _params = obj;
                    _thread.Start();
                }
            }

            public void Join()
            {
                if (_isRunning)
                {
                    lock(_lock)
                    {
                        _isRunning = false;
                    }

                    Signal();
                    _thread.Join();
                    _thread = null;
                }
            }

            public virtual void Suspend()
            {
                _mre.Reset();
            }

            public virtual void Signal()
            {
                _mre.Set();
            }

            private void threadMain()
            {
                while (true)
                {
                    //Debug.Log("PreRunning");

                    lock (_lock)
                    {
                        if (!_isRunning) break;
                    }

                    _mre.WaitOne();
                    _main(_params);
                }
            }
        }

        //public class ThreadPool
        //{
        //    private List<Thread> _threads;
        //    private Queue<Thread> _inactive;

        //    private delegate void OnSuspendDelegate();

        //    private class InternalThread : Thread
        //    {
        //        private OnSuspendDelegate _onSuspend;
        //        public InternalThread(ParameterizedThreadStart threadStart, OnSuspendDelegate onSuspend) : base(threadStart)
        //        {
        //            _onSuspend = onSuspend;
        //        }

        //        public override void Suspend()
        //        {
        //            base.Suspend();
        //        }
        //    }

        //    public ThreadPool(int n, ParameterizedThreadStart threadStart)
        //    {
        //        _threads = new List<Thread>();
        //        _inactive = new Queue<Thread>();

        //        for (int i = 0; i < n; ++i)
        //        {
        //            _threads.Add(new Thread(threadStart));
        //        }
        //    }
        //}
    }
}
