// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading;

namespace Punchclock;

/// <summary>
/// A subject that enforces a maximum concurrent count using priority-based semaphore semantics.
/// Items are queued when the semaphore is full and dequeued in priority order when capacity becomes available.
/// </summary>
/// <typeparam name="T">The type of item, which must be comparable for priority ordering.</typeparam>
internal class PrioritySemaphoreSubject<T> : ISubject<T>
    where T : IComparable<T>
{
    /// <summary>
    /// The inner subject that receives items once they pass through the semaphore.
    /// </summary>
    private readonly ISubject<T> _inner;

    /// <summary>
    /// Synchronization primitive guarding mutations to the priority queue.
    /// </summary>
    /// <remarks>
    /// Protects updates to <see cref="_nextItems"/>. Count operations use interlocked semantics.
    /// </remarks>
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif

    /// <summary>
    /// Priority queue holding items waiting for available semaphore slots.
    /// Null when the subject has been completed or errored.
    /// </summary>
    private PriorityQueue<T>? _nextItems = new();

    /// <summary>
    /// Current count of items that have been yielded and are consuming semaphore slots.
    /// </summary>
    private int _count;

    /// <summary>
    /// Backing field for MaximumCount property.
    /// </summary>
    private int _maximumCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrioritySemaphoreSubject{T}"/> class.
    /// </summary>
    /// <param name="maxCount">The maximum number of items to allow through the semaphore concurrently.</param>
    /// <param name="sched">The scheduler to use when emitting items to the inner subject. If null, uses immediate scheduling.</param>
    public PrioritySemaphoreSubject(int maxCount, IScheduler? sched = null)
    {
        _inner = sched != null ? new ScheduledSubject<T>(sched) : new Subject<T>();
        _maximumCount = maxCount;
    }

    /// <summary>
    /// Gets or sets the maximum count of items allowed through the semaphore concurrently.
    /// Setting this property triggers draining of queued items if capacity increases.
    /// </summary>
    public int MaximumCount
    {
        get => Volatile.Read(ref _maximumCount);
        set
        {
            Volatile.Write(ref _maximumCount, value);
            YieldUntilEmptyOrBlocked();
        }
    }

    /// <inheritdoc />
    public void OnNext(T value)
    {
        var queue = Volatile.Read(ref _nextItems);
        if (queue is null)
        {
            return;
        }

        lock (_gate)
        {
            // Re-check after acquiring lock - might have been completed/errored
            queue = Volatile.Read(ref _nextItems);
            if (queue is null)
            {
                return;
            }

            queue.Enqueue(value);
        }

        YieldUntilEmptyOrBlocked();
    }

    /// <summary>
    /// Releases a semaphore slot, decrementing the count and triggering drain of queued items.
    /// </summary>
    public void Release()
    {
        Interlocked.Decrement(ref _count);
        YieldUntilEmptyOrBlocked();
    }

    /// <inheritdoc />
    public void OnCompleted()
    {
        PriorityQueue<T>? queue;
        lock (_gate)
        {
            queue = Interlocked.Exchange(ref _nextItems, null);
        }

        if (queue is null)
        {
            return;
        }

        // Drain all remaining items to inner subject
#if NET8_0_OR_GREATER
        // Use Span-based API with ArrayPool to reduce allocations
        const int batchSize = 128;
        var pool = System.Buffers.ArrayPool<T>.Shared;
        var buffer = pool.Rent(Math.Min(queue.Count, batchSize));

        try
        {
            while (queue.Count > 0)
            {
                var count = queue.DequeueRange(buffer.AsSpan());
                for (var i = 0; i < count; i++)
                {
                    _inner.OnNext(buffer[i]);
                }
            }
        }
        finally
        {
            pool.Return(buffer, clearArray: System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
#else
        // Legacy TFMs: use array-based API
        var items = queue.DequeueAll();
        foreach (var v in items)
        {
            _inner.OnNext(v);
        }
#endif

        _inner.OnCompleted();
    }

    /// <inheritdoc />
    public void OnError(Exception error)
    {
        lock (_gate)
        {
            Interlocked.Exchange(ref _nextItems, null);
        }

        _inner.OnError(error);
    }

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer) => _inner.Subscribe(observer);

    /// <summary>
    /// Dequeues and yields items from the priority queue while count is below maximum.
    /// Acquires the gate lock for each item to prevent oversubscription and ensure correct ordering.
    /// </summary>
    private void YieldUntilEmptyOrBlocked()
    {
        while (true)
        {
            T next;
            lock (_gate)
            {
                var queue = Volatile.Read(ref _nextItems);
                if (queue is null || queue.Count == 0)
                {
                    return;
                }

                var currentCount = Volatile.Read(ref _count);
                var maxCount = Volatile.Read(ref _maximumCount);

                if (currentCount >= maxCount)
                {
                    return;
                }

                next = queue.Dequeue();
                Interlocked.Increment(ref _count);
            }

            // Emit outside the lock to avoid blocking enqueue operations
            _inner.OnNext(next);
        }
    }
}
