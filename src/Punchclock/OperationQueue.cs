// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Random is used for non-security purposes (priority queue tiebreaking) and supports deterministic seeding for tests")]
public class OperationQueue : IDisposable
{
    /// <summary>
    /// Global sequence number for operation IDs across all OperationQueue instances.
    /// Ensures globally unique and monotonically increasing IDs for debugging and ordering.
    /// </summary>
    private static int sequenceNumber;

    /// <summary>
    /// Synchronization primitive guarding mutations to queue state.
    /// </summary>
    /// <remarks>
    /// Protects access to <see cref="_shutdownObs"/>, <see cref="_maximumConcurrent"/>, and queue operations.
    /// </remarks>
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif

    /// <summary>
    /// Subject that receives enqueued operations from user code.
    /// </summary>
    private readonly Subject<KeyedOperation> _queuedOps = new();

    /// <summary>
    /// The connected observable that processes and executes queued operations.
    /// </summary>
    private readonly IConnectableObservable<KeyedOperation> _resultObs;

    /// <summary>
    /// The semaphore gate that controls concurrent execution based on priority.
    /// </summary>
    private readonly PrioritySemaphoreSubject<KeyedOperation> _scheduledGate;

    /// <summary>
    /// Whether to randomize execution order among equal-priority items across different keys.
    /// </summary>
    private readonly bool _randomizeEqualPriority;

    /// <summary>
    /// Random number generator for tie-breaking when <see cref="_randomizeEqualPriority"/> is enabled.
    /// Null when randomization is disabled.
    /// </summary>
    private readonly Random? _random;

    /// <summary>
    /// Maximum number of concurrent operations allowed. Modified under lock via <see cref="SetMaximumConcurrent"/>.
    /// </summary>
    private int _maximumConcurrent;

    /// <summary>
    /// Reference count for pause operations. When greater than zero, the queue is paused.
    /// </summary>
    private int _pauseRefCount;

    /// <summary>
    /// Tracks whether this instance has been disposed.
    /// </summary>
    private bool _isDisposed;

    /// <summary>
    /// Observable that signals when shutdown is complete. Null until <see cref="ShutdownQueue"/> is called.
    /// </summary>
    private AsyncSubject<Unit>? _shutdownObs;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationQueue"/> class.
    /// </summary>
    /// <param name="maximumConcurrent">The maximum number of concurrent operations. Must be positive.</param>
    public OperationQueue(int maximumConcurrent = 4)
        : this(maximumConcurrent, randomizeEqualPriority: false, seed: null, scheduler: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationQueue"/> class with a scheduler.
    /// </summary>
    /// <param name="maximumConcurrent">The maximum number of concurrent operations. Must be positive.</param>
    /// <param name="scheduler">Scheduler for controlling execution timing. Useful for testing.</param>
    public OperationQueue(int maximumConcurrent, IScheduler scheduler)
        : this(maximumConcurrent, randomizeEqualPriority: false, seed: null, scheduler: scheduler)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationQueue"/> class with optional randomness support.
    /// </summary>
    /// <param name="maximumConcurrent">The maximum number of concurrent operations. Must be positive.</param>
    /// <param name="randomizeEqualPriority">If true, randomizes execution order among equal-priority items across different keys.</param>
    /// <param name="seed">Optional seed to make randomization deterministic for tests.</param>
    public OperationQueue(int maximumConcurrent, bool randomizeEqualPriority, int? seed)
        : this(maximumConcurrent, randomizeEqualPriority, seed, scheduler: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationQueue"/> class with optional randomness support and scheduler.
    /// </summary>
    /// <param name="maximumConcurrent">The maximum number of concurrent operations. Must be positive.</param>
    /// <param name="randomizeEqualPriority">If true, randomizes execution order among equal-priority items across different keys.</param>
    /// <param name="seed">Optional seed to make randomization deterministic for tests.</param>
    /// <param name="scheduler">Scheduler for controlling execution timing. Useful for testing.</param>
    public OperationQueue(int maximumConcurrent, bool randomizeEqualPriority, int? seed, IScheduler? scheduler)
    {
        if (maximumConcurrent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrent), "Maximum concurrent operations must be positive.");
        }

        _maximumConcurrent = maximumConcurrent;
        _scheduledGate = new(maximumConcurrent, scheduler);
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
    /// Gets the default key used for non-keyed operations.
    /// Operations with this key run concurrently with each other.
    /// </summary>
    internal static string DefaultKey { get; } = "__NONE__";

    /// <summary>
    /// This method enqueues an action to be run at a later time, according
    /// to the scheduling policies (i.e. via priority and key).
    /// </summary>
    /// <typeparam name="T">The type of item for the observable.</typeparam>
    /// <typeparam name="TDontCare">Type parameter used to allow any observable type for cancellation signal.</typeparam>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="key">A key to apply to the operation. Items with the same key will be run in order. Pass null or <see cref="DefaultKey"/> for non-keyed operations.</param>
    /// <param name="cancel">An observable which if signalled, the operation will be cancelled.</param>
    /// <param name="asyncCalculationFunc">The async method to execute when scheduled.</param>
    /// <returns>An observable that produces the result of the async calculation.</returns>
    public IObservable<T> EnqueueObservableOperation<T, TDontCare>(int priority, string key, IObservable<TDontCare> cancel, Func<IObservable<T>> asyncCalculationFunc)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(cancel);
        ArgumentNullException.ThrowIfNull(asyncCalculationFunc);
#else
        if (cancel is null)
        {
            throw new ArgumentNullException(nameof(cancel));
        }

        if (asyncCalculationFunc is null)
        {
            throw new ArgumentNullException(nameof(asyncCalculationFunc));
        }
#endif

        var id = Interlocked.Increment(ref sequenceNumber);
        var cancelReplay = new ReplaySubject<TDontCare>();

        var item = new KeyedOperation<T>
        {
            Key = string.IsNullOrEmpty(key) ? DefaultKey : key,
            Id = id,
            Priority = priority,
            CancelSignal = cancelReplay.Select(_ => Unit.Default),
            Func = asyncCalculationFunc,
            UseRandomTiebreak = _randomizeEqualPriority,
            RandomOrder = _randomizeEqualPriority ? _random!.Next() : 0,
        };

        cancel
            .Do(_ =>
            {
                Debug.WriteLine("Cancelling {0}", id);
                item.CancelledEarly = true;
            })
            .Multicast(cancelReplay).Connect();

        lock (_gate)
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
    /// <param name="key">Items with the same key will be run in order. Pass null or <see cref="DefaultKey"/> for non-keyed operations.</param>
    /// <param name="asyncCalculationFunc">The async method to execute when scheduled.</param>
    /// <returns>An observable that produces the result of the async calculation.</returns>
    public IObservable<T> EnqueueObservableOperation<T>(int priority, string key, Func<IObservable<T>> asyncCalculationFunc) =>
        EnqueueObservableOperation(priority, key, Observable.Never<Unit>(), asyncCalculationFunc);

    /// <summary>
    /// This method enqueues an action to be run at a later time, according
    /// to the scheduling policies (i.e. via priority).
    /// </summary>
    /// <typeparam name="T">The type of item for the observable.</typeparam>
    /// <param name="priority">Higher priorities run before lower ones.</param>
    /// <param name="asyncCalculationFunc">The async method to execute when scheduled.</param>
    /// <returns>An observable that produces the result of the async calculation.</returns>
    public IObservable<T> EnqueueObservableOperation<T>(int priority, Func<IObservable<T>> asyncCalculationFunc) =>
        EnqueueObservableOperation(priority, DefaultKey, Observable.Never<Unit>(), asyncCalculationFunc);

    /// <summary>
    /// This method pauses the dispatch queue. Inflight operations will not
    /// be canceled, but new ones will not be processed until the queue is
    /// resumed.
    /// </summary>
    /// <returns>A Disposable that resumes the queue when disposed. Multiple pause calls are ref-counted.</returns>
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

            // Don't resume if we've been shut down (no lock needed for this check)
            if (Volatile.Read(ref _shutdownObs) != null)
            {
                return;
            }

            // Resume the queue - MaximumCount setter is thread-safe
            // Reading _maximumConcurrent without lock is safe because SetMaximumConcurrent
            // uses PauseQueue(), so when _pauseRefCount reaches 0, the value is stable
            _scheduledGate.MaximumCount = Volatile.Read(ref _maximumConcurrent);
        });
    }

    /// <summary>
    /// Sets the maximum level of concurrency for the operation queue.
    /// The queue is paused during the update to ensure consistency.
    /// </summary>
    /// <param name="maximumConcurrent">The maximum amount of concurrency. Must be positive.</param>
    public void SetMaximumConcurrent(int maximumConcurrent)
    {
        if (maximumConcurrent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrent), "Maximum concurrent operations must be positive.");
        }

        using (PauseQueue())
        {
            lock (_gate)
            {
                _maximumConcurrent = maximumConcurrent;
            }
        }
    }

    /// <summary>
    /// Shuts down the queue and notifies when all outstanding items have
    /// been processed. After shutdown, no new operations can be enqueued.
    /// </summary>
    /// <returns>An Observable that will signal when all items are complete,
    /// or an error if any operations failed during shutdown.</returns>
    public IObservable<Unit> ShutdownQueue()
    {
        lock (_gate)
        {
            if (_shutdownObs != null)
            {
                return _shutdownObs;
            }

            _shutdownObs = new AsyncSubject<Unit>();

            // Disregard paused queue - force resume to drain
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
            _queuedOps.Dispose();
            _shutdownObs?.Dispose();
        }

        _isDisposed = true;
    }

    /// <summary>
    /// Processes a single operation by evaluating its function.
    /// Catches exceptions and returns the operation regardless of success or failure.
    /// </summary>
    /// <param name="operation">The operation to process.</param>
    /// <returns>An observable that emits the operation when complete.</returns>
    private static IObservable<KeyedOperation> ProcessOperation(KeyedOperation operation)
    {
        Debug.WriteLine("Processing item {0}, priority {1}", operation.Id, operation.Priority);
        return Observable.Defer(operation.EvaluateFunc)
            .Select(_ => operation)
            .Catch(Observable.Return(operation));
    }
}
