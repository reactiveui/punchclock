// Copyright (c) 2021 .NET Foundation and Contributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading;

namespace Punchclock
{
    internal class PrioritySemaphoreSubject<T> : ISubject<T>
        where T : IComparable<T>
    {
        private readonly ISubject<T> _inner;
        private PriorityQueue<T> _nextItems = new PriorityQueue<T>();
        private int _count;

        private int _MaximumCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrioritySemaphoreSubject{T}"/> class.
        /// </summary>
        /// <param name="maxCount">The maximum number of items to allow.</param>
        /// <param name="sched">The scheduler to use when emitting the items.</param>
        public PrioritySemaphoreSubject(int maxCount, IScheduler? sched = null)
        {
            _inner = sched != null ? (ISubject<T>)new ScheduledSubject<T>(sched) : new Subject<T>();
            MaximumCount = maxCount;
        }

        /// <summary>
        /// Gets or sets the maximum count to allow.
        /// </summary>
        public int MaximumCount
        {
            get => _MaximumCount;
            set
            {
                _MaximumCount = value;
                YieldUntilEmptyOrBlocked();
            }
        }

        /// <inheritdoc />
        public void OnNext(T value)
        {
            var queue = Interlocked.CompareExchange(ref _nextItems, null!, null!);
            if (queue == null)
            {
                return;
            }

            lock (queue)
            {
                queue.Enqueue(value);
            }

            YieldUntilEmptyOrBlocked();
        }

        /// <summary>
        /// Releases a reference counted value.
        /// </summary>
        public void Release()
        {
            Interlocked.Decrement(ref _count);
            YieldUntilEmptyOrBlocked();
        }

        /// <inheritdoc />
        public void OnCompleted()
        {
            var queue = Interlocked.Exchange(ref _nextItems, null!);
            if (queue == null)
            {
                return;
            }

            T[] items;
            lock (queue)
            {
                items = queue.DequeueAll();
            }

            foreach (var v in items)
            {
                _inner.OnNext(v);
            }

            _inner.OnCompleted();
        }

        /// <inheritdoc />
        public void OnError(Exception error)
        {
            Interlocked.Exchange(ref _nextItems, null!);
            _inner.OnError(error);
        }

        /// <inheritdoc />
        public IDisposable Subscribe(IObserver<T> observer)
        {
            return _inner.Subscribe(observer);
        }

        private void YieldUntilEmptyOrBlocked()
        {
            var queue = Interlocked.CompareExchange(ref _nextItems, null!, null!);

            if (queue == null)
            {
                return;
            }

            while (_count < MaximumCount)
            {
                T next;
                lock (queue)
                {
                    if (queue.Count == 0)
                    {
                        break;
                    }

                    next = queue.Dequeue();
                }

                _inner.OnNext(next);

                if (Interlocked.Increment(ref _count) >= MaximumCount)
                {
                    break;
                }
            }
        }
    }
}
