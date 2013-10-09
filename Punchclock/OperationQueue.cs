using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace Punchclock
{
    abstract class KeyedOperation : IComparable<KeyedOperation>
    {
        public int Priority { get; set; }
        public int Id { get; set; }
        public string Key { get; set; }
        public IObservable<Unit> CancelSignal { get; set; }
        
        public abstract IObservable<Unit> EvaluateFunc();

        public bool KeyIsDefault {
            get { return (String.IsNullOrEmpty(Key) || Key == OperationQueue.defaultKey); }
        }

        public int CompareTo(KeyedOperation other)
        {
            // NB: Non-keyed operations always come before keyed operations in
            // order to make sure that serialized keyed operations don't take 
            // up concurrency slots
            if (this.KeyIsDefault != other.KeyIsDefault) {
                return this.KeyIsDefault ? 1 : -1;
            }

            return other.Priority.CompareTo(this.Priority);
        }
    }

    class KeyedOperation<T> : KeyedOperation
    {
        public Func<IObservable<T>> Func { get; set; }
        public readonly ReplaySubject<T> Result = new ReplaySubject<T>();

        public override IObservable<Unit> EvaluateFunc()
        {
            var ret = Func().TakeUntil(CancelSignal).Multicast(Result);
            ret.Connect();

            return ret.Select(_ => Unit.Default);
        }
    }

    /// <summary>
    /// OperationQueue is the core of PunchClock, and represents a scheduler for
    /// deferred actions, such as network requests. This scheduler supports 
    /// scheduling via priorities, as well as serializing requests that access
    /// the same data.
    ///
    /// The queue allows a fixed number of concurrent in-flight operations at a
    /// time. When there are available "slots", items are dispatched as they come
    /// in. When the slots are full, the queueing policy starts to apply.
    /// 
    /// The queue, similar to Akavache's KeyedOperationQueue, also allows keys to
    /// be specified to serialize operations - if you have three "foo" items, they
    /// will wait in line and only one "foo" can run. However, a "bar" and "baz" 
    /// item can run at the same time as a "foo" item.
    /// </summary>
    public class OperationQueue
    {
        internal const string defaultKey = "__NONE__";

        readonly Subject<KeyedOperation> queuedOps = new Subject<KeyedOperation>();
        readonly IConnectableObservable<KeyedOperation> resultObs;
        readonly PrioritySemaphoreSubject<KeyedOperation> scheduledGate;
        readonly int maximumConcurrent;

        static int sequenceNumber = 0;
        int pauseRefCount = 0;

        AsyncSubject<Unit> shutdownObs;

        /// <summary>
        /// Createa a new operation queue
        /// </summary>
        /// <param name="maximumConcurrent">The maximum number of concurrent operations.</param>
        public OperationQueue(int maximumConcurrent = 4)
        {
            this.maximumConcurrent = maximumConcurrent;
            scheduledGate = new PrioritySemaphoreSubject<KeyedOperation>(maximumConcurrent);

            resultObs = queuedOps
                .Multicast(scheduledGate).RefCount()
                .GroupBy(x => x.Key)
                .Select(x => {
                    var ret = x.Select(y => ProcessOperation(y).TakeUntil(y.CancelSignal).Finally(() => scheduledGate.Release()));
                    return x.Key == defaultKey ? ret.Merge() : ret.Concat();
                })
                .Merge()
                .Multicast(new Subject<KeyedOperation>());

            resultObs.Connect();
        }

        /// <summary>
        /// This method enqueues an action to be run at a later time, according
        /// to the scheduling policies (i.e. via priority and key).
        /// </summary>
        /// <param name="priority">Higher priorities run before lower ones.</param>
        /// <param name="key">Items with the same key will be run in order.</param>
        /// <param name="cancel">If signalled, the operation will be cancelled.</param>
        /// <param name="asyncCalculationFunc">The async method to execute when scheduled.</param>
        /// <returns>The result of the async calculation.</returns>
        public IObservable<T> EnqueueObservableOperation<T, TDontCare>(int priority, string key, IObservable<TDontCare> cancel, Func<IObservable<T>> asyncCalculationFunc)
        {
            var id = Interlocked.Increment(ref sequenceNumber);
            var cancelReplay = new ReplaySubject<TDontCare>();
            cancel.Multicast(cancelReplay).Connect();

            var item = new KeyedOperation<T> {
                Key = key,
                Id = id,
                Priority = priority,
                CancelSignal = cancelReplay.Select(_ => Unit.Default).Do(_ => Debug.WriteLine("Cancelling {0}", id)),
                Func = asyncCalculationFunc,
            };

            lock (queuedOps) {
                Debug.WriteLine("Queued item {0}, priority {1}", item.Id, item.Priority);
                queuedOps.OnNext(item);
            }

            return item.Result;
        }

        /// <summary>
        /// This method enqueues an action to be run at a later time, according
        /// to the scheduling policies (i.e. via priority and key).
        /// </summary>
        /// <param name="priority">Higher priorities run before lower ones.</param>
        /// <param name="key">Items with the same key will be run in order.</param>
        /// <param name="asyncCalculationFunc">The async method to execute when scheduled.</param>
        /// <returns>The result of the async calculation.</returns>
        public IObservable<T> EnqueueObservableOperation<T>(int priority, string key, Func<IObservable<T>> asyncCalculationFunc)
        {
            return EnqueueObservableOperation(priority, key, Observable.Never<Unit>(), asyncCalculationFunc);
        }

        /// <summary>
        /// This method enqueues an action to be run at a later time, according
        /// to the scheduling policies (i.e. via priority)
        /// </summary>
        /// <param name="priority">Higher priorities run before lower ones.</param>
        /// <param name="asyncCalculationFunc">The async method to execute when scheduled.</param>
        /// <returns>The result of the async calculation.</returns>
        public IObservable<T> EnqueueObservableOperation<T>(int priority, Func<IObservable<T>> asyncCalculationFunc)
        {
            return EnqueueObservableOperation(priority, defaultKey, Observable.Never<Unit>(), asyncCalculationFunc);
        }

        /// <summary>
        /// This method pauses the dispatch queue. Inflight operations will not
        /// be canceled, but new ones will not be processed until the queue is
        /// resumed.
        /// </summary>
        /// <returns>A Disposable that resumes the queue when disposed.</returns>
        public IDisposable PauseQueue()
        {
            if (Interlocked.Increment(ref pauseRefCount) == 1) {
                scheduledGate.MaximumCount = 0;
            }

            return Disposable.Create(() => {
                if (Interlocked.Decrement(ref pauseRefCount) > 0) return;
                if (shutdownObs != null) return;

                scheduledGate.MaximumCount = maximumConcurrent;
            });
        }

        /// <summary>
        /// Shuts down the queue and notifies when all outstanding items have
        /// been processed.
        /// </summary>
        /// <returns>An Observable that will signal when all items are complete.
        /// </returns>
        public IObservable<Unit> ShutdownQueue()
        {
            lock (queuedOps) {
                if (shutdownObs != null) return shutdownObs;
                shutdownObs = new AsyncSubject<Unit>();

                // Disregard paused queue
                scheduledGate.MaximumCount = maximumConcurrent;

                queuedOps.OnCompleted();

                resultObs.Materialize()
                    .Where(x => x.Kind != NotificationKind.OnNext)
                    .SelectMany(x =>
                        (x.Kind == NotificationKind.OnError) ?
                            Observable.Throw<Unit>(x.Exception) :
                            Observable.Return(Unit.Default))
                    .Multicast(shutdownObs)
                    .Connect();

                return shutdownObs;
            }
        }

        static IObservable<KeyedOperation> ProcessOperation(KeyedOperation operation)
        {
            Debug.WriteLine("Processing item {0}, priority {1}", operation.Id, operation.Priority);
            return Observable.Defer(operation.EvaluateFunc)
                .Select(_ => operation)
                .Catch(Observable.Return(operation));
        }
    }
}