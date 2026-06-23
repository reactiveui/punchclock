// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

#if REACTIVE_SHIM

namespace Punchclock.Reactive;
#else

namespace Punchclock;
#endif

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
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Random - used for non-security purposes (priority queue tiebreaking), supports deterministic seeding for tests")]
public class OperationQueue : IDisposable
{
    private const int MaximumConcurrent = 4;

    /// <summary>
    /// Global sequence number for operation IDs across all OperationQueue instances.
    /// Ensures globally unique and monotonically increasing IDs for debugging and ordering.
    /// </summary>
    private static int _sequenceNumber;

    /// <summary>Synchronization primitive guarding mutations to queue state.</summary>
    /// <remarks>
    /// Protects access to <see cref="_shutdownObs"/>, <see cref="_maximumConcurrent"/>, and queue operations.
    /// </remarks>
    private readonly Lock _gate = new();

    /// <summary>Pending operations ordered by priority.</summary>
    private readonly PriorityQueue<KeyedOperation> _pendingOperations = new();

    /// <summary>Keys that currently have an active operation.</summary>
    private readonly HashSet<string> _activeKeys = [];

    /// <summary>Sequencer used to start scheduled operations.</summary>
    private readonly IScheduler _sequencer;

    /// <summary>Whether to randomize execution order among equal-priority items across different keys.</summary>
    private readonly bool _randomizeEqualPriority;

    /// <summary>Random number generator for tie-breaking when <see cref="_randomizeEqualPriority"/> is enabled. Null when randomization is disabled.</summary>
    private readonly Random? _random;

    /// <summary>Maximum number of concurrent operations allowed. Modified under lock via <see cref="SetMaximumConcurrent"/>.</summary>
    private int _maximumConcurrent;

    /// <summary>Reference count for pause operations. When greater than zero, the queue is paused.</summary>
    private int _pauseRefCount;

    /// <summary>Number of currently active operations.</summary>
    private int _activeCount;

    /// <summary>Tracks whether this instance has been disposed.</summary>
    private bool _isDisposed;

    /// <summary>Observable that signals when shutdown is complete. Null until <see cref="ShutdownQueue"/> is called.</summary>
    private ReplaySignal<Unit>? _shutdownObs;

    /// <summary>Tracks whether shutdown has already been signalled.</summary>
    private bool _shutdownCompleted;

    /// <summary>Initializes a new instance of the <see cref="OperationQueue"/> class.</summary>
    public OperationQueue()
        : this(MaximumConcurrent)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OperationQueue"/> class.</summary>
    /// <param name="maximumConcurrent">The maximum number of concurrent operations. Must be positive.</param>
    public OperationQueue(int maximumConcurrent)
        : this(maximumConcurrent, randomizeEqualPriority: false, seed: null, scheduler: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OperationQueue"/> class with a scheduler.</summary>
    /// <param name="maximumConcurrent">The maximum number of concurrent operations. Must be positive.</param>
    /// <param name="scheduler">Sequencer for controlling execution timing. Useful for testing.</param>
    public OperationQueue(int maximumConcurrent, IScheduler scheduler)
        : this(maximumConcurrent, randomizeEqualPriority: false, seed: null, scheduler: scheduler)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OperationQueue"/> class with optional randomness support.</summary>
    /// <param name="maximumConcurrent">The maximum number of concurrent operations. Must be positive.</param>
    /// <param name="randomizeEqualPriority">If true, randomizes execution order among equal-priority items across different keys.</param>
    /// <param name="seed">Optional seed to make randomization deterministic for tests.</param>
    public OperationQueue(int maximumConcurrent, bool randomizeEqualPriority, int? seed)
        : this(maximumConcurrent, randomizeEqualPriority, seed, scheduler: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OperationQueue"/> class with optional randomness support and scheduler.</summary>
    /// <param name="maximumConcurrent">The maximum number of concurrent operations. Must be positive.</param>
    /// <param name="randomizeEqualPriority">If true, randomizes execution order among equal-priority items across different keys.</param>
    /// <param name="seed">Optional seed to make randomization deterministic for tests.</param>
    /// <param name="scheduler">Sequencer for controlling execution timing. Useful for testing.</param>
    public OperationQueue(int maximumConcurrent, bool randomizeEqualPriority, int? seed, IScheduler? scheduler)
    {
        ArgumentOutOfRangeExceptionHelper.ThrowIfNegativeOrZero(maximumConcurrent);

        _maximumConcurrent = maximumConcurrent;
        _sequencer = scheduler ?? Scheduler.Immediate;
        _randomizeEqualPriority = randomizeEqualPriority;
        if (randomizeEqualPriority)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }
        else
        {
            _random = null;
        }
    }

    /// <summary>Gets the default key used for non-keyed operations. Operations with this key run concurrently with each other.</summary>
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
        ArgumentExceptionHelper.ThrowIfNull(cancel);
        ArgumentExceptionHelper.ThrowIfNull(asyncCalculationFunc);

        var id = Interlocked.Increment(ref _sequenceNumber);
        var cancelReplay = new ReplaySignal<TDontCare>();

        var item = new KeyedOperation<T>
        {
            Key = string.IsNullOrEmpty(key) ? DefaultKey : key,
            Id = id,
            Priority = priority,
            CancelSignal = cancelReplay.Map(_ => Unit.Default),
            Func = asyncCalculationFunc,
            UseRandomTiebreak = _randomizeEqualPriority,
            RandomOrder = _randomizeEqualPriority ? _random!.Next() : 0,
        };

        item.CancelSubscription = cancel.Subscribe(
            value =>
            {
                Debug.WriteLine("Cancelling {0}", id);
                item.CancelledEarly = true;
                cancelReplay.OnNext(value);
                ScheduleOperations();
            },
            cancelReplay.OnError,
            cancelReplay.OnCompleted);

        lock (_gate)
        {
            if (_shutdownObs is not null)
            {
                item.CancelSubscription.Dispose();
                throw new InvalidOperationException("Cannot enqueue operations after shutdown has started.");
            }

            Debug.WriteLine("Queued item {0}, priority {1}", item.Id, item.Priority);
            _pendingOperations.Enqueue(item);
        }

        ScheduleOperations();
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
        EnqueueObservableOperation(priority, key, Signal.Silent<Unit>(), asyncCalculationFunc);

    /// <summary>This method enqueues an action to be run at a later time, according to the scheduling policies (i.e. via priority).</summary>
    /// <typeparam name="T">The type of item for the observable.</typeparam>
    /// <param name="priority">Higher priorities run before lower ones.</param>
    /// <param name="asyncCalculationFunc">The async method to execute when scheduled.</param>
    /// <returns>An observable that produces the result of the async calculation.</returns>
    public IObservable<T> EnqueueObservableOperation<T>(int priority, Func<IObservable<T>> asyncCalculationFunc) =>
        EnqueueObservableOperation(priority, DefaultKey, Signal.Silent<Unit>(), asyncCalculationFunc);

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
            ScheduleOperations();
        }

        return Disposable.Create(() =>
        {
            if (Interlocked.Decrement(ref _pauseRefCount) > 0)
            {
                return;
            }

            // Don't resume if we've been shut down (no lock needed for this check)
            if (Volatile.Read(ref _shutdownObs) is not null)
            {
                return;
            }

            ScheduleOperations();
        });
    }

    /// <summary>
    /// Sets the maximum level of concurrency for the operation queue.
    /// The queue is paused during the update to ensure consistency.
    /// </summary>
    /// <param name="maximumConcurrent">The maximum amount of concurrency. Must be positive.</param>
    public void SetMaximumConcurrent(int maximumConcurrent)
    {
        ArgumentOutOfRangeExceptionHelper.ThrowIfNegativeOrZero(maximumConcurrent);

        using (PauseQueue())
        {
            lock (_gate)
            {
                _maximumConcurrent = maximumConcurrent;
            }
        }

        ScheduleOperations();
    }

    /// <summary>
    /// Shuts down the queue and notifies when all outstanding items have
    /// been processed. After shutdown, no new operations can be enqueued.
    /// </summary>
    /// <returns>An Observable that will signal when all items are complete,
    /// or an error if any operations failed during shutdown.</returns>
    public IObservable<Unit> ShutdownQueue()
    {
        ReplaySignal<Unit> shutdown;
        lock (_gate)
        {
            if (_shutdownObs is not null)
            {
                return _shutdownObs;
            }

            shutdown = new ReplaySignal<Unit>();
            _shutdownObs = shutdown;
        }

        ScheduleOperations();
        CompleteShutdownIfReady();
        return shutdown;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes managed resources that are disposable and handles cleanup of unmanaged items.</summary>
    /// <param name="isDisposing">If we are disposing managed resources.</param>
    protected virtual void Dispose(bool isDisposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (isDisposing)
        {
            KeyedOperation[] pending;
            lock (_gate)
            {
                pending = _pendingOperations.DequeueAll();
            }

            foreach (var operation in pending)
            {
                operation.CancelSubscription!.Dispose();
            }

            _shutdownObs?.Dispose();
        }

        _isDisposed = true;
    }

    /// <summary>Schedules pending operations that have capacity to run.</summary>
    private void ScheduleOperations()
    {
        var operationsToStart = new List<KeyedOperation>();

        lock (_gate)
        {
            DrainPendingOperations(operationsToStart);
        }

        for (var i = 0; i < operationsToStart.Count; i++)
        {
            var operation = operationsToStart[i];
            ScheduleStart(operation);
        }
    }

    /// <summary>Moves eligible pending operations to the active set.</summary>
    /// <param name="operationsToStart">The operations to start after leaving the queue lock.</param>
    private void DrainPendingOperations(List<KeyedOperation> operationsToStart)
    {
        if (SchedulingIsPaused())
        {
            return;
        }

        var deferred = new List<KeyedOperation>();
        try
        {
            while (_activeCount < _maximumConcurrent && _pendingOperations.Count > 0)
            {
                var operation = _pendingOperations.Dequeue();
                AddReadyOperation(operation, deferred, operationsToStart);
            }
        }
        finally
        {
            for (var i = 0; i < deferred.Count; i++)
            {
                _pendingOperations.Enqueue(deferred[i]);
            }
        }
    }

    /// <summary>Determines whether scheduling should pause before shutdown starts.</summary>
    /// <returns>True when normal scheduling is paused; otherwise false.</returns>
    private bool SchedulingIsPaused() => _shutdownObs is null && Volatile.Read(ref _pauseRefCount) > 0;

    /// <summary>Adds the operation to the start list, defers it, or disposes an early-cancelled registration.</summary>
    /// <param name="operation">The operation being considered for execution.</param>
    /// <param name="deferred">The operations blocked by active keyed work.</param>
    /// <param name="operationsToStart">The operations ready to start after leaving the queue lock.</param>
    private void AddReadyOperation(
        KeyedOperation operation,
        List<KeyedOperation> deferred,
        List<KeyedOperation> operationsToStart)
    {
        if (operation.CancelledEarly)
        {
            operation.CancelSubscription!.Dispose();
            return;
        }

        if (IsBlockedByActiveKey(operation))
        {
            deferred.Add(operation);
            return;
        }

        MarkOperationActive(operation);
        operationsToStart.Add(operation);
    }

    /// <summary>Determines whether a keyed operation is blocked by another active operation with the same key.</summary>
    /// <param name="operation">The operation to check.</param>
    /// <returns>True if the operation must wait for an active operation with the same key; otherwise false.</returns>
    private bool IsBlockedByActiveKey(KeyedOperation operation) =>
        !operation.KeyIsDefault && operation.Key is { } key && _activeKeys.Contains(key);

    /// <summary>Marks an operation active and records its key when needed.</summary>
    /// <param name="operation">The operation being started.</param>
    private void MarkOperationActive(KeyedOperation operation)
    {
        _activeCount++;
        if (operation.KeyIsDefault || operation.Key is not { } activeKey)
        {
            return;
        }

        _activeKeys.Add(activeKey);
    }

    /// <summary>Starts a scheduled operation.</summary>
    /// <param name="operation">The operation to start.</param>
    private void StartOperation(KeyedOperation operation)
    {
        Debug.WriteLine("Processing item {0}, priority {1}", operation.Id, operation.Priority);

        try
        {
            operation.EvaluateFunc().Subscribe(
                _ => { },
                _ => FinishOperation(operation),
                () => FinishOperation(operation));
        }
        catch
        {
            FinishOperation(operation);
        }
    }

    /// <summary>Marks an operation as finished and schedules more work if capacity is available.</summary>
    /// <param name="operation">The operation that finished.</param>
    private void FinishOperation(KeyedOperation operation)
    {
        operation.CancelSubscription!.Dispose();

        var operationsToStart = new List<KeyedOperation>();
        var completeShutdown = false;

        lock (_gate)
        {
            if (_activeCount > 0)
            {
                _activeCount--;
            }

            if (!operation.KeyIsDefault && operation.Key is { } key)
            {
                _activeKeys.Remove(key);
            }

            DrainPendingOperations(operationsToStart);
            completeShutdown = IsShutdownReady();
        }

        for (var i = 0; i < operationsToStart.Count; i++)
        {
            var next = operationsToStart[i];
            ScheduleStart(next);
        }

        if (!completeShutdown)
        {
            return;
        }

        CompleteShutdown();
    }

    /// <summary>Completes shutdown if there is no pending or active work.</summary>
    private void CompleteShutdownIfReady()
    {
        bool completeShutdown;
        lock (_gate)
        {
            completeShutdown = IsShutdownReady();
        }

        if (!completeShutdown)
        {
            return;
        }

        CompleteShutdown();
    }

    /// <summary>Schedules an operation start using the active scheduler shape for the current package variant.</summary>
    /// <param name="operation">The operation to start.</param>
    private void ScheduleStart(KeyedOperation operation)
    {
#if REACTIVE_SHIM
        _sequencer.Schedule(
            (Queue: this, Operation: operation),
            static (_, state) =>
            {
                state.Queue.StartOperation(state.Operation);
                return Disposable.Empty;
            });
#else
        _sequencer.Schedule(
            (Queue: this, Operation: operation),
            static state => state.Queue.StartOperation(state.Operation));
#endif
    }

    /// <summary>Determines whether shutdown can be signalled.</summary>
    /// <returns><see langword="true"/> if shutdown is ready; otherwise, <see langword="false"/>.</returns>
    private bool IsShutdownReady()
    {
        if (_shutdownObs is null || _shutdownCompleted || _activeCount != 0 || _pendingOperations.Count != 0)
        {
            return false;
        }

        _shutdownCompleted = true;
        return true;
    }

    /// <summary>Emits and completes the shutdown signal.</summary>
    private void CompleteShutdown()
    {
        var shutdown = Volatile.Read(ref _shutdownObs)!;
        shutdown.OnNext(Unit.Default);
        shutdown.OnCompleted();
    }
}
