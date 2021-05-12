using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace UnityPIC
{
    namespace Utils
    {
        using Ref = PriorityQueue<AbstractJob, float>.Ref;

        public abstract class AbstractJob
        {
            public Ref JobThreadPQRef;
            public abstract void Execute(object args);
        }

        public class JobThread<JobType> where JobType : AbstractJob
        {
            private Thread _thread;
            private PriorityQueue<AbstractJob, float> _jobQueue;

            public delegate void Callback(JobType job);

            public Callback OnJobComplete;
            public Callback OnJobCancel;

#if UNITY_EDITOR
            public string Name
            {
                get { return _thread.Name; }
                set { _thread.Name = value; }
            }
#endif

            public JobThread()
            {
                _thread = new Thread(threadMain);
                _jobQueue = new PriorityQueue<AbstractJob, float>();
            }

            ~JobThread()
            {
                Join();
            }

            public void Start(object obj=null)
            {
                _thread.Start(obj);
            }

            public void Join()
            {
                Clear();
                _thread.Join();
            }

            public void Dispatch(JobType job, float p)
            {
                lock(_jobQueue)
                {
                    job.JobThreadPQRef = _jobQueue.Enqueue(job, p);
                    _thread.Signal();
                }
            }

            public bool Cancel(JobType job)
            {
                lock (_jobQueue)
                {
                    if (job.JobThreadPQRef.IsValid)
                    {
                        _jobQueue.Remove(ref job.JobThreadPQRef);
                    }
                    else
                    {
                        return false;
                    }
                }

                OnJobCancel?.Invoke(job);
                return true;
            }

            public bool UpdatePriority(JobType job, float p)
            {
                lock (_jobQueue)
                {
                    if (job.JobThreadPQRef.IsValid)
                    {
                        _jobQueue.UpdatePriority(ref job.JobThreadPQRef, p);
                        return true;
                    }
                }

                return true;
            }

            public void Clear()
            {
                JobType[] jobs;

                lock(_jobQueue)
                {
                    jobs = new JobType[_jobQueue.Count];

                    using (var enumerator = _jobQueue.GetEnumerator())
                    {
                        int i = 0;
                        while (enumerator.MoveNext())
                        {
                            jobs[i] = (JobType)enumerator.Current;
                            jobs[i].JobThreadPQRef = Ref.Null;
                            ++i;
                        }
                    }

                    _jobQueue.Clear();
                }

                for (int i = 0, c = jobs.Length; i < c; ++i)
                {
                    OnJobCancel?.Invoke(jobs[i]);
                }
            }

            private void threadMain(object args)
            {
                JobType nextJob;

                lock(_jobQueue)
                {
                    if (_jobQueue.Count == 0)
                    {
                        _thread.Suspend();
                        return;
                    }
                    else
                    {
                        nextJob = (JobType)_jobQueue.Dequeue();
                        nextJob.JobThreadPQRef = Ref.Null;
                    }
                }

                nextJob.Execute(args);
                OnJobComplete?.Invoke(nextJob);
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
