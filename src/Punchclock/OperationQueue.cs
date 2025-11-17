// Copyright (c) 2025 .NET Foundation and Contributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace Punchclock;

/// <summary>
/// <para>
/// OperationQueue is the core of PunchClock, and represents a scheduler for
/// deferred actions, such as network requests. This scheduler supports
/// scheduling via priorities, as well as serializing requests that access
/// the same data.
/// </para>
/// <para>
/// The queue allows a fixed number of concurrent in-flight operations at a
/// time. When there are available "slots", items are dispatched as they come
/// in. When the slots are full, the queueing policy starts to apply.
/// </para>
/// <para>
/// The queue, similar to Akavache's KeyedOperationQueue, also allows keys to
/// be specified to serialize operations - if you have three "foo" items, they
/// will wait in line and only one "foo" can run. However, a "bar" and "baz"
/// item can run at the same time as a "foo" item.
/// </para>
/// </summary>
public class OperationQueue : IDisposable
{
    private static int sequenceNumber;

    private readonly Subject<KeyedOperation> _queuedOps = new();
    private readonly IConnectableObservable<KeyedOperation> _resultObs;
    private readonly PrioritySemaphoreSubject<KeyedOperation> _scheduledGate;
    private readonly bool _randomizeEqualPriority;
    private readonly Random? _random;
    private int _maximumConcurrent;
    private int _pauseRefCount;
    private bool _isDisposed;

    private AsyncSubject<Unit>? _shutdownObs;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationQueue"/> class.
    /// </summary>
    /// <param name="maximumConcurrent">The maximum number of concurrent operations.</param>
    public OperationQueue(int maximumConcurrent = 4)
        : this(maximumConcurrent, false, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationQueue"/> class with optional randomness support.
    /// </summary>
    /// <param name="maximumConcurrent">The maximum number of concurrent operations.</param>
    /// <param name="randomizeEqualPriority">If true, randomizes execution order among equal-priority items across different keys.</param>
    /// <param name="seed">Optional seed to make randomization deterministic for tests.</param>
    public OperationQueue(int maximumConcurrent, bool randomizeEqualPriority, int? seed)
    {
        _maximumConcurrent = maximumConcurrent;
        _scheduledGate = new(maximumConcurrent);
        _randomizeEqualPriority = randomizeEqualPriority;
        _random = randomizeEqualPriority ? (seed.HasValue ? new Random(seed.Value) : new Random()) : null;

        _resultObs = _queuedOps
            .Multicast(_scheduledGate).RefCount()
            .GroupBy(x => x.Key)
            .Select(x =>
            {
                var ret = x.Select(
                    y => ProcessOperation(y)
                        .TakeUntil(y.CancelSignal ?? Observable.Empty<Unit>())
                        .Finally(() => _scheduledGate.Release()));
                return x.Key == DefaultKey ? ret.Merge() : ret.Concat();
            })
            .Merge()
            .Multicast(new Subject<KeyedOperation>());

        _resultObs.Connect();
    }

    /// <summary>
    /// Gets the default key used if there is no item.
    /// </summary>
    internal static string DefaultKey { get; } = "__NONE__";

    /// <summary>
    /// This method enqueues an action to be run at a later time, according
    /// to the scheduling policies (i.e. via priority and key).
    /// </summary>
    /// <typeparam name="T">The type of item for the observable.</typeparam>
    /// <typeparam name="TDontCare">Used to allow any observable type.</typeparam>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="key">A key to apply to the operation. Items with the same key will be run in order.</param>
    /// <param name="cancel">A observable which if signalled, the operation will be cancelled.</param>
    /// <param name="asyncCalculationFunc">The async method to execute when scheduled.</param>
    /// <returns>The result of the async calculation.</returns>
    public IObservable<T> EnqueueObservableOperation<T, TDontCare>(int priority, string key, IObservable<TDontCare> cancel, Func<IObservable<T>> asyncCalculationFunc)
    {
        var id = Interlocked.Increment(ref sequenceNumber);
        var cancelReplay = new ReplaySubject<TDontCare>();

        var item = new KeyedOperation<T>
        {
            Key = key ?? DefaultKey,
            Id = id,
            Priority = priority,
            CancelSignal = cancelReplay.Select(_ => Unit.Default),
            Func = asyncCalculationFunc,
        };

        if (_randomizeEqualPriority)
        {
            item.UseRandomTiebreak = true;
            item.RandomOrder = _random!.Next();
        }

        cancel
            .Do(_ =>
            {
                Debug.WriteLine("Cancelling {0}", id);
                item.CancelledEarly = true;
            })
            .Multicast(cancelReplay).Connect();

        lock (_queuedOps)
        {
            Debug.WriteLine("Queued item {0}, priority {1}", item.Id, item.Priority);
            _queuedOps.OnNext(item);
        }

        return item.Result;
    }

    /// <summary>
    /// This method enqueues an action to be run at a later time, according
    /// to the scheduling policies (i.e. via priority and key).
    /// </summary>
    /// <typeparam name="T">The type of item for the observable.</typeparam>
    /// <param name="priority">Higher priorities run before lower ones.</param>
    /// <param name="key">Items with the same key will be run in order.</param>
    /// <param name="asyncCalculationFunc">The async method to execute when scheduled.</param>
    /// <returns>The result of the async calculation.</returns>
    public IObservable<T> EnqueueObservableOperation<T>(int priority, string key, Func<IObservable<T>> asyncCalculationFunc) =>
        EnqueueObservableOperation(priority, key, Observable.Never<Unit>(), asyncCalculationFunc);

    /// <summary>
    /// This method enqueues an action to be run at a later time, according
    /// to the scheduling policies (i.e. via priority).
    /// </summary>
    /// <typeparam name="T">The type of item for the observable.</typeparam>
    /// <param name="priority">Higher priorities run before lower ones.</param>
    /// <param name="asyncCalculationFunc">The async method to execute when scheduled.</param>
    /// <returns>The result of the async calculation.</returns>
    public IObservable<T> EnqueueObservableOperation<T>(int priority, Func<IObservable<T>> asyncCalculationFunc) =>
        EnqueueObservableOperation(priority, DefaultKey, Observable.Never<Unit>(), asyncCalculationFunc);

    /// <summary>
    /// This method pauses the dispatch queue. Inflight operations will not
    /// be canceled, but new ones will not be processed until the queue is
    /// resumed.
    /// </summary>
    /// <returns>A Disposable that resumes the queue when disposed.</returns>
    public IDisposable PauseQueue()
    {
        if (Interlocked.Increment(ref _pauseRefCount) == 1)
        {
            _scheduledGate.MaximumCount = 0;
        }

        return Disposable.Create(() =>
        {
            if (Interlocked.Decrement(ref _pauseRefCount) > 0)
            {
                return;
            }

            if (_shutdownObs != null)
            {
                return;
            }

            _scheduledGate.MaximumCount = _maximumConcurrent;
        });
    }

    /// <summary>
    /// Sets the maximum level of concurrency for the operation queue.
    /// </summary>
    /// <param name="maximumConcurrent">The maximum amount of concurrency.</param>
    public void SetMaximumConcurrent(int maximumConcurrent)
    {
        using (PauseQueue())
        {
            _maximumConcurrent = maximumConcurrent;
        }
    }

    /// <summary>
    /// Shuts down the queue and notifies when all outstanding items have
    /// been processed.
    /// </summary>
    /// <returns>An Observable that will signal when all items are complete.
    /// </returns>
    public IObservable<Unit> ShutdownQueue()
    {
        lock (_queuedOps)
        {
            if (_shutdownObs != null)
            {
                return _shutdownObs;
            }

            _shutdownObs = new AsyncSubject<Unit>();

            // Disregard paused queue
            _scheduledGate.MaximumCount = _maximumConcurrent;

            _queuedOps.OnCompleted();

            _resultObs.Materialize()
                .Where(x => x.Kind != NotificationKind.OnNext)
                .SelectMany(x =>
                    x.Kind == NotificationKind.OnError ?
                        Observable.Throw<Unit>(x.Exception!) :
                        Observable.Return(Unit.Default))
                .Multicast(_shutdownObs)
                .Connect();

            return _shutdownObs;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources that are disposable and handles cleanup of unmanaged items.
    /// </summary>
    /// <param name="isDisposing">If we are disposing managed resources.</param>
    protected virtual void Dispose(bool isDisposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (isDisposing)
        {
            _queuedOps?.Dispose();
            _shutdownObs?.Dispose();
        }

        _isDisposed = true;
    }

    private static IObservable<KeyedOperation> ProcessOperation(KeyedOperation operation)
    {
        Debug.WriteLine("Processing item {0}, priority {1}", operation.Id, operation.Priority);
        return Observable.Defer(operation.EvaluateFunc)
            .Select(_ => operation)
            .Catch(Observable.Return(operation));
    }
}
