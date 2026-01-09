// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace Punchclock;

/// <summary>
/// Internal helper class providing static heap operations for priority queues.
/// Extracted for testability and potential reuse across different heap implementations.
/// </summary>
internal static class PriorityQueueHelper
{
    /// <summary>
    /// Percolates (bubbles up) an item at the given index to maintain max-heap property.
    /// The item is repeatedly swapped with its parent until the heap property is restored.
    /// </summary>
    /// <typeparam name="T">The type of items in the heap, must be comparable.</typeparam>
    /// <param name="items">The array representing the heap structure.</param>
    /// <param name="index">The zero-based index of the item to percolate upward.</param>
    /// <param name="count">The current number of valid items in the heap.</param>
    /// <remarks>
    /// This method is used after insertion to restore the max-heap property.
    /// Time complexity: O(log n) where n is the count of items in the heap.
    /// </remarks>
    internal static void Percolate<T>(T[] items, int index, int count)
        where T : IComparable<T>
    {
        if (index >= count || index < 0)
        {
            return;
        }

        var parent = (index - 1) / 2;
        if (parent < 0 || parent == index)
        {
            return;
        }

        if (IsHigherPriority(items, index, parent))
        {
            (items[parent], items[index]) = (items[index], items[parent]);
            Percolate(items, parent, count);
        }
    }

    /// <summary>
    /// Heapifies (sinks down) an item at the given index to maintain max-heap property.
    /// The item is repeatedly swapped with its highest-priority child until the heap property is restored.
    /// </summary>
    /// <typeparam name="T">The type of items in the heap, must be comparable.</typeparam>
    /// <param name="items">The array representing the heap structure.</param>
    /// <param name="index">The zero-based index of the item to heapify downward.</param>
    /// <param name="count">The current number of valid items in the heap.</param>
    /// <remarks>
    /// This method is used after removal to restore the max-heap property.
    /// Time complexity: O(log n) where n is the count of items in the heap.
    /// </remarks>
    internal static void Heapify<T>(T[] items, int index, int count)
        where T : IComparable<T>
    {
        if (index >= count || index < 0)
        {
            return;
        }

        var left = (2 * index) + 1;
        var right = (2 * index) + 2;
        var first = index;

        if (left < count && IsHigherPriority(items, left, first))
        {
            first = left;
        }

        if (right < count && IsHigherPriority(items, right, first))
        {
            first = right;
        }

        if (first != index)
        {
            (items[first], items[index]) = (items[index], items[first]);
            Heapify(items, first, count);
        }
    }

    /// <summary>
    /// Verifies that the max-heap property holds for the entire heap structure.
    /// For each node, verifies it has higher priority than both of its children.
    /// </summary>
    /// <typeparam name="T">The type of items in the heap, must be comparable.</typeparam>
    /// <param name="items">The array representing the heap structure.</param>
    /// <param name="count">The current number of valid items in the heap.</param>
    /// <returns>True if the max-heap property is satisfied for all nodes; otherwise false.</returns>
    /// <remarks>
    /// This method is primarily used for testing and validation purposes.
    /// Time complexity: O(n) where n is the count of items in the heap.
    /// </remarks>
    internal static bool VerifyHeapProperty<T>(T[] items, int count)
        where T : IComparable<T>
    {
        for (var i = 0; i < count; i++)
        {
            var left = (2 * i) + 1;
            var right = (2 * i) + 2;

            if (left < count && !IsHigherPriority(items, i, left))
            {
                return false;
            }

            if (right < count && !IsHigherPriority(items, i, right))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether the item at the left index has higher priority than the item at the right index.
    /// </summary>
    /// <typeparam name="T">The type of items in the heap, must be comparable.</typeparam>
    /// <param name="items">The array representing the heap structure.</param>
    /// <param name="left">The index of the left item to compare.</param>
    /// <param name="right">The index of the right item to compare.</param>
    /// <returns>True if the left item has higher priority (should be dequeued first); otherwise false.</returns>
    /// <remarks>
    /// In a max-heap, higher priority means a smaller comparison value (CompareTo returns negative).
    /// This method is aggressively inlined for performance on hot paths.
    /// </remarks>
#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private static bool IsHigherPriority<T>(T[] items, int left, int right)
        where T : IComparable<T>
        => items[left].CompareTo(items[right]) < 0;
}
