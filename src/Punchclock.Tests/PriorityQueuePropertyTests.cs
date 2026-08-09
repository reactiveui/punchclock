// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using ReactiveUI.Primitives.Core;

namespace Punchclock.Tests;

/// <summary>
/// Property-based tests for <see cref="PriorityQueue{T}"/> that run multiple times with generated data
/// to verify invariants hold under various conditions.
/// </summary>
public class PriorityQueuePropertyTests
{
    /// <summary>Represents the enqueue operation kind.</summary>
    private const int EnqueueOperationKind = 0;

    /// <summary>Represents the dequeue operation kind.</summary>
    private const int DequeueOperationKind = 1;

    /// <summary>Represents the remove operation kind.</summary>
    private const int RemoveOperationKind = 2;

    /// <summary>Represents the total number of operation kinds.</summary>
    private const int OperationKindCount = 3;

    /// <summary>Represents the lower bound for generated operation counts.</summary>
    private const int MinimumOperationCount = 10;

    /// <summary>Represents the upper bound for generated operation counts.</summary>
    private const int MaximumOperationCountExclusive = 50;

    /// <summary>Represents the upper bound for generated FIFO item counts.</summary>
    private const int MaximumFifoItemCountExclusive = 30;

    /// <summary>Represents the number of repetitions for property tests.</summary>
    private const int PropertyTestIterationCount = 100;

    /// <summary>Represents the exclusive upper bound for generated priorities.</summary>
    private const int PriorityUpperBoundExclusive = 100;

    /// <summary>
    /// Property test: Heap invariant must hold after any sequence of operations.
    /// Runs 100 times with different random operation sequences.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Repeat(PropertyTestIterationCount)]
    public async Task PropertyTest_HeapInvariantAlwaysHolds()
    {
        PriorityQueue<TestItem> queue = new();
        var operations = RandomNumberGenerator.GetInt32(MinimumOperationCount, MaximumOperationCountExclusive);
        List<TestItem> items = new();

        for (var i = 0; i < operations; i++)
        {
            switch (RandomNumberGenerator.GetInt32(OperationKindCount))
            {
                case EnqueueOperationKind:
                    {
                        var item = new TestItem(RandomNumberGenerator.GetInt32(PriorityUpperBoundExclusive));
                        queue.Enqueue(item);
                        items.Add(item);
                        break;
                    }

                case DequeueOperationKind when queue.Count > 0:
                    {
                        var dequeuedItem = queue.Dequeue();
                        _ = items.Remove(dequeuedItem);
                        break;
                    }

                case RemoveOperationKind when items.Count > 0:
                    {
                        var toRemove = items[RandomNumberGenerator.GetInt32(items.Count)];
                        if (queue.Remove(toRemove))
                        {
                            _ = items.Remove(toRemove);
                        }

                        break;
                    }
            }

            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>
    /// Property test: Items must be dequeued in non-decreasing numeric priority order (lower values first).
    /// Runs 100 times with different random data sets.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Repeat(PropertyTestIterationCount)]
    public async Task PropertyTest_DequeueOrderIsNonIncreasing()
    {
        PriorityQueue<TestItem> queue = new();
        var count = RandomNumberGenerator.GetInt32(MinimumOperationCount, MaximumOperationCountExclusive);

        for (var i = 0; i < count; i++)
        {
            queue.Enqueue(new(RandomNumberGenerator.GetInt32(PriorityUpperBoundExclusive)));
        }

        TestItem? previous = null;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (previous is not null)
            {
                await Assert.That(previous.Priority <= current.Priority).IsTrue();
            }

            previous = current;
        }
    }

    /// <summary>
    /// Property test: Items with equal priority must maintain FIFO order.
    /// Runs 100 times to verify sequence counter tie-breaking.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Repeat(PropertyTestIterationCount)]
    public async Task PropertyTest_FIFO_OrderMaintained()
    {
        PriorityQueue<TestItem> queue = new();
        var itemCount = RandomNumberGenerator.GetInt32(MinimumOperationCount, MaximumFifoItemCountExclusive);
        var priority = RandomNumberGenerator.GetInt32(PriorityUpperBoundExclusive);

        for (var i = 0; i < itemCount; i++)
        {
            var item = new TestItem(priority, Id: i);
            queue.Enqueue(item);
        }

        using (Assert.Multiple())
        {
            for (var i = 0; i < itemCount; i++)
            {
                var item = queue.Dequeue();
                await Assert.That(item.Id).IsEqualTo(i);
            }
        }
    }

    /// <summary>Test item for property-based tests with priority and optional ID for FIFO testing.</summary>
    /// <param name="Priority">The priority value for this item (lower values have higher priority).</param>
    /// <param name="Id">Optional ID for tracking FIFO order in equal-priority scenarios.</param>
    private sealed record TestItem(int Priority, int Id = 0) : IComparable<TestItem>
    {
        /// <summary>Compares this instance with another TestItem based on Priority.</summary>
        /// <param name="other">The other TestItem to compare with.</param>
        /// <returns>A value indicating the relative order of the items.</returns>
        public int CompareTo(TestItem? other) => other is null ? -1 : Priority.CompareTo(other.Priority);
    }
}
