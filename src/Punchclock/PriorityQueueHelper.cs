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
    /// Uses an iterative "hole" approach: moves parents down until finding the correct position,
    /// then places the item once. This reduces memory writes compared to recursive swapping.
    /// </summary>
    /// <typeparam name="T">The type of items in the heap, must be comparable.</typeparam>
    /// <param name="items">The array representing the heap structure.</param>
    /// <param name="index">The zero-based index of the item to percolate upward.</param>
    /// <param name="count">The current number of valid items in the heap.</param>
    /// <remarks>
    /// This method is used after insertion to restore the quaternary heap property.
    /// Quaternary heap: parent = (index - 1) / 4.
    /// Time complexity: O(log₄ n) where n is the count of items in the heap.
    /// Implementation follows dotnet/runtime's iterative hole-based approach for better performance.
    /// </remarks>
    internal static void Percolate<T>(T[] items, int index, int count)
        where T : IComparable<T>
    {
        if (index >= count || index < 0)
        {
            return;
        }

        // Save the item we're percolating - this creates a "hole" at index
        var item = items[index];
        var currentIndex = index;

        // Quaternary heap: parent = (index - 1) / 4
        while (currentIndex > 0)
        {
            var parent = (currentIndex - 1) / Arity;

            // If parent has higher priority, we've found the right spot
            if (!IsHigherPriority(item, items[parent]))
            {
                break;
            }

            // Move parent down into the hole
            items[currentIndex] = items[parent];
            currentIndex = parent;
        }

        // Place the item in its final position
        items[currentIndex] = item;
    }

    /// <summary>
    /// Heapifies (sinks down) an item at the given index to maintain quaternary heap property.
    /// Uses an iterative "hole" approach: moves highest-priority children up until finding the correct position,
    /// then places the item once. This reduces memory writes compared to recursive swapping.
    /// </summary>
    /// <typeparam name="T">The type of items in the heap, must be comparable.</typeparam>
    /// <param name="items">The array representing the heap structure.</param>
    /// <param name="index">The zero-based index of the item to heapify downward.</param>
    /// <param name="count">The current number of valid items in the heap.</param>
    /// <remarks>
    /// This method is used after removal to restore the quaternary heap property.
    /// Quaternary heap: children are at 4*index + 1, 4*index + 2, 4*index + 3, 4*index + 4.
    /// Time complexity: O(log₄ n) where n is the count of items in the heap.
    /// Implementation follows dotnet/runtime's iterative hole-based approach for better performance.
    /// </remarks>
    internal static void Heapify<T>(T[] items, int index, int count)
        where T : IComparable<T>
    {
        if (index >= count || index < 0)
        {
            return;
        }

        // Save the item we're heapifying - this creates a "hole" at index
        var item = items[index];
        var currentIndex = index;

        // Quaternary heap: children are at 4*index + 1, 4*index + 2, 4*index + 3, 4*index + 4
        while (true)
        {
            var firstChild = (Arity * currentIndex) + 1;

            // If no children exist, we've found the right spot
            if (firstChild >= count)
            {
                break;
            }

            // Find the highest-priority child among the 4 children
            var highestPriorityChild = firstChild;
            var lastChild = Math.Min(firstChild + Arity, count);

            for (var child = firstChild + 1; child < lastChild; child++)
            {
                if (IsHigherPriority(items, child, highestPriorityChild))
                {
                    highestPriorityChild = child;
                }
            }

            // If the item has higher priority than the best child, we've found the right spot
            if (!IsHigherPriority(items[highestPriorityChild], item))
            {
                break;
            }

            // Move the highest-priority child up into the hole
            items[currentIndex] = items[highestPriorityChild];
            currentIndex = highestPriorityChild;
        }

        // Place the item in its final position
        items[currentIndex] = item;
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
    /// <para>
    /// In a max-heap, higher priority means a smaller comparison value (CompareTo returns negative).
    /// This method is aggressively inlined for performance on hot paths.
    /// </para>
    /// <para>
    /// This implementation assumes items have a total ordering (CompareTo never returns 0 for distinct items).
    /// In Punchclock, this is guaranteed by wrapping items in IndexedItem with FIFO sequence IDs.
    /// If reusing this helper for heaps without FIFO sequencing, use &lt;= 0 instead of &lt; 0 to allow equal priorities.
    /// </para>
    /// </remarks>
#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private static bool IsHigherPriority<T>(T[] items, int left, int right)
        where T : IComparable<T>
        => items[left].CompareTo(items[right]) < 0;

    /// <summary>
    /// Determines whether the left item has higher priority than the right item.
    /// </summary>
    /// <typeparam name="T">The type of items in the heap, must be comparable.</typeparam>
    /// <param name="left">The left item to compare.</param>
    /// <param name="right">The right item to compare.</param>
    /// <returns>True if the left item has higher priority (should be dequeued first); otherwise false.</returns>
    /// <remarks>
    /// This overload is used during iterative heap operations when comparing a saved item
    /// with array elements. Aggressively inlined for performance on hot paths.
    /// </remarks>
#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private static bool IsHigherPriority<T>(T left, T right)
        where T : IComparable<T>
        => left.CompareTo(right) < 0;
}
