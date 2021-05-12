using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPIC
{
    using Graphics.PackingFormat;
    using Base;
    using Utils;

    using Internals;
    using Internals.Potree;

    namespace Core
    {
        namespace Load
        {
            using Node = UPICInternalNode;

            public class ConcurrentLoadStrategy : AbstractLoadStrategy
            {
                private object _lock = new object();

                //private JobThread<LoadPointsJob> _readingThread;
                //private JobThread<LoadPointsJob> _parsingThread;
                //private JobThread<LoadPointsJob> _batchingThread;
                //private JobThread<LoadPointsJob> _postLoadingThread;

                private bool _initialized;

                public ConcurrentLoadStrategy()
                {
                    //_readingThread = new JobThread<LoadPointsJob>();
                    //_parsingThread = new JobThread<LoadPointsJob>();
                    //_batchingThread = new JobThread<LoadPointsJob>();
                    //_postLoadingThread = new JobThread<LoadPointsJob>();

                    //_readingThread.OnJobComplete += onReadingComplete;
                    //_parsingThread.OnJobComplete += onParsingComplete;
                    //_batchingThread.OnJobComplete += onBatchingComplete;
                    //_postLoadingThread.OnJobComplete += onPostLoadingComplete;

//#if UNITY_EDITOR
//                    _readingThread.Name = "UnityPIC::ReadingThread";
//                    _parsingThread.Name = "UnityPIC::ParsingThread";
//                    _batchingThread.Name = "UnityPIC::BatchingThread";
//                    _postLoadingThread.Name = "UnityPIC::PostLoadingThread";
//#endif
                }

                public override void Initialize()
                {
                    base.Initialize();

                    //_readingThread.Start();
                    //_parsingThread.Start();
                    //_batchingThread.Start();
                    //_postLoadingThread.Start();

                    _initialized = true;
                }

                public override void Dispose()
                {
                    if (_initialized)
                    {
                        //_readingThread.Join();
                        //_parsingThread.Join();
                        //_batchingThread.Join();
                        //_postLoadingThread.Join();

                        _initialized = false;
                    }

                    //_readingThread.Clear();
                    //_parsingThread.Clear();
                    //_batchingThread.Clear();
                    //_postLoadingThread.Clear();
                }

                public override LoadPointsJob Load(Node node, float p)
                {
                    LoadPointsJob job;

                    job = new LoadPointsJob(node, p);

                    job.Stage = LoadPointsJob.JobStage.Reading;
                    job.State = LoadPointsJob.JobState.Loading;

                    //_readingThread.Dispatch(job, job.Priority);
                    job.Execute(null);
                    onReadingComplete(job);
                    job.Execute(null);
                    onParsingComplete(job);
                    job.Execute(null);
                    onBatchingComplete(job);
                    job.Execute(null);
                    onPostLoadingComplete(job);

                    return job;
                }

                public override void Cancel(Node node)
                {
                    //lock (_lock)
                    //{
                    //    LoadPointsJob job = node.LoadPointsJob;

                    //    job.State = LoadPointsJob.JobState.Cancelled;

                    //    switch (job.Stage)
                    //    {
                    //        case LoadPointsJob.JobStage.Reading:
                    //            _readingThread.Cancel(job);
                    //            break;
                    //        case LoadPointsJob.JobStage.Parsing:
                    //            _parsingThread.Cancel(job);
                    //            break;
                    //        case LoadPointsJob.JobStage.Batching:
                    //            _batchingThread.Cancel(job);
                    //            break;
                    //        case LoadPointsJob.JobStage.PostLoading:
                    //            _postLoadingThread.Cancel(job);
                    //            break;
                    //        case LoadPointsJob.JobStage.WaitingForParentPoints:
                    //            break;
                    //    }
                    //}
                }

                public override void UpdatePriority(Node node, float p)
                {
                    //lock (_lock)
                    //{
                    //    LoadPointsJob job = node.LoadPointsJob;

                    //    job.Priority = p;

                    //    switch (job.Stage)
                    //    {
                    //        case LoadPointsJob.JobStage.Reading:
                    //            _readingThread.UpdatePriority(job, p);
                    //            break;
                    //        case LoadPointsJob.JobStage.Parsing:
                    //            _parsingThread.UpdatePriority(job, p);
                    //            break;
                    //        case LoadPointsJob.JobStage.Batching:
                    //            _batchingThread.UpdatePriority(job, p);
                    //            break;
                    //        case LoadPointsJob.JobStage.PostLoading:
                    //            _postLoadingThread.UpdatePriority(job, p);
                    //            break;
                    //        case LoadPointsJob.JobStage.WaitingForParentPoints:
                    //            break;
                    //    }
                    //}
                }

                private void OnDestroy()
                {
                    Dispose();
                }

                private void onReadingComplete(LoadPointsJob job)
                {
                    lock (_lock)
                    {
                        if (job.State != LoadPointsJob.JobState.Cancelled)
                        {
                            job.Stage = LoadPointsJob.JobStage.Parsing;
                            //_parsingThread.Dispatch(job, job.Priority);
                        }
                    }
                }

                private void onParsingComplete(LoadPointsJob job)
                {
                    lock (_lock)
                    {
                        if (job.State != LoadPointsJob.JobState.Cancelled)
                        {
                            if (job.PrepareForDynamicBatching())
                            {
                                job.Stage = LoadPointsJob.JobStage.Batching;
                                //_batchingThread.Dispatch(job, job.Priority);
                            }
                            else
                            {
                                job.Stage = LoadPointsJob.JobStage.WaitingForParentPoints;
                            }
                        }
                    }
                }

                private void onBatchingComplete(LoadPointsJob job)
                {
                    lock (_lock)
                    {
                        if (job.State != LoadPointsJob.JobState.Cancelled)
                        {
                            Node node = job.Node;

                            node.Points = job.AugmentedPoints;
                            node.PartitionOffsets = job.PartitionOffsets;

                            dispatchPendingChildren(node);

                            job.Stage = LoadPointsJob.JobStage.PostLoading;
                            //_postLoadingThread.Dispatch(job, job.Priority);
                        }
                    }
                }

                private void onPostLoadingComplete(LoadPointsJob job)
                {
                    lock (_lock)
                    {
                        if (job.State != LoadPointsJob.JobState.Cancelled)
                        {
                            Node node = job.Node;

                            node.IntensityRange = job.IntensityRange;
                            node.Points = job.AugmentedPoints;
                            node.Edges = job.Edges;
                        }
                    }

                    OnNodeLoaded?.Invoke(job.Node);
                }

                private void dispatchPendingChildren(Node node)
                {
                    if (!node.IsLeaf)
                    {
                        Node child;
                        LoadPointsJob childJob;

                        for (int i = 0; i < 8; ++i)
                        {
                            child = node.Children[i];
                            childJob = child.LoadPointsJob;

                            if (childJob != null && childJob.Stage == LoadPointsJob.JobStage.WaitingForParentPoints)
                            {
                                childJob.PrepareForDynamicBatching();
                                childJob.Stage = LoadPointsJob.JobStage.Batching;
                                //_batchingThread.Dispatch(childJob, childJob.Priority);
                            }
                        }
                    }
                }
            }
        }
    }
}
