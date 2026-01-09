// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Punchclock;

/// <summary>
/// A priority queue which stores items in order of their priorities using a quaternary (4-ary) heap.
/// Items are stored in a quaternary heap structure where higher-priority items are dequeued first.
/// </summary>
/// <typeparam name="T">The type of item to store in the queue.</typeparam>
/// <remarks>
/// Quaternary heaps provide 15-43% better performance than binary heaps across all workload sizes
/// due to shallower trees, better cache locality, and fewer comparisons. Benchmarks show consistent
/// improvements from small (16 items) to large (1000+ items) queues across all .NET runtimes.
/// Based on Microsoft internal code originally from Rx.NET, enhanced with quaternary heap structure.
/// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.
/// This is https://github.com/mono/rx/blob/master/Rx/NET/Source/System.Reactive.Core/Reactive/Internal/PriorityQueue.cs originally.
/// </remarks>
internal class PriorityQueue<T>
    where T : IComparable<T>
{
    /// <summary>
    /// Default initial capacity for the priority queue.
    /// </summary>
    private const int DefaultCapacity = 16;

    /// <summary>
    /// Number of children per node in the quaternary heap.
    /// </summary>
    private const int Arity = 4;

    /// <summary>
    /// Sequence counter for FIFO tie-breaking among equal-priority items (instance-scoped for isolation).
    /// </summary>
    private long _sequenceCounter = long.MinValue;

    /// <summary>
    /// Internal array storing heap-ordered items.
    /// </summary>
    private IndexedItem[] _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="PriorityQueue{T}"/> class with a specified capacity.
    /// </summary>
    /// <param name="capacity">The starting capacity of the queue.</param>
    public PriorityQueue(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be non-negative.");
        }

        _items = capacity == 0 ? [] : new IndexedItem[capacity];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PriorityQueue{T}"/> class with the default capacity.
    /// </summary>
    public PriorityQueue()
        : this(DefaultCapacity)
    {
    }

    /// <summary>
    /// Gets the number of items inside the queue.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Peeks at the next item available in the queue without removing it.
    /// </summary>
    /// <returns>The next item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public T Peek()
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("There are no items in the collection");
        }

        return _items[0].Value;
    }

    /// <summary>
    /// Removes and returns the next item in the queue.
    /// </summary>
    /// <returns>The next item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
    public T Dequeue()
    {
        var result = Peek();
        RemoveAt(0, single: true);
        return result;
    }

    /// <summary>
    /// Removes up to the specified number of items and returns those items.
    /// </summary>
    /// <param name="count">The maximum number of items to remove from the queue.</param>
    /// <returns>An array containing the dequeued items.</returns>
    public T[] DequeueSome(int count)
    {
        if (count == 0)
        {
            return [];
        }

        var actualCount = Math.Min(count, Count);
        var ret = new T[actualCount];

        for (var i = 0; i < actualCount; i++)
        {
            ret[i] = Peek();
            RemoveAt(0, single: false);
        }

        return ret;
    }

    /// <summary>
    /// Removes all the items currently contained within the queue and returns them.
    /// </summary>
    /// <returns>An array containing all items from the queue in priority order.</returns>
    public T[] DequeueAll() => DequeueSome(Count);

    /// <summary>
    /// Adds an item in the correct location based on priority to the queue.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Enqueue(T item)
    {
        if (Count >= _items.Length)
        {
            var newCapacity = _items.Length == 0 ? DefaultCapacity : _items.Length * 2;
            var newItems = new IndexedItem[newCapacity];
            Array.Copy(_items, newItems, _items.Length);
            _items = newItems;
        }

        var index = Count++;
        _items[index] = new IndexedItem(item, ++_sequenceCounter);
        Percolate(index);
    }

    /// <summary>
    /// Removes the specified item from the queue.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>True if the item was found and removed; otherwise, false.</returns>
    public bool Remove(T item)
    {
        for (var i = 0; i < Count; ++i)
        {
            if (EqualityComparer<T>.Default.Equals(_items[i].Value, item))
            {
                RemoveAt(i, single: false);
                return true;
            }
        }

        return false;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Enqueues multiple items efficiently using span-based allocation-free API.
    /// </summary>
    /// <param name="items">The items to enqueue.</param>
    public void EnqueueRange(ReadOnlySpan<T> items)
    {
        if (items.IsEmpty)
        {
            return;
        }

        // Ensure capacity for all items
        var requiredCapacity = Count + items.Length;
        if (requiredCapacity > _items.Length)
        {
            var newCapacity = Math.Max(_items.Length == 0 ? DefaultCapacity : _items.Length * 2, requiredCapacity);
            var newItems = new IndexedItem[newCapacity];
            Array.Copy(_items, newItems, Count);
            _items = newItems;
        }

        // Add all items, percolating each one
        foreach (var item in items)
        {
            var index = Count++;
            _items[index] = new IndexedItem(item, ++_sequenceCounter);
            Percolate(index);
        }
    }

    /// <summary>
    /// Dequeues multiple items into a span buffer.
    /// </summary>
    /// <param name="destination">The destination span to fill with dequeued items.</param>
    /// <returns>The number of items actually dequeued (may be less than destination.Length if queue has fewer items).</returns>
    public int DequeueRange(Span<T> destination)
    {
        var count = Math.Min(destination.Length, Count);
        for (var i = 0; i < count; i++)
        {
            destination[i] = Dequeue();
        }

        return count;
    }

    /// <summary>
    /// Tries to peek at the next item without allocating or throwing an exception.
    /// </summary>
    /// <param name="item">The next item if available.</param>
    /// <returns>True if an item was available; false if queue is empty.</returns>
    public bool TryPeek(out T item)
    {
        if (Count == 0)
        {
            item = default!;
            return false;
        }

        item = _items[0].Value;
        return true;
    }

    /// <summary>
    /// Tries to dequeue the next item without allocating or throwing an exception.
    /// </summary>
    /// <param name="item">The dequeued item if available.</param>
    /// <returns>True if an item was dequeued; false if queue is empty.</returns>
    public bool TryDequeue(out T item)
    {
        if (Count == 0)
        {
            item = default!;
            return false;
        }

        item = Dequeue();
        return true;
    }
#endif

    /// <summary>
    /// Verifies that the heap property is maintained for all items in the queue.
    /// This method is primarily used for testing and validation.
    /// </summary>
    /// <returns>True if the heap property is satisfied; otherwise false.</returns>
    internal bool VerifyHeapProperty()
    {
        // Verify quaternary heap property: each parent has higher priority than all 4 children
        for (var i = 0; i < Count; i++)
        {
            for (var childOffset = 1; childOffset <= Arity; childOffset++)
            {
                var child = (Arity * i) + childOffset;
                if (child < Count && !IsHigherPriority(i, child))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether the left item has higher priority than the right item.
    /// </summary>
    /// <param name="left">The left index.</param>
    /// <param name="right">The right index.</param>
    /// <returns>True if the left item should be dequeued before the right item.</returns>
#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private bool IsHigherPriority(int left, int right) => _items[left].CompareTo(_items[right]) < 0;

    /// <summary>
    /// Percolates (bubbles up) an item to maintain heap property after insertion.
    /// </summary>
    /// <param name="index">The index of the item to percolate.</param>
    private void Percolate(int index)
    {
        if (index >= Count || index < 0)
        {
            return;
        }

        // Quaternary heap: parent = (index - 1) / 4
        var parent = (index - 1) / Arity;
        if (parent < 0 || parent == index)
        {
            return;
        }

        if (IsHigherPriority(index, parent))
        {
            (_items[parent], _items[index]) = (_items[index], _items[parent]);
            Percolate(parent);
        }
    }

    /// <summary>
    /// Heapifies (sinks down) an item to maintain heap property after removal.
    /// </summary>
    /// <param name="index">The index of the item to heapify.</param>
    private void Heapify(int index)
    {
        if (index >= Count || index < 0)
        {
            return;
        }

        // Quaternary heap: children are at 4*index + 1, 4*index + 2, 4*index + 3, 4*index + 4
        var first = index;

        for (var i = 1; i <= Arity; i++)
        {
            var child = (Arity * index) + i;
            if (child < Count && IsHigherPriority(child, first))
            {
                first = child;
            }
        }

        if (first != index)
        {
            (_items[first], _items[index]) = (_items[index], _items[first]);
            Heapify(first);
        }
    }

    /// <summary>
    /// Removes the item at the specified index and rebalances the heap.
    /// </summary>
    /// <param name="index">The index of the item to remove.</param>
    /// <param name="single">True if this is a single removal operation; false if part of a batch.</param>
    private void RemoveAt(int index, bool single)
    {
        _items[index] = _items[--Count];
        _items[Count] = default;

        // Only rebalance if we didn't remove the last item
        // The replacement item might need to move up or down to restore heap property
        if (index < Count)
        {
            Percolate(index);  // Try moving up if it has higher priority than parent
            Heapify(index);    // Try moving down if it has lower priority than children
        }

        // Shrink array if utilization drops below 25% and either single removal or below default capacity
        if (Count < _items.Length / 4 && (single || Count < DefaultCapacity))
        {
            var newCapacity = _items.Length / 2;
            var newItems = new IndexedItem[newCapacity];
            Array.Copy(_items, 0, newItems, 0, Count);
            _items = newItems;
        }
    }

    /// <summary>
    /// Internal structure that wraps a value with a sequence ID for FIFO tie-breaking.
    /// </summary>
    /// <param name="Value">The value stored in this indexed item.</param>
    /// <param name="Id">The insertion sequence ID for FIFO ordering among equal priorities.</param>
    private readonly record struct IndexedItem(T Value, long Id) : IComparable<IndexedItem>
    {
        /// <summary>
        /// Compares this item to another for priority ordering.
        /// First compares by value priority, then by sequence ID for FIFO ordering.
        /// </summary>
        /// <param name="other">The other item to compare against.</param>
        /// <returns>A value indicating the relative order.</returns>
#if NET8_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public int CompareTo(IndexedItem other)
        {
            var c = Value.CompareTo(other.Value);
            if (c == 0)
            {
                c = Id.CompareTo(other.Id);
            }

            return c;
        }
    }
}
