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
/// Uses a quaternary (4-ary) heap structure for improved performance.
/// </summary>
internal static class PriorityQueueHelper
{
    /// <summary>
    /// Number of children per node in the quaternary heap.
    /// </summary>
    private const int Arity = 4;

    /// <summary>
    /// Percolates (bubbles up) an item at the given index to maintain quaternary heap property.
    /// The item is repeatedly swapped with its parent until the heap property is restored.
    /// </summary>
    /// <typeparam name="T">The type of items in the heap, must be comparable.</typeparam>
    /// <param name="items">The array representing the heap structure.</param>
    /// <param name="index">The zero-based index of the item to percolate upward.</param>
    /// <param name="count">The current number of valid items in the heap.</param>
    /// <remarks>
    /// This method is used after insertion to restore the quaternary heap property.
    /// Quaternary heap: parent = (index - 1) / 4.
    /// Time complexity: O(log₄ n) where n is the count of items in the heap.
    /// </remarks>
    internal static void Percolate<T>(T[] items, int index, int count)
        where T : IComparable<T>
    {
        if (index >= count || index < 0)
        {
            return;
        }

        // Quaternary heap: parent = (index - 1) / 4
        var parent = (index - 1) / Arity;
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
    /// Heapifies (sinks down) an item at the given index to maintain quaternary heap property.
    /// The item is repeatedly swapped with its highest-priority child until the heap property is restored.
    /// </summary>
    /// <typeparam name="T">The type of items in the heap, must be comparable.</typeparam>
    /// <param name="items">The array representing the heap structure.</param>
    /// <param name="index">The zero-based index of the item to heapify downward.</param>
    /// <param name="count">The current number of valid items in the heap.</param>
    /// <remarks>
    /// This method is used after removal to restore the quaternary heap property.
    /// Quaternary heap: children are at 4*index + 1, 4*index + 2, 4*index + 3, 4*index + 4.
    /// Time complexity: O(log₄ n) where n is the count of items in the heap.
    /// </remarks>
    internal static void Heapify<T>(T[] items, int index, int count)
        where T : IComparable<T>
    {
        if (index >= count || index < 0)
        {
            return;
        }

        // Quaternary heap: children are at 4*index + 1, 4*index + 2, 4*index + 3, 4*index + 4
        var first = index;

        for (var i = 1; i <= Arity; i++)
        {
            var child = (Arity * index) + i;
            if (child < count && IsHigherPriority(items, child, first))
            {
                first = child;
            }
        }

        if (first != index)
        {
            (items[first], items[index]) = (items[index], items[first]);
            Heapify(items, first, count);
        }
    }

    /// <summary>
    /// Verifies that the quaternary heap property holds for the entire heap structure.
    /// For each node, verifies it has higher priority than all four of its children.
    /// </summary>
    /// <typeparam name="T">The type of items in the heap, must be comparable.</typeparam>
    /// <param name="items">The array representing the heap structure.</param>
    /// <param name="count">The current number of valid items in the heap.</param>
    /// <returns>True if the quaternary heap property is satisfied for all nodes; otherwise false.</returns>
    /// <remarks>
    /// This method is primarily used for testing and validation purposes.
    /// Time complexity: O(n) where n is the count of items in the heap.
    /// </remarks>
    internal static bool VerifyHeapProperty<T>(T[] items, int count)
        where T : IComparable<T>
    {
        // Verify quaternary heap property: each parent has higher priority than all 4 children
        for (var i = 0; i < count; i++)
        {
            for (var childOffset = 1; childOffset <= Arity; childOffset++)
            {
                var child = (Arity * i) + childOffset;
                if (child < count && !IsHigherPriority(items, i, child))
                {
                    return false;
                }
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
