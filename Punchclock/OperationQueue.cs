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
                    var ret = x.Select(y => ProcessOperation(y).TakeUntil(y.CancelSignal).Finally(() => scheduledGate.Release()));
                    return x.Key == defaultKey ? ret.Merge() : ret.Concat();
                })
                .Merge()
                .Multicast(new Subject<KeyedOperation>());

            resultObs.Connect();
        }

        static int sequenceNumber = 0;
        public IObservable<T> EnqueueObservableOperation<T, TDontCare>(int priority, string key, IObservable<TDontCare> cancel, Func<IObservable<T>> asyncCalculationFunc)
        {
            var item = new KeyedOperation<T> {
                Key = key,
                Id = Interlocked.Increment(ref sequenceNumber),
                Priority = priority,
                CancelSignal = cancel.Select(_ => Unit.Default),
                Func = asyncCalculationFunc,
            };

            lock (queuedOps) {
                Debug.WriteLine("Queued item {0}, priority {1}", item.Id, item.Priority);
                queuedOps.OnNext(item);
            }

            return item.Result;
        }

        public IObservable<T> EnqueueObservableOperation<T>(int priority, string key, Func<IObservable<T>> asyncCalculationFunc)
        {
            return EnqueueObservableOperation(priority, key, Observable.Never<Unit>(), asyncCalculationFunc);
        }

        public IObservable<T> EnqueueObservableOperation<T>(int priority, Func<IObservable<T>> asyncCalculationFunc)
        {
            return EnqueueObservableOperation(priority, defaultKey, Observable.Never<Unit>(), asyncCalculationFunc);
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
            Debug.WriteLine("Processing item {0}, priority {1}", operation.Id, operation.Priority);
            return Observable.Defer(operation.EvaluateFunc)
                .Select(_ => operation)
                .Catch(Observable.Return(operation));
        }
    }
}