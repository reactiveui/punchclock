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
    /// <para>
    /// This method is used after insertion to restore the quaternary heap property.
    /// Quaternary heap: parent = (index - 1) / 4.
    /// Time complexity: O(log₄ n) where n is the count of items in the heap.
    /// Implementation follows dotnet/runtime's iterative hole-based approach for better performance.
    /// </para>
    /// <para>
    /// <strong>JIT vs AOT Strategy:</strong> This method uses different code shapes for different runtimes.
    /// Modern .NET (8.0+) with tiered PGO benefits from helper methods and natural optimization,
    /// while AOT and legacy .NET Framework benefit from direct CompareTo calls and aggressive upfront optimization.
    /// </para>
    /// </remarks>
#if NATIVEAOT
    // AOT path: Direct CompareTo + AggressiveOptimization for upfront compilation
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#elif NETFRAMEWORK
    // .NET Framework: Direct CompareTo (AggressiveOptimization not available before .NET Core 3.0)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
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

#if NATIVEAOT || NETFRAMEWORK
            // AOT + .NET Framework: Direct CompareTo call to reduce call layers and improve inlining
            if (item.CompareTo(items[parent]) >= 0)
#else
            // Modern .NET: Helper method enables better inlining decisions with tiered PGO
            if (!IsHigherPriority(item, items[parent]))
#endif
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
    /// <para>
    /// This method is used after removal to restore the quaternary heap property.
    /// Quaternary heap: children are at 4*index + 1, 4*index + 2, 4*index + 3, 4*index + 4.
    /// Time complexity: O(log₄ n) where n is the count of items in the heap.
    /// Implementation follows dotnet/runtime's iterative hole-based approach for better performance.
    /// </para>
    /// <para>
    /// <strong>JIT vs AOT Strategy:</strong> This method uses different code shapes for different runtimes.
    /// Modern .NET (8.0+) with tiered PGO uses a loop with helper methods (compact, lets JIT optimize naturally),
    /// while AOT and legacy .NET Framework use unrolled comparisons with direct CompareTo calls for better upfront optimization.
    /// </para>
    /// </remarks>
#if NATIVEAOT
    // AOT path: Unrolled comparisons + Direct CompareTo + AggressiveOptimization
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#elif NETFRAMEWORK
    // .NET Framework: Unrolled + Direct CompareTo (AggressiveOptimization not available before .NET Core 3.0)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
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

#if NATIVEAOT || NETFRAMEWORK
            // AOT + .NET Framework path: Unroll the 4-child comparisons for better code generation
            // Direct CompareTo calls reduce call layers and improve inlining
            var highestPriorityChild = firstChild;

            var child2 = firstChild + 1;
            if (child2 < count && items[child2].CompareTo(items[highestPriorityChild]) < 0)
            {
                highestPriorityChild = child2;
            }

            var child3 = firstChild + 2;
            if (child3 < count && items[child3].CompareTo(items[highestPriorityChild]) < 0)
            {
                highestPriorityChild = child3;
            }

            var child4 = firstChild + 3;
            if (child4 < count && items[child4].CompareTo(items[highestPriorityChild]) < 0)
            {
                highestPriorityChild = child4;
            }

            // If the item has higher priority than the best child, we've found the right spot
            if (items[highestPriorityChild].CompareTo(item) >= 0)
            {
                break;
            }
#else
            // Modern .NET path: Use loop with helper method for compact code that JIT can optimize well
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
#endif

            // Move the highest-priority child up into the hole
            items[currentIndex] = items[highestPriorityChild];
            currentIndex = highestPriorityChild;
        }

        // Place the item in its final position
        items[currentIndex] = item;
    }

    /// <summary>
    /// Verifies that the quaternary heap property holds for the entire heap structure.
    /// For each parent node, verifies it has higher priority than all four of its children.
    /// </summary>
    /// <typeparam name="T">The type of items in the heap, must be comparable.</typeparam>
    /// <param name="items">The array representing the heap structure.</param>
    /// <param name="count">The current number of valid items in the heap.</param>
    /// <returns>True if the quaternary heap property is satisfied for all nodes; otherwise false.</returns>
    /// <remarks>
    /// <para>
    /// This method is primarily used for testing and validation purposes.
    /// </para>
    /// <para>
    /// Optimization: Only iterates through parent nodes (index &lt;= (count-2)/4).
    /// Leaf nodes have no children to validate, so checking them is unnecessary.
    /// For a 1000-item heap, this reduces checks from 1000 to ~250 nodes (75% reduction).
    /// </para>
    /// <para>
    /// Time complexity: O(n) where n is the count of items in the heap, but with a smaller constant factor.
    /// </para>
    /// </remarks>
    internal static bool VerifyHeapProperty<T>(T[] items, int count)
        where T : IComparable<T>
    {
        if (count <= 1)
        {
            return true; // Empty or single-item heap is always valid
        }

        // Verify quaternary heap property: each parent has higher priority than all 4 children
        // Only iterate through parent nodes - leaf nodes have no children to check
        // Last parent is at index (count-2)/4 because parent of last item (count-1) is (count-2)/4
        var lastParentIndex = (count - 2) / Arity;

        for (var i = 0; i <= lastParentIndex; i++)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHigherPriority<T>(T left, T right)
        where T : IComparable<T>
        => left.CompareTo(right) < 0;
}
