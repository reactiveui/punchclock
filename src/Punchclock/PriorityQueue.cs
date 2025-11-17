// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Punchclock;

/// <summary>
/// A priority queue which will store items contained in order of the various priorities.
/// </summary>
/// <typeparam name="T">The type of item to store in the queue.</typeparam>
/// <remarks>
/// Based off Microsoft internal code.
/// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.
/// This is https://github.com/mono/rx/blob/master/Rx/NET/Source/System.Reactive.Core/Reactive/Internal/PriorityQueue.cs originally.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="PriorityQueue{T}"/> class.
/// </remarks>
/// <param name="capacity">The starting capacity of the queue.</param>
internal class PriorityQueue<T>(int capacity)
    where T : IComparable<T>
{
    private const int DefaultCapacity = 16;

#if !NO_INTERLOCKED_64
    private static long _count = long.MinValue;
#else
    private static int _count = int.MinValue;
#endif
    private IndexedItem[] _items = new IndexedItem[capacity];

    /// <summary>
    /// Initializes a new instance of the <see cref="PriorityQueue{T}"/> class.
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
    /// Peeks at the next time available in the queue.
    /// </summary>
    /// <returns>The next item.</returns>
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
    public T Dequeue()
    {
        var result = Peek();
        RemoveAt(0, true);
        return result;
    }

    /// <summary>
    /// Removes up to the specified number of items and returns those items.
    /// </summary>
    /// <param name="count">The maximum number of items to remove from the queue.</param>
    /// <returns>The next items.</returns>
    public T[] DequeueSome(int count)
    {
        if (count == 0)
        {
            return [];
        }

        var ret = new T[count];
        count = Math.Min(count, Count);
        for (var i = 0; i < count; i++)
        {
            ret[i] = Peek();
            RemoveAt(0, false);
        }

        return ret;
    }

    /// <summary>
    /// Removes all the items currently contained within the queue and returns them.
    /// </summary>
    /// <returns>All the items from the queue.</returns>
    public T[] DequeueAll() => DequeueSome(Count);

    /// <summary>
    /// Adds a item in the correct location based on priority to the queue.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Enqueue(T item)
    {
        if (Count >= _items.Length)
        {
            var temp = _items;
            _items = new IndexedItem[_items.Length * 2];
            Array.Copy(temp, _items, temp.Length);
        }

        var index = Count++;
        _items[index] = new IndexedItem { Value = item, Id = Interlocked.Increment(ref _count) };
        Percolate(index);
    }

    /// <summary>
    /// Removes the specified item from the queue.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>If the remove was successful or not.</returns>
    public bool Remove(T item)
    {
        for (var i = 0; i < Count; ++i)
        {
            if (EqualityComparer<T>.Default.Equals(_items[i].Value, item))
            {
                RemoveAt(i, false);
                return true;
            }
        }

        return false;
    }

    private bool IsHigherPriority(int left, int right) => _items[left].CompareTo(_items[right]) < 0;

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

    private void Heapify() => Heapify(0);

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

    private void RemoveAt(int index, bool single)
    {
        _items[index] = _items[--Count];
        _items[Count] = default;
        Heapify();
        if (Count < _items.Length / 4 && (single || Count < DefaultCapacity))
        {
            var temp = _items;
            _items = new IndexedItem[_items.Length / 2];
            Array.Copy(temp, 0, _items, 0, Count);
        }
    }

    private struct IndexedItem : IComparable<IndexedItem>
    {
        public T Value;
#if !NO_INTERLOCKED_64
        public long Id;
#else
        public int Id;
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
