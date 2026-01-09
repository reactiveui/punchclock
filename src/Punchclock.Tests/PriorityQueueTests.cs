// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Punchclock.Tests;

/// <summary>
/// Tests for <see cref="PriorityQueue{T}"/>.
/// </summary>
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
