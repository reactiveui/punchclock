using System;
using System.Collections.Generic;
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
        public string Key { get; set; }
        
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
                return this.KeyIsDefault ? -1 : 1;
            }

            return this.Priority.CompareTo(other.Priority);
        }
    }

    class KeyedOperation<T> : KeyedOperation
    {
        public Func<IObservable<T>> Func { get; set; }
        public readonly ReplaySubject<T> Result = new ReplaySubject<T>();

        public override IObservable<Unit> EvaluateFunc()
        {
            var ret = Func().Multicast(Result);
            ret.Connect();

            return ret.Select(_ => Unit.Default);
        }
    }

    public class OperationQueue
    {
        internal const string defaultKey = "__NONE__";

        readonly Subject<KeyedOperation> queuedOps = new Subject<KeyedOperation>();
        readonly IConnectableObservable<KeyedOperation> resultObs;
        readonly PrioritySemaphoreSubject<KeyedOperation> scheduledGate;
        readonly int maximumConcurrent;

        int pauseRefCount = 0;

        AsyncSubject<Unit> shutdownObs;

        public OperationQueue(int maximumConcurrent = 4)
        {
            this.maximumConcurrent = maximumConcurrent;
            scheduledGate = new PrioritySemaphoreSubject<KeyedOperation>(maximumConcurrent);

            resultObs = queuedOps
                .Multicast(scheduledGate).RefCount()
                .GroupBy(x => x.Key)
                .Select(x => {
                    var ret = x.Select(y => ProcessOperation(y).Finally(() => scheduledGate.Release()));
                    return x.Key == defaultKey ? ret.Merge() : ret.Concat();
                })
                .Merge()
                .Multicast(new Subject<KeyedOperation>());

            resultObs.Connect();
        }

        public IObservable<T> EnqueueObservableOperation<T>(int priority, string key, Func<IObservable<T>> asyncCalculationFunc)
        {
            var item = new KeyedOperation<T> {
                Key = key,
                Priority = priority,
                Func = asyncCalculationFunc,
            };

            lock (queuedOps) {
                queuedOps.OnNext(item);
            }

            return item.Result;
        }

        public IObservable<T> EnqueueObservableOperation<T>(int priority, Func<IObservable<T>> asyncCalculationFunc)
        {
            return EnqueueObservableOperation(priority, defaultKey, asyncCalculationFunc);
        }

        public IDisposable PauseQueue()
        {
            if (Interlocked.Increment(ref pauseRefCount) == 1) {
                scheduledGate.MaximumCount = 0;
            }

            return Disposable.Create(() => {
                if (Interlocked.Decrement(ref pauseRefCount) > 0) return;
                scheduledGate.MaximumCount = maximumConcurrent;
            });
        }

        public IObservable<Unit> ShutdownQueue()
        {
            lock (queuedOps) {
                if (shutdownObs != null) return shutdownObs;
                shutdownObs = new AsyncSubject<Unit>();

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
            return Observable.Defer(operation.EvaluateFunc)
                .Select(_ => operation)
                .Catch(Observable.Return(operation));
        }
    }
}