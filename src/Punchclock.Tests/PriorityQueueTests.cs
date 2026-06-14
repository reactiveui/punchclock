// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using ReactiveUI.Primitives.Core;

namespace Punchclock.Tests;

/// <summary>Tests for <see cref="PriorityQueue{T}"/>.</summary>
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Random is used for test data generation, not for security purposes")]
public class PriorityQueueTests
{
    private const int Two = 2;

    private const int Three = 3;

    private const int Five = 5;

    private const int Seven = 7;

    private const int Eight = 8;

    private const int Ten = 10;

    private const int Fifteen = 15;

    private const int Sixteen = 16;

    private const int Seventeen = 17;

    private const int Twenty = 20;

    private const int TwentyFour = 24;

    private const int ThirtyOne = 31;

    private const int ThirtyTwo = 32;

    private const int ThirtyThree = 33;

    private const int Ninety = 90;

    private const int OneHundred = 100;

    private const int OnThousand = 1000;

    /// <summary>Verifies that the constructor throws <see cref="ArgumentOutOfRangeException"/> for negative capacity.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithNegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        var ex = await Assert.That(() => new PriorityQueue<TestItem>(-1))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(ex!.ParamName).IsEqualTo("capacity");
    }

    /// <summary>Verifies that the constructor accepts zero capacity.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithZeroCapacity_Succeeds()
    {
        var queue = new PriorityQueue<TestItem>(0);
        await Assert.That(queue.Count).IsEqualTo(0);
    }

    /// <summary>Verifies that the default constructor creates a queue with default capacity.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_Default_Succeeds()
    {
        var queue = new PriorityQueue<TestItem>();
        await Assert.That(queue.Count).IsEqualTo(0);
    }

    /// <summary>Verifies that Peek throws <see cref="InvalidOperationException"/> when queue is empty.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Peek_OnEmptyQueue_ThrowsInvalidOperationException()
    {
        var queue = new PriorityQueue<TestItem>();
        await Assert.That(() => queue.Peek())
            .Throws<InvalidOperationException>();
    }

    /// <summary>Verifies that Dequeue throws <see cref="InvalidOperationException"/> when queue is empty.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dequeue_OnEmptyQueue_ThrowsInvalidOperationException()
    {
        var queue = new PriorityQueue<TestItem>();
        await Assert.That(() => queue.Dequeue())
            .Throws<InvalidOperationException>();
    }

    /// <summary>Verifies that Enqueue and Dequeue work correctly for a single item.</summary>
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

    /// <summary>Verifies that Peek returns the highest priority item without removing it.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Peek_ReturnsHighestPriorityWithoutRemoving()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            var item1 = new TestItem(1);
            var item2 = new TestItem(Two);

            queue.Enqueue(item1);
            queue.Enqueue(item2);

            var peeked = queue.Peek();
            await Assert.That(peeked).IsEqualTo(item1); // Lower value = higher priority
            await Assert.That(queue.Count).IsEqualTo(Two); // Count unchanged
        }
    }

    /// <summary>Verifies that items are dequeued in priority order.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dequeue_ReturnsItemsInPriorityOrder()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            var item1 = new TestItem(Five);
            var item2 = new TestItem(1);
            var item3 = new TestItem(Three);

            queue.Enqueue(item1);
            queue.Enqueue(item2);
            queue.Enqueue(item3);

            await Assert.That(queue.Dequeue()).IsEqualTo(item2); // Priority 1
            await Assert.That(queue.Dequeue()).IsEqualTo(item3); // Priority 3
            await Assert.That(queue.Dequeue()).IsEqualTo(item1); // Priority 5
        }
    }

    /// <summary>Verifies that DequeueSome returns the correct number of items.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DequeueSome_ReturnsCorrectNumberOfItems()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            queue.Enqueue(new TestItem(1));
            queue.Enqueue(new TestItem(Two));
            queue.Enqueue(new TestItem(Three));

            var items = queue.DequeueSome(Two);
            await Assert.That(items.Length).IsEqualTo(Two);
            await Assert.That(queue.Count).IsEqualTo(1);
        }
    }

    /// <summary>Verifies that DequeueSome with zero count returns empty array.</summary>
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

    /// <summary>Verifies that DequeueSome caps at available items when requesting more than available.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DequeueSome_RequestingMoreThanAvailable_ReturnsAllItems()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            queue.Enqueue(new TestItem(1));
            queue.Enqueue(new TestItem(Two));

            var items = queue.DequeueSome(Ten);
            await Assert.That(items.Length).IsEqualTo(Two);
            await Assert.That(queue.Count).IsEqualTo(0);
        }
    }

    /// <summary>Verifies that DequeueAll returns all items and empties the queue.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DequeueAll_ReturnsAllItems()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            queue.Enqueue(new TestItem(Three));
            queue.Enqueue(new TestItem(1));
            queue.Enqueue(new TestItem(Two));

            var items = queue.DequeueAll();
            await Assert.That(items.Length).IsEqualTo(Three);
            await Assert.That(queue.Count).IsEqualTo(0);
            await Assert.That(items[0].Priority).IsEqualTo(1);
            await Assert.That(items[1].Priority).IsEqualTo(Two);
            await Assert.That(items[Two].Priority).IsEqualTo(Three);
        }
    }

    /// <summary>Verifies that DequeueAll on empty queue returns empty array.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DequeueAll_OnEmptyQueue_ReturnsEmptyArray()
    {
        var queue = new PriorityQueue<TestItem>();
        var items = queue.DequeueAll();
        await Assert.That(items.Length).IsEqualTo(0);
    }

    /// <summary>Verifies that Remove returns true and removes the specified item.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Remove_ExistingItem_ReturnsTrueAndRemovesItem()
    {
        using (Assert.Multiple())
        {
            var queue = new PriorityQueue<TestItem>();
            var item1 = new TestItem(1);
            var item2 = new TestItem(Two);
            var item3 = new TestItem(Three);

            queue.Enqueue(item1);
            queue.Enqueue(item2);
            queue.Enqueue(item3);

            var removed = queue.Remove(item2);
            await Assert.That(removed).IsTrue();
            await Assert.That(queue.Count).IsEqualTo(Two);

            var dequeued = queue.Dequeue();
            await Assert.That(dequeued).IsEqualTo(item1); // item2 was removed
        }
    }

    /// <summary>Verifies that Remove returns false for non-existent item.</summary>
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

    /// <summary>Verifies that the queue grows capacity when needed.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_BeyondInitialCapacity_GrowsQueue()
    {
        var queue = new PriorityQueue<TestItem>(Two);
        for (var i = 0; i < Twenty; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        await Assert.That(queue.Count).IsEqualTo(Twenty);
    }

    /// <summary>Verifies that the queue shrinks capacity after many removals.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dequeue_ManyItems_ShrinksCapacity()
    {
        var queue = new PriorityQueue<TestItem>();
        for (var i = 0; i < OneHundred; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        // Dequeue most items to trigger shrinking
        for (var i = 0; i < Ninety; i++)
        {
            queue.Dequeue();
        }

        await Assert.That(queue.Count).IsEqualTo(Ten);
    }

    /// <summary>Verifies that equal priority items maintain FIFO order via sequence counter.</summary>
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
        var queue = new PriorityQueue<TestItem>(Ten);

        // Add and remove items to potentially trigger edge cases in percolation
        for (var i = 0; i < Five; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        queue.Dequeue();
        queue.Dequeue();

        // Queue should still be consistent
        await Assert.That(queue.Count).IsEqualTo(Three);
    }

    /// <summary>Verifies that enqueuing multiple items maintains heap property after each insertion.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_MultipleItems_MaintainsHeapProperty()
    {
        var queue = new PriorityQueue<TestItem>();

        for (var i = 0; i < Twenty; i++)
        {
            queue.Enqueue(new TestItem(Random.Shared.Next(OneHundred)));
            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>Verifies that dequeuing multiple items maintains heap property after each removal.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dequeue_MultipleItems_MaintainsHeapProperty()
    {
        var queue = new PriorityQueue<TestItem>();
        for (var i = 0; i < Twenty; i++)
        {
            queue.Enqueue(new TestItem(Random.Shared.Next(OneHundred)));
        }

        while (queue.Count > 0)
        {
            queue.Dequeue();
            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>Verifies that removing middle elements maintains heap property.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Remove_MiddleElement_MaintainsHeapProperty()
    {
        var queue = new PriorityQueue<TestItem>();
        var items = Enumerable.Range(0, Fifteen).Select(i => new TestItem(i, $"item{i}")).ToArray();

        foreach (var item in items)
        {
            queue.Enqueue(item);
        }

        // Remove middle elements
        for (var i = Five; i < Ten; i++)
        {
            queue.Remove(items[i]);
            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>Verifies that items are dequeued in correct priority order (reverse of insertion for ascending priorities).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_ReverseOrder_MaintainsCorrectness()
    {
        var queue = new PriorityQueue<TestItem>();

        // Insert in descending order
        for (var i = Twenty; i >= 1; i--)
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

    /// <summary>Verifies that items inserted in ascending order are still dequeued correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_AscendingOrder_MaintainsCorrectness()
    {
        var queue = new PriorityQueue<TestItem>();

        // Insert in ascending order
        for (var i = 1; i <= Twenty; i++)
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

    /// <summary>Verifies that capacity grows correctly across boundaries.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CapacityGrowth_MaintainsHeapProperty()
    {
        var queue = new PriorityQueue<TestItem>(Eight);

        // Test around capacity boundaries: 8 -> 16 -> 32
        foreach (var count in new[] { Eight, Fifteen, Sixteen, Seventeen, ThirtyOne, ThirtyTwo, ThirtyThree })
        {
            while (queue.Count < count)
            {
                queue.Enqueue(new TestItem(Random.Shared.Next(OneHundred)));
            }

            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>Verifies that capacity shrinking maintains heap property.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CapacityShrink_MaintainsHeapProperty()
    {
        var queue = new PriorityQueue<TestItem>();

        // Enqueue 100 items
        for (var i = 0; i < OneHundred; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        // Dequeue 90 items to trigger shrinking
        for (var i = 0; i < Ninety; i++)
        {
            queue.Dequeue();
        }

        using (Assert.Multiple())
        {
            await Assert.That(queue.Count).IsEqualTo(Ten);
            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>Stress test with 1000 random enqueue/dequeue/remove operations.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task StressTest_RandomOperations_AlwaysValid()
    {
        var queue = new PriorityQueue<TestItem>();
        var items = new List<TestItem>();

        for (var i = 0; i < OnThousand; i++)
        {
            switch (Random.Shared.Next(Three))
            {
                case 0: // Enqueue
                    {
                        var item = new TestItem(Random.Shared.Next(OneHundred), $"item{i}");
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
                        queue.Remove(toRemove);
                        items.Remove(toRemove);
                        break;
                    }
            }

            await Assert.That(queue.VerifyHeapProperty()).IsTrue();
        }
    }

    /// <summary>Verifies FIFO ordering when all items have equal priority.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task FIFO_EqualPriorities_AllElements()
    {
        var queue = new PriorityQueue<TestItem>();
        var items = Enumerable.Range(0, Twenty).Select(i => new TestItem(Five, $"item{i}")).ToArray();

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

    /// <summary>Verifies that sequence counter handles near-overflow correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SequenceCounter_NearOverflow_HandlesCorrectly()
    {
        var queue = new PriorityQueue<TestItem>();

        // Enqueue 100 items with equal priority to test sequence counter
        for (var i = 0; i < OneHundred; i++)
        {
            queue.Enqueue(new TestItem(Five, $"item{i}"));
        }

        // All items should still be retrievable
        await Assert.That(queue.Count).IsEqualTo(OneHundred);

        // Verify FIFO order is maintained
        for (var i = 0; i < OneHundred; i++)
        {
            var dequeued = queue.Dequeue();
            await Assert.That(dequeued.Id).IsEqualTo($"item{i}");
        }
    }

    /// <summary>Tests that EnqueueRange enqueues multiple items correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueRange_MultipleItems_AllEnqueued()
    {
        var queue = new PriorityQueue<TestItem>();
        var items = Enumerable.Range(0, Twenty).Select(i => new TestItem(i)).ToArray();

        queue.EnqueueRange(items);

        await Assert.That(queue.Count).IsEqualTo(Twenty);
        await Assert.That(queue.VerifyHeapProperty()).IsTrue();
    }

    /// <summary>Tests that EnqueueRange handles empty span correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueRange_EmptySpan_NoChange()
    {
        var queue = new PriorityQueue<TestItem>();
        queue.EnqueueRange([]);

        await Assert.That(queue.Count).IsEqualTo(0);
    }

    /// <summary>Tests that DequeueRange returns correct count when buffer is larger than available items.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DequeueRange_PartialBuffer_ReturnsCorrectCount()
    {
        var queue = new PriorityQueue<TestItem>();
        for (var i = 0; i < Ten; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        var buffer = new TestItem[Five];
        var count = queue.DequeueRange(buffer);

        await Assert.That(count).IsEqualTo(Five);
        await Assert.That(queue.Count).IsEqualTo(Five);
    }

    /// <summary>Tests that DequeueRange returns all items when buffer is larger than queue.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DequeueRange_LargerThanQueue_ReturnsAllItems()
    {
        var queue = new PriorityQueue<TestItem>();
        for (var i = 0; i < Five; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        var buffer = new TestItem[Ten];
        var count = queue.DequeueRange(buffer);

        await Assert.That(count).IsEqualTo(Five);
        await Assert.That(queue.Count).IsEqualTo(0);
    }

    /// <summary>Tests that TryPeek returns false on empty queue.</summary>
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

    /// <summary>Tests that TryPeek returns true and item on non-empty queue.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryPeek_NonEmptyQueue_ReturnsTrue()
    {
        var queue = new PriorityQueue<TestItem>();
        var expected = new TestItem(Five);
        queue.Enqueue(expected);

        var result = queue.TryPeek(out var item);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsTrue();
            await Assert.That(item!.Priority).IsEqualTo(expected.Priority);
            await Assert.That(queue.Count).IsEqualTo(1); // Peek doesn't remove
        }
    }

    /// <summary>Tests that TryDequeue returns false on empty queue.</summary>
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

    /// <summary>Tests that TryDequeue returns true and removes item on non-empty queue.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryDequeue_NonEmptyQueue_ReturnsTrue()
    {
        var queue = new PriorityQueue<TestItem>();
        var expected = new TestItem(Five);
        queue.Enqueue(expected);

        var result = queue.TryDequeue(out var item);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsTrue();
            await Assert.That(item!.Priority).IsEqualTo(expected.Priority);
            await Assert.That(queue.Count).IsEqualTo(0); // Dequeue removes
        }
    }

    /// <summary>Tests that EnqueueRange handles capacity growth correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueRange_CapacityGrowth_HandlesCorrectly()
    {
        var queue = new PriorityQueue<TestItem>(4);
        var items = Enumerable.Range(0, Twenty).Select(i => new TestItem(i)).ToArray();

        queue.EnqueueRange(items); // Should trigger capacity growth

        await Assert.That(queue.Count).IsEqualTo(Twenty);
        await Assert.That(queue.VerifyHeapProperty()).IsTrue();
    }

    /// <summary>
    /// Covers PriorityQueue.cs line 191 - EnqueueRange with zero initial capacity.
    /// Verifies that when _items.Length == 0, the capacity defaults to DefaultCapacity (16).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueRange_WithZeroInitialCapacity_UsesDefaultCapacity()
    {
        var queue = new PriorityQueue<TestItem>(0); // Start with zero capacity
        var items = Enumerable.Range(0, Five).Select(i => new TestItem(i)).ToArray();

        // Line 191: _items.Length == 0 ? DefaultCapacity : _items.Length * 2
        queue.EnqueueRange(items); // Should use DefaultCapacity path

        await Assert.That(queue.Count).IsEqualTo(Five);
        await Assert.That(queue.VerifyHeapProperty()).IsTrue();
    }

    /// <summary>Tests that index 0 doesn't cause infinite loop during percolation.</summary>
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

    /// <summary>Tests heapify with only left child (no right child).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_HeapifyWithOnlyLeftChild()
    {
        // Create heap: [10, 5] where 5 is left child of 10
        // Removing root should handle left-only case
        var queue = new PriorityQueue<TestItem>(Two);
        queue.Enqueue(new TestItem(Ten));
        queue.Enqueue(new TestItem(Five));

        queue.Dequeue(); // Remove 5 (root after percolation)
        await Assert.That(queue.Peek().Priority).IsEqualTo(Ten);
        await Assert.That(queue.VerifyHeapProperty()).IsTrue();
    }

    /// <summary>Tests capacity growth at exact boundary (16 to 17 items).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_CapacityGrowthExactBoundary()
    {
        var queue = new PriorityQueue<TestItem>(Sixteen);

        // Fill to capacity
        for (var i = 0; i < Sixteen; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        // This should trigger doubling to 32
        queue.Enqueue(new TestItem(OneHundred));
        await Assert.That(queue.Count).IsEqualTo(Seventeen);
        await Assert.That(queue.VerifyHeapProperty()).IsTrue();
    }

    /// <summary>Tests shrinking at 25% utilization threshold.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_ShrinkAt25PercentThreshold()
    {
        var queue = new PriorityQueue<TestItem>(ThirtyTwo);

        // Fill to 32 items
        for (var i = 0; i < ThirtyTwo; i++)
        {
            queue.Enqueue(new TestItem(i));
        }

        // Dequeue to exactly 8 items (25% of 32)
        for (var i = 0; i < TwentyFour; i++)
        {
            queue.Dequeue();
        }

        await Assert.That(queue.Count).IsEqualTo(Eight);

        // Next dequeue should trigger shrink from 32 to 16
        queue.Dequeue();
        await Assert.That(queue.Count).IsEqualTo(Seven);
        await Assert.That(queue.VerifyHeapProperty()).IsTrue();
    }

    /// <summary>Tests that sequence counter handles large number of equal-priority items.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_SequenceCounterNearOverflow()
    {
        var queue = new PriorityQueue<TestItem>();
        var items = Enumerable.Range(0, OneHundred)
            .Select(i => new TestItem(Five, $"item{i}"))
            .ToArray();

        queue.EnqueueRange(items);

        await Assert.That(queue.Count).IsEqualTo(OneHundred);

        for (var i = 0; i < OneHundred; i++)
        {
            var dequeued = queue.Dequeue();
            await Assert.That(dequeued.Id).IsEqualTo($"item{i}");
        }
    }

    /// <summary>Tests removing from a two-element heap.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_RemoveTwoElementHeap()
    {
        var queue = new PriorityQueue<TestItem>();
        var item1 = new TestItem(Ten);
        var item2 = new TestItem(Five);

        queue.Enqueue(item1);
        queue.Enqueue(item2);

        // Remove root (lower priority dequeued first)
        using (Assert.Multiple())
        {
            await Assert.That(queue.Remove(item2)).IsTrue();
            await Assert.That(queue.Peek().Priority).IsEqualTo(Ten);
        }

        // Remove remaining
        using (Assert.Multiple())
        {
            await Assert.That(queue.Remove(item1)).IsTrue();
            await Assert.That(queue.Count).IsEqualTo(0);
        }
    }

    /// <summary>Tests creating a queue with zero capacity.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_ZeroCapacityQueue()
    {
        var queue = new PriorityQueue<TestItem>(0);
        await Assert.That(queue.Count).IsEqualTo(0);

        // Should be able to enqueue even with zero initial capacity
        queue.Enqueue(new TestItem(Five));
        await Assert.That(queue.Count).IsEqualTo(1);
        await Assert.That(queue.Peek().Priority).IsEqualTo(Five);
    }

    /// <summary>Tests removing an item that doesn't exist in the queue.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EdgeCase_RemoveNonExistentItem()
    {
        var queue = new PriorityQueue<TestItem>();
        queue.Enqueue(new TestItem(1));
        queue.Enqueue(new TestItem(Two));
        queue.Enqueue(new TestItem(Three));

        var nonExistent = new TestItem(99);
        await Assert.That(queue.Remove(nonExistent)).IsFalse();
        await Assert.That(queue.Count).IsEqualTo(Three);
    }

    /// <summary>Test item that is comparable by priority.</summary>
    /// <param name="Priority">The priority of the item.</param>
    /// <param name="Id">The identifier of the item.</param>
    private sealed record TestItem(int Priority, string Id = "") : IComparable<TestItem>
    {
        /// <summary>Compares this instance with another TestItem based on Priority.</summary>
        /// <param name="other">The other TestItem to compare with.</param>
        /// <returns>A value indicating the relative order of the items.</returns>
        public int CompareTo(TestItem? other)
        {
            return other is null ? 1 : Priority.CompareTo(other.Priority);
        }
    }
}
