// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Punchclock;

/// <summary>
/// A priority queue which will store items contained in order of the various priorities.
/// Items are stored in a binary heap structure where higher-priority items are dequeued first.
/// </summary>
/// <typeparam name="T">The type of item to store in the queue.</typeparam>
/// <remarks>
/// Based off Microsoft internal code.
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

        var parent = (index - 1) / 2;
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
    /// Heapifies the entire tree starting from the root.
    /// </summary>
    private void Heapify() => Heapify(0);

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

        var left = (2 * index) + 1;
        var right = (2 * index) + 2;
        var first = index;

        if (left < Count && IsHigherPriority(left, first))
        {
            first = left;
        }

        if (right < Count && IsHigherPriority(right, first))
        {
            first = right;
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
        Heapify();

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
