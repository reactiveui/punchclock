// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Punchclock.Tests;

/// <summary>
/// Property-based tests for PriorityQueue that run multiple times with random data
/// to verify invariants hold under various conditions.
/// </summary>
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Random is used for test data generation, not for security purposes")]
public class PriorityQueuePropertyTests
{
    private const int Two = 2;

    private const int Three = 3;

    private const int OneHundred = 100;

    /// <summary>
    /// Property test: Heap invariant must hold after any sequence of operations.
    /// Runs 100 times with different random operation sequences.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Repeat(OneHundred)]
    public async Task PropertyTest_HeapInvariantAlwaysHolds()
    {
        var queue = new ReactiveUI.Primitives.Core.PriorityQueue<TestItem>();
        var operations = Random.Shared.Next(10, 50);
        var items = new List<TestItem>();

        for (var i = 0; i < operations; i++)
        {
            switch (Random.Shared.Next(Three))
            {
                case 0: // Enqueue
                    {
                        var item = new TestItem(Random.Shared.Next(OneHundred));
                        queue.Enqueue(item);
                        items.Add(item);
                        break;
                    }

                case 1 when queue.Count > 0: // Dequeue
                    {
                        queue.Dequeue();
                        break;
                    }

                case Two when items.Count > 0: // Remove
                    {
                        var toRemove = items[Random.Shared.Next(items.Count)];
                        if (queue.Remove(toRemove))
                        {
                            items.Remove(toRemove);
                        }

                        break;
                    }
            }

            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>
    /// Property test: Items must be dequeued in non-increasing priority order (higher priority first).
    /// Runs 100 times with different random data sets.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Repeat(OneHundred)]
    public async Task PropertyTest_DequeueOrderIsNonIncreasing()
    {
        var queue = new ReactiveUI.Primitives.Core.PriorityQueue<TestItem>();
        var count = Random.Shared.Next(10, 50);

        // Enqueue random items
        for (var i = 0; i < count; i++)
        {
            queue.Enqueue(new TestItem(Random.Shared.Next(OneHundred)));
        }

        // Dequeue all items and verify priority order
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
    [Repeat(OneHundred)]
    public async Task PropertyTest_FIFO_OrderMaintained()
    {
        var queue = new ReactiveUI.Primitives.Core.PriorityQueue<TestItem>();
        var itemCount = Random.Shared.Next(10, 30);
        var priority = Random.Shared.Next(OneHundred); // All items have same priority

        // Enqueue items with sequential IDs
        var items = Enumerable.Range(0, itemCount)
            .Select(i => new TestItem(priority, Id: i))
            .ToArray();

        foreach (var item in items)
        {
            queue.Enqueue(item);
        }

        // Dequeue all items and verify FIFO order
        var dequeued = new List<TestItem>();
        while (queue.Count > 0)
        {
            dequeued.Add(queue.Dequeue());
        }

        await Assert.That(dequeued.Count).IsEqualTo(itemCount);

        // Verify FIFO order: items should be dequeued in insertion order
        using (Assert.Multiple())
        {
            for (var i = 0; i < dequeued.Count; i++)
            {
                await Assert.That(dequeued[i].Id).IsEqualTo(i);
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
        public int CompareTo(TestItem? other)
        {
            if (other is null)
            {
                return -1;
            }

            return Priority.CompareTo(other.Priority);
        }
    }
}
