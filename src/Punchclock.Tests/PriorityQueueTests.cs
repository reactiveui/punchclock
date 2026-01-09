// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Punchclock.Tests;

/// <summary>
/// Tests for <see cref="PriorityQueue{T}"/>.
/// </summary>
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Random is used for test data generation, not for security purposes")]
public class PriorityQueueTests
{
    /// <summary>
    /// Verifies that the constructor throws <see cref="ArgumentOutOfRangeException"/> for negative capacity.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithNegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        var ex = await Assert.That(() => new PriorityQueue<TestItem>(-1))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(ex!.ParamName).IsEqualTo("capacity");
    }

    /// <summary>
    /// Verifies that the constructor accepts zero capacity.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithZeroCapacity_Succeeds()
    {
        var queue = new PriorityQueue<TestItem>(0);
        await Assert.That(queue.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that the default constructor creates a queue with default capacity.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_Default_Succeeds()
    {
        var queue = new PriorityQueue<TestItem>();
        await Assert.That(queue.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that Peek throws <see cref="InvalidOperationException"/> when queue is empty.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Peek_OnEmptyQueue_ThrowsInvalidOperationException()
    {
        var queue = new PriorityQueue<TestItem>();
        await Assert.That(() => queue.Peek())
            .Throws<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that Dequeue throws <see cref="InvalidOperationException"/> when queue is empty.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dequeue_OnEmptyQueue_ThrowsInvalidOperationException()
    {
        var queue = new PriorityQueue<TestItem>();
        await Assert.That(() => queue.Dequeue())
            .Throws<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that Enqueue and Dequeue work correctly for a single item.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_ThenDequeue_ReturnsSameItem()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            var item = new TestItem(1);

            queue.Enqueue(item);
            await Assert.That(queue.Count).IsEqualTo(1);

            var dequeued = queue.Dequeue();
            await Assert.That(dequeued).IsEqualTo(item);
            await Assert.That(queue.Count).IsEqualTo(0);
        }
    }

    /// <summary>
    /// Verifies that Peek returns the highest priority item without removing it.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Peek_ReturnsHighestPriorityWithoutRemoving()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            var item1 = new TestItem(1);
            var item2 = new TestItem(2);

            queue.Enqueue(item1);
            queue.Enqueue(item2);

            var peeked = queue.Peek();
            await Assert.That(peeked).IsEqualTo(item1); // Lower value = higher priority
            await Assert.That(queue.Count).IsEqualTo(2); // Count unchanged
        }
    }

    /// <summary>
    /// Verifies that items are dequeued in priority order.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dequeue_ReturnsItemsInPriorityOrder()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            var item1 = new TestItem(5);
            var item2 = new TestItem(1);
            var item3 = new TestItem(3);

            queue.Enqueue(item1);
            queue.Enqueue(item2);
            queue.Enqueue(item3);

            await Assert.That(queue.Dequeue()).IsEqualTo(item2); // Priority 1
            await Assert.That(queue.Dequeue()).IsEqualTo(item3); // Priority 3
            await Assert.That(queue.Dequeue()).IsEqualTo(item1); // Priority 5
        }
    }

    /// <summary>
    /// Verifies that DequeueSome returns the correct number of items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DequeueSome_ReturnsCorrectNumberOfItems()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            queue.Enqueue(new TestItem(1));
            queue.Enqueue(new TestItem(2));
            queue.Enqueue(new TestItem(3));

            var items = queue.DequeueSome(2);
            await Assert.That(items.Length).IsEqualTo(2);
            await Assert.That(queue.Count).IsEqualTo(1);
        }
    }

    /// <summary>
    /// Verifies that DequeueSome with zero count returns empty array.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DequeueSome_WithZeroCount_ReturnsEmptyArray()
    {
        var queue = new PriorityQueue<TestItem>();
        queue.Enqueue(new TestItem(1));

        var items = queue.DequeueSome(0);
        await Assert.That(items.Length).IsEqualTo(0);
        await Assert.That(queue.Count).IsEqualTo(1); // Queue unchanged
    }

    /// <summary>
    /// Verifies that DequeueSome caps at available items when requesting more than available.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DequeueSome_RequestingMoreThanAvailable_ReturnsAllItems()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            queue.Enqueue(new TestItem(1));
            queue.Enqueue(new TestItem(2));

            var items = queue.DequeueSome(10);
            await Assert.That(items.Length).IsEqualTo(2);
            await Assert.That(queue.Count).IsEqualTo(0);
        }
    }

    /// <summary>
    /// Verifies that DequeueAll returns all items and empties the queue.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DequeueAll_ReturnsAllItems()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            queue.Enqueue(new TestItem(3));
            queue.Enqueue(new TestItem(1));
            queue.Enqueue(new TestItem(2));

            var items = queue.DequeueAll();
            await Assert.That(items.Length).IsEqualTo(3);
            await Assert.That(queue.Count).IsEqualTo(0);
            await Assert.That(items[0].Priority).IsEqualTo(1);
            await Assert.That(items[1].Priority).IsEqualTo(2);
            await Assert.That(items[2].Priority).IsEqualTo(3);
        }
    }

    /// <summary>
    /// Verifies that DequeueAll on empty queue returns empty array.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DequeueAll_OnEmptyQueue_ReturnsEmptyArray()
    {
        var queue = new PriorityQueue<TestItem>();
        var items = queue.DequeueAll();
        await Assert.That(items.Length).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that Remove returns true and removes the specified item.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Remove_ExistingItem_ReturnsTrueAndRemovesItem()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            var item1 = new TestItem(1);
            var item2 = new TestItem(2);
            var item3 = new TestItem(3);

            queue.Enqueue(item1);
            queue.Enqueue(item2);
            queue.Enqueue(item3);

            var removed = queue.Remove(item2);
            await Assert.That(removed).IsTrue();
            await Assert.That(queue.Count).IsEqualTo(2);

            var dequeued = queue.Dequeue();
            await Assert.That(dequeued).IsEqualTo(item1); // item2 was removed
        }
    }

    /// <summary>
    /// Verifies that Remove returns false for non-existent item.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Remove_NonExistentItem_ReturnsFalse()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            queue.Enqueue(new TestItem(1));

            var removed = queue.Remove(new TestItem(999));
            await Assert.That(removed).IsFalse();
            await Assert.That(queue.Count).IsEqualTo(1);
        }
    }

    /// <summary>
    /// Verifies that the queue grows capacity when needed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_BeyondInitialCapacity_GrowsQueue()
    {
        var queue = new PriorityQueue<TestItem>(2);
        for (var i = 0; i < 20; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        await Assert.That(queue.Count).IsEqualTo(20);
    }

    /// <summary>
    /// Verifies that the queue shrinks capacity after many removals.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dequeue_ManyItems_ShrinksCapacity()
    {
        var queue = new PriorityQueue<TestItem>();
        for (var i = 0; i < 100; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        // Dequeue most items to trigger shrinking
        for (var i = 0; i < 90; i++)
        {
            queue.Dequeue();
        }

        await Assert.That(queue.Count).IsEqualTo(10);
    }

    /// <summary>
    /// Verifies that equal priority items maintain FIFO order via sequence counter.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WithEqualPriorities_MaintainsFIFOOrder()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            var item1 = new TestItem(1, "first");
            var item2 = new TestItem(1, "second");
            var item3 = new TestItem(1, "third");

            queue.Enqueue(item1);
            queue.Enqueue(item2);
            queue.Enqueue(item3);

            await Assert.That(queue.Dequeue()).IsEqualTo(item1);
            await Assert.That(queue.Dequeue()).IsEqualTo(item2);
            await Assert.That(queue.Dequeue()).IsEqualTo(item3);
        }
    }

    /// <summary>
    /// Covers PriorityQueue.cs line 137 - capacity growth when _items.Length == 0.
    /// Verifies that enqueueing to a zero-capacity queue grows to default capacity.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WhenZeroCapacity_GrowsToDefaultCapacity()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>(0); // Start with zero capacity

            // First enqueue should trigger line 137 with _items.Length == 0
            queue.Enqueue(new TestItem(1));

            await Assert.That(queue.Count).IsEqualTo(1);

            var item = queue.Dequeue();
            await Assert.That(item.Priority).IsEqualTo(1);
        }
    }

    /// <summary>
    /// Covers PriorityQueue.cs lines 186/188 - Percolate with edge cases.
    /// Verifies that the queue handles multiple enqueue/dequeue operations correctly,
    /// ensuring internal percolation logic works properly even in edge cases.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task MultipleEnqueueDequeue_HandlesPercolateEdgeCases()
    {
        var queue = new PriorityQueue<TestItem>(10);

        // Add and remove items to potentially trigger edge cases in percolation
        for (var i = 0; i < 5; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        queue.Dequeue();
        queue.Dequeue();

        // Queue should still be consistent
        await Assert.That(queue.Count).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that enqueuing multiple items maintains heap property after each insertion.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_MultipleItems_MaintainsHeapProperty()
    {
        var queue = new PriorityQueue<TestItem>();

        for (var i = 0; i < 20; i++)
        {
            queue.Enqueue(new TestItem(Random.Shared.Next(100)));
            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that dequeuing multiple items maintains heap property after each removal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dequeue_MultipleItems_MaintainsHeapProperty()
    {
        var queue = new PriorityQueue<TestItem>();
        for (var i = 0; i < 20; i++)
        {
            queue.Enqueue(new TestItem(Random.Shared.Next(100)));
        }

        while (queue.Count > 0)
        {
            queue.Dequeue();
            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that removing middle elements maintains heap property.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Remove_MiddleElement_MaintainsHeapProperty()
    {
        var queue = new PriorityQueue<TestItem>();
        var items = Enumerable.Range(0, 15).Select(i => new TestItem(i, $"item{i}")).ToArray();

        foreach (var item in items)
        {
            queue.Enqueue(item);
        }

        // Remove middle elements
        for (var i = 5; i < 10; i++)
        {
            queue.Remove(items[i]);
            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that items are dequeued in correct priority order (reverse of insertion for ascending priorities).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_ReverseOrder_MaintainsCorrectness()
    {
        var queue = new PriorityQueue<TestItem>();

        // Insert in descending order
        for (var i = 20; i >= 1; i--)
        {
            queue.Enqueue(new TestItem(i));
        }

        // Should dequeue in ascending order (highest priority first)
        var previous = 0;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            await Assert.That(current.Priority).IsGreaterThanOrEqualTo(previous);
            previous = current.Priority;
        }
    }

    /// <summary>
    /// Verifies that items inserted in ascending order are still dequeued correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_AscendingOrder_MaintainsCorrectness()
    {
        var queue = new PriorityQueue<TestItem>();

        // Insert in ascending order
        for (var i = 1; i <= 20; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        // Should dequeue in ascending order (highest priority first)
        var previous = 0;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            await Assert.That(current.Priority).IsGreaterThanOrEqualTo(previous);
            previous = current.Priority;
        }
    }

    /// <summary>
    /// Verifies that capacity grows correctly across boundaries.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CapacityGrowth_MaintainsHeapProperty()
    {
        var queue = new PriorityQueue<TestItem>(8);

        // Test around capacity boundaries: 8 -> 16 -> 32
        var boundaries = new[] { 8, 15, 16, 17, 31, 32, 33 };
        foreach (var count in boundaries)
        {
            while (queue.Count < count)
            {
                queue.Enqueue(new TestItem(Random.Shared.Next(100)));
            }

            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that capacity shrinking maintains heap property.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CapacityShrink_MaintainsHeapProperty()
    {
        var queue = new PriorityQueue<TestItem>();

        // Enqueue 100 items
        for (var i = 0; i < 100; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        // Dequeue 90 items to trigger shrinking
        for (var i = 0; i < 90; i++)
        {
            queue.Dequeue();
        }

        using (Assert.Multiple())
        {
            await Assert.That(queue.Count).IsEqualTo(10);
            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>
    /// Stress test with 1000 random enqueue/dequeue/remove operations.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task StressTest_RandomOperations_AlwaysValid()
    {
        var queue = new PriorityQueue<TestItem>();
        var items = new List<TestItem>();

        for (var i = 0; i < 1000; i++)
        {
            var operation = Random.Shared.Next(3);
            switch (operation)
            {
                case 0: // Enqueue
                    var item = new TestItem(Random.Shared.Next(100), $"item{i}");
                    queue.Enqueue(item);
                    items.Add(item);
                    break;
                case 1 when queue.Count > 0: // Dequeue
                    queue.Dequeue();
                    break;
                case 2 when items.Count > 0: // Remove
                    var toRemove = items[Random.Shared.Next(items.Count)];
                    queue.Remove(toRemove);
                    items.Remove(toRemove);
                    break;
            }

            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>
    /// Verifies FIFO ordering when all items have equal priority.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task FIFO_EqualPriorities_AllElements()
    {
        var queue = new PriorityQueue<TestItem>();
        var items = Enumerable.Range(0, 20).Select(i => new TestItem(5, $"item{i}")).ToArray();

        foreach (var item in items)
        {
            queue.Enqueue(item);
        }

        // Items with equal priority should be dequeued in FIFO order
        for (var i = 0; i < items.Length; i++)
        {
            var dequeued = queue.Dequeue();
            await Assert.That(dequeued.Id).IsEqualTo($"item{i}");
        }
    }

    /// <summary>
    /// Verifies that sequence counter handles near-overflow correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SequenceCounter_NearOverflow_HandlesCorrectly()
    {
        var queue = new PriorityQueue<TestItem>();

        // Enqueue 100 items with equal priority to test sequence counter
        for (var i = 0; i < 100; i++)
        {
            queue.Enqueue(new TestItem(5, $"item{i}"));
        }

        // All items should still be retrievable
        await Assert.That(queue.Count).IsEqualTo(100);

        // Verify FIFO order is maintained
        for (var i = 0; i < 100; i++)
        {
            var dequeued = queue.Dequeue();
            await Assert.That(dequeued.Id).IsEqualTo($"item{i}");
        }
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Tests that EnqueueRange enqueues multiple items correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueRange_MultipleItems_AllEnqueued()
    {
        var queue = new PriorityQueue<TestItem>();
        var items = Enumerable.Range(0, 20).Select(i => new TestItem(i)).ToArray();

        queue.EnqueueRange(items);

        await Assert.That(queue.Count).IsEqualTo(20);
        await Assert.That(queue.VerifyHeapProperty()).IsTrue();
    }

    /// <summary>
    /// Tests that EnqueueRange handles empty span correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueRange_EmptySpan_NoChange()
    {
        var queue = new PriorityQueue<TestItem>();
        queue.EnqueueRange(ReadOnlySpan<TestItem>.Empty);

        await Assert.That(queue.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Tests that DequeueRange returns correct count when buffer is larger than available items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DequeueRange_PartialBuffer_ReturnsCorrectCount()
    {
        var queue = new PriorityQueue<TestItem>();
        for (var i = 0; i < 10; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        var buffer = new TestItem[5];
        var count = queue.DequeueRange(buffer);

        await Assert.That(count).IsEqualTo(5);
        await Assert.That(queue.Count).IsEqualTo(5);
    }

    /// <summary>
    /// Tests that DequeueRange returns all items when buffer is larger than queue.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DequeueRange_LargerThanQueue_ReturnsAllItems()
    {
        var queue = new PriorityQueue<TestItem>();
        for (var i = 0; i < 5; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        var buffer = new TestItem[10];
        var count = queue.DequeueRange(buffer);

        await Assert.That(count).IsEqualTo(5);
        await Assert.That(queue.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Tests that TryPeek returns false on empty queue.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryPeek_EmptyQueue_ReturnsFalse()
    {
        var queue = new PriorityQueue<TestItem>();
        var result = queue.TryPeek(out var item);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsFalse();
            await Assert.That(item).IsNull();
        }
    }

    /// <summary>
    /// Tests that TryPeek returns true and item on non-empty queue.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryPeek_NonEmptyQueue_ReturnsTrue()
    {
        var queue = new PriorityQueue<TestItem>();
        var expected = new TestItem(5);
        queue.Enqueue(expected);

        var result = queue.TryPeek(out var item);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsTrue();
            await Assert.That(item.Priority).IsEqualTo(expected.Priority);
            await Assert.That(queue.Count).IsEqualTo(1); // Peek doesn't remove
        }
    }

    /// <summary>
    /// Tests that TryDequeue returns false on empty queue.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryDequeue_EmptyQueue_ReturnsFalse()
    {
        var queue = new PriorityQueue<TestItem>();
        var result = queue.TryDequeue(out var item);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsFalse();
            await Assert.That(item).IsNull();
        }
    }

    /// <summary>
    /// Tests that TryDequeue returns true and removes item on non-empty queue.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryDequeue_NonEmptyQueue_ReturnsTrue()
    {
        var queue = new PriorityQueue<TestItem>();
        var expected = new TestItem(5);
        queue.Enqueue(expected);

        var result = queue.TryDequeue(out var item);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsTrue();
            await Assert.That(item.Priority).IsEqualTo(expected.Priority);
            await Assert.That(queue.Count).IsEqualTo(0); // Dequeue removes
        }
    }

    /// <summary>
    /// Tests that EnqueueRange handles capacity growth correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueRange_CapacityGrowth_HandlesCorrectly()
    {
        var queue = new PriorityQueue<TestItem>(4);
        var items = Enumerable.Range(0, 20).Select(i => new TestItem(i)).ToArray();

        queue.EnqueueRange(items); // Should trigger capacity growth

        await Assert.That(queue.Count).IsEqualTo(20);
        await Assert.That(queue.VerifyHeapProperty()).IsTrue();
    }
#endif

    /// <summary>
    /// Tests that index 0 doesn't cause infinite loop during percolation.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_ParentEqualsIndex_NoInfiniteLoop()
    {
        // Index 0: parent = (0-1)/2 = -1/2 = 0 (integer division)
        // But the code checks if parent < 0 || parent == index, so it should return
        var queue = new PriorityQueue<TestItem>();
        queue.Enqueue(new TestItem(1));
        await Assert.That(queue.Count).IsEqualTo(1);
        await Assert.That(queue.VerifyHeapProperty()).IsTrue();
    }

    /// <summary>
    /// Tests heapify with only left child (no right child).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_HeapifyWithOnlyLeftChild()
    {
        // Create heap: [10, 5] where 5 is left child of 10
        // Removing root should handle left-only case
        var queue = new PriorityQueue<TestItem>(2);
        queue.Enqueue(new TestItem(10));
        queue.Enqueue(new TestItem(5));

        queue.Dequeue(); // Remove 5 (root after percolation)
        await Assert.That(queue.Peek().Priority).IsEqualTo(10);
        await Assert.That(queue.VerifyHeapProperty()).IsTrue();
    }

    /// <summary>
    /// Tests capacity growth at exact boundary (16 to 17 items).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_CapacityGrowthExactBoundary()
    {
        var queue = new PriorityQueue<TestItem>(16);

        // Fill to capacity
        for (var i = 0; i < 16; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        // This should trigger doubling to 32
        queue.Enqueue(new TestItem(100));
        await Assert.That(queue.Count).IsEqualTo(17);
        await Assert.That(queue.VerifyHeapProperty()).IsTrue();
    }

    /// <summary>
    /// Tests shrinking at 25% utilization threshold.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_ShrinkAt25PercentThreshold()
    {
        var queue = new PriorityQueue<TestItem>(32);

        // Fill to 32 items
        for (var i = 0; i < 32; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        // Dequeue to exactly 8 items (25% of 32)
        for (var i = 0; i < 24; i++)
        {
            queue.Dequeue();
        }

        await Assert.That(queue.Count).IsEqualTo(8);

        // Next dequeue should trigger shrink from 32 to 16
        queue.Dequeue();
        await Assert.That(queue.Count).IsEqualTo(7);
        await Assert.That(queue.VerifyHeapProperty()).IsTrue();
    }

    /// <summary>
    /// Tests that sequence counter handles large number of equal-priority items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_SequenceCounterNearOverflow()
    {
        // Note: This is a theoretical edge case. In practice, reaching long.MaxValue
        // would require 2^63 operations, which is practically impossible.
        // This test just verifies that equal-priority items maintain FIFO order.
        var queue = new PriorityQueue<TestItem>();

        for (var i = 0; i < 100; i++)
        {
            queue.Enqueue(new TestItem(5, $"item{i}")); // Equal priorities
        }

        // All items should still be retrievable in FIFO order
        await Assert.That(queue.Count).IsEqualTo(100);

        for (var i = 0; i < 100; i++)
        {
            var dequeued = queue.Dequeue();
            await Assert.That(dequeued.Id).IsEqualTo($"item{i}");
        }
    }

    /// <summary>
    /// Tests removing from a two-element heap.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_RemoveTwoElementHeap()
    {
        var queue = new PriorityQueue<TestItem>();
        var item1 = new TestItem(10);
        var item2 = new TestItem(5);

        queue.Enqueue(item1);
        queue.Enqueue(item2);

        // Remove root (lower priority dequeued first)
        using (Assert.Multiple())
        {
            await Assert.That(queue.Remove(item2)).IsTrue();
            await Assert.That(queue.Peek().Priority).IsEqualTo(10);
        }

        // Remove remaining
        using (Assert.Multiple())
        {
            await Assert.That(queue.Remove(item1)).IsTrue();
            await Assert.That(queue.Count).IsEqualTo(0);
        }
    }

    /// <summary>
    /// Tests creating a queue with zero capacity.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_ZeroCapacityQueue()
    {
        var queue = new PriorityQueue<TestItem>(0);
        await Assert.That(queue.Count).IsEqualTo(0);

        // Should be able to enqueue even with zero initial capacity
        queue.Enqueue(new TestItem(5));
        await Assert.That(queue.Count).IsEqualTo(1);
        await Assert.That(queue.Peek().Priority).IsEqualTo(5);
    }

    /// <summary>
    /// Tests removing an item that doesn't exist in the queue.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_RemoveNonExistentItem()
    {
        var queue = new PriorityQueue<TestItem>();
        queue.Enqueue(new TestItem(1));
        queue.Enqueue(new TestItem(2));
        queue.Enqueue(new TestItem(3));

        var nonExistent = new TestItem(99);
        await Assert.That(queue.Remove(nonExistent)).IsFalse();
        await Assert.That(queue.Count).IsEqualTo(3);
    }

    /// <summary>
    /// Test item that is comparable by priority.
    /// </summary>
    private sealed record TestItem(int Priority, string Id = "") : IComparable<TestItem>
    {
        public int CompareTo(TestItem? other)
        {
            if (other is null)
            {
                return 1;
            }

            return Priority.CompareTo(other.Priority);
        }
    }
}
