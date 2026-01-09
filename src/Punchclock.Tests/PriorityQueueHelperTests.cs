// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Core;

namespace Punchclock.Tests;

/// <summary>
/// Unit tests for PriorityQueueHelper static methods.
/// These tests verify the correctness of heap operations in isolation.
/// </summary>
public class PriorityQueueHelperTests
{
    /// <summary>
    /// Verifies that Percolate handles an empty array correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_EmptyArray_NoChange()
    {
        var items = Array.Empty<TestItem>();
        PriorityQueueHelper.Percolate(items, 0, 0);
        await Assert.That(items.Length).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that Percolate handles a single element correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_SingleElement_NoChange()
    {
        var items = new[] { new TestItem(5) };
        var expected = items[0];
        PriorityQueueHelper.Percolate(items, 0, 1);
        await Assert.That(items[0]).IsEqualTo(expected);
    }

    /// <summary>
    /// Verifies that Percolate handles out-of-bounds index correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_IndexOutOfBounds_NoChange()
    {
        var items = new[] { new TestItem(5), new TestItem(10) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Percolate(items, 5, 2);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
        }
    }

    /// <summary>
    /// Verifies that Percolate handles negative index correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_NegativeIndex_NoChange()
    {
        var items = new[] { new TestItem(5), new TestItem(10) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Percolate(items, -1, 2);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
        }
    }

    /// <summary>
    /// Verifies that Percolate does not move a leaf node that satisfies heap property.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_LeafNode_NoPercolation()
    {
        var items = new[] { new TestItem(1), new TestItem(5), new TestItem(10) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Percolate(items, 2, 3);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
            await Assert.That(items[2]).IsEqualTo(originalItems[2]);
        }
    }

    /// <summary>
    /// Verifies that Percolate does not swap when node has lower priority than parent.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_NodeSmallerThanParent_NoSwap()
    {
        var items = new[] { new TestItem(1), new TestItem(5) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Percolate(items, 1, 2);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
        }
    }

    /// <summary>
    /// Verifies that Percolate swaps once when node has higher priority than parent.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_NodeLargerThanParent_SwapsOnce()
    {
        var items = new[] { new TestItem(10), new TestItem(5) };
        PriorityQueueHelper.Percolate(items, 1, 2);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(5);
            await Assert.That(items[1].Priority).IsEqualTo(10);
        }
    }

    /// <summary>
    /// Verifies that Percolate bubbles high-priority item to root through multiple levels.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_MultiLevel_PercolatesToRoot()
    {
        var items = new[]
        {
            new TestItem(5),
            new TestItem(10),
            new TestItem(15),
            new TestItem(20),
            new TestItem(25),
            new TestItem(1),
            new TestItem(30),
        };

        PriorityQueueHelper.Percolate(items, 5, 7);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(1);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, 7)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that Percolate works correctly when inserting as left child.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_LeftChildPath_Correct()
    {
        var items = new[] { new TestItem(10), new TestItem(5) };
        PriorityQueueHelper.Percolate(items, 1, 2);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(5);
            await Assert.That(items[1].Priority).IsEqualTo(10);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, 2)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that Percolate works correctly when inserting as right child.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_RightChildPath_Correct()
    {
        var items = new[] { new TestItem(10), new TestItem(15), new TestItem(5) };
        PriorityQueueHelper.Percolate(items, 2, 3);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(5);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, 3)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that Percolate stops recursion at root.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_ParentEqualsIndex_StopsRecursion()
    {
        var items = new[] { new TestItem(5) };
        PriorityQueueHelper.Percolate(items, 0, 1);
        await Assert.That(items[0].Priority).IsEqualTo(5);
    }

    /// <summary>
    /// Verifies that Percolate maintains heap property after each insertion.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_MaintainsHeapProperty()
    {
        var items = new TestItem[10];
        var priorities = new[] { 50, 30, 40, 10, 20, 5, 15, 25, 35, 1 };
        var count = 0;

        foreach (var priority in priorities)
        {
            items[count] = new TestItem(priority);
            PriorityQueueHelper.Percolate(items, count, count + 1);
            count++;

            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, count)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that Heapify handles an empty array correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_EmptyArray_NoChange()
    {
        var items = Array.Empty<TestItem>();
        PriorityQueueHelper.Heapify(items, 0, 0);
        await Assert.That(items.Length).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that Heapify handles a single element correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_SingleElement_NoChange()
    {
        var items = new[] { new TestItem(5) };
        var expected = items[0];
        PriorityQueueHelper.Heapify(items, 0, 1);
        await Assert.That(items[0]).IsEqualTo(expected);
    }

    /// <summary>
    /// Verifies that Heapify handles out-of-bounds index correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_IndexOutOfBounds_NoChange()
    {
        var items = new[] { new TestItem(5), new TestItem(10) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Heapify(items, 5, 2);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
        }
    }

    /// <summary>
    /// Verifies that Heapify handles negative index correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_NegativeIndex_NoChange()
    {
        var items = new[] { new TestItem(5), new TestItem(10) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Heapify(items, -1, 2);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
        }
    }

    /// <summary>
    /// Verifies that Heapify does not move a leaf node.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_LeafNode_NoHeapify()
    {
        var items = new[] { new TestItem(1), new TestItem(5), new TestItem(10) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Heapify(items, 2, 3);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
            await Assert.That(items[2]).IsEqualTo(originalItems[2]);
        }
    }

    /// <summary>
    /// Verifies that Heapify does not swap when node is larger than children.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_NodeLargerThanBothChildren_NoSwap()
    {
        var items = new[] { new TestItem(1), new TestItem(5), new TestItem(10) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Heapify(items, 0, 3);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
            await Assert.That(items[2]).IsEqualTo(originalItems[2]);
        }
    }

    /// <summary>
    /// Verifies that Heapify swaps with left child when only left exists.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_NodeSmallerThanLeftOnly_SwapsWithLeft()
    {
        var items = new[] { new TestItem(10), new TestItem(5) };
        PriorityQueueHelper.Heapify(items, 0, 2);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(5);
            await Assert.That(items[1].Priority).IsEqualTo(10);
        }
    }

    /// <summary>
    /// Verifies that Heapify swaps with right child when right has higher priority.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_NodeSmallerThanRightOnly_SwapsWithRight()
    {
        var items = new[] { new TestItem(10), new TestItem(15), new TestItem(5) };
        PriorityQueueHelper.Heapify(items, 0, 3);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(5);
            await Assert.That(items[2].Priority).IsEqualTo(10);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, 3)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that Heapify swaps with highest-priority child when both children exist.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_NodeSmallerThanBoth_SwapsWithHigherChild()
    {
        var items = new[] { new TestItem(30), new TestItem(10), new TestItem(15) };
        PriorityQueueHelper.Heapify(items, 0, 3);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(10);
            await Assert.That(items[1].Priority).IsEqualTo(30);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, 3)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that Heapify sinks low-priority item from root to leaf.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_FromRoot_SinksToLeaf()
    {
        // Start with a valid heap, then replace root with low-priority item
        // Valid heap:  5
        //            /   \
        //           10    15
        //          / \   /
        //         20 25 30
        var items = new[]
        {
            new TestItem(100),  // Replace root with low-priority item
            new TestItem(10),   // Valid subtree
            new TestItem(15),   // Valid subtree
            new TestItem(20),
            new TestItem(25),
            new TestItem(30),
        };

        PriorityQueueHelper.Heapify(items, 0, 6);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsNotEqualTo(100);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, 6)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that Heapify from middle node only affects subtree.
    /// Quaternary heap: node at index 1 has children at 5,6,7,8.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_MiddleNode_SinksPartially()
    {
        // Quaternary heap structure:
        // Index 0: children at 1, 2, 3, 4
        // Index 1: children at 5, 6, 7, 8
        var items = new[]
        {
            new TestItem(1),   // Root (index 0)
            new TestItem(100), // Child of root (index 1) - HIGH value, will sink
            new TestItem(6),   // Child of root (index 2)
            new TestItem(7),   // Child of root (index 3)
            new TestItem(8),   // Child of root (index 4)
            new TestItem(10),  // Child of node 1 (index 5)
            new TestItem(15),  // Child of node 1 (index 6)
            new TestItem(20),  // Child of node 1 (index 7)
            new TestItem(25),  // Child of node 1 (index 8)
        };

        // Heapify node at index 1 (priority 100) - should sink down to its subtree
        PriorityQueueHelper.Heapify(items, 1, 9);

        using (Assert.Multiple())
        {
            await Assert.That(items[1].Priority).IsNotEqualTo(100); // Should have sunk
            await Assert.That(items[1].Priority).IsLessThan(100); // Should be swapped with a child
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, 9)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that Heapify handles node with only left child correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_OnlyLeftChild_NoRightChild()
    {
        var items = new[] { new TestItem(10), new TestItem(5) };
        PriorityQueueHelper.Heapify(items, 0, 2);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(5);
            await Assert.That(items[1].Priority).IsEqualTo(10);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, 2)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that Heapify maintains heap property through repeated operations.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_MaintainsHeapProperty()
    {
        var items = new[]
        {
            new TestItem(1),
            new TestItem(5),
            new TestItem(10),
            new TestItem(15),
            new TestItem(20),
            new TestItem(25),
            new TestItem(30),
        };

        var count = items.Length;

        while (count > 1)
        {
            items[0] = items[count - 1];
            count--;
            PriorityQueueHelper.Heapify(items, 0, count);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, count)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that VerifyHeapProperty returns true for an empty array.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_EmptyArray_ReturnsTrue()
    {
        var items = Array.Empty<TestItem>();
        var result = PriorityQueueHelper.VerifyHeapProperty(items, 0);
        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Verifies that VerifyHeapProperty returns true for a single element.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_SingleElement_ReturnsTrue()
    {
        var items = new[] { new TestItem(5) };
        var result = PriorityQueueHelper.VerifyHeapProperty(items, 1);
        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Verifies that VerifyHeapProperty returns true for valid two-element heap.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_ValidTwoElements_ReturnsTrue()
    {
        var items = new[] { new TestItem(5), new TestItem(10) };
        var result = PriorityQueueHelper.VerifyHeapProperty(items, 2);
        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Verifies that VerifyHeapProperty returns true for valid max-heap.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_ValidMaxHeap_ReturnsTrue()
    {
        var items = new[]
        {
            new TestItem(1),
            new TestItem(5),
            new TestItem(10),
            new TestItem(15),
            new TestItem(20),
            new TestItem(25),
        };

        var result = PriorityQueueHelper.VerifyHeapProperty(items, 6);
        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Verifies that VerifyHeapProperty returns false when left child violates heap property.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_InvalidLeftChild_ReturnsFalse()
    {
        var items = new[] { new TestItem(10), new TestItem(5) };
        var result = PriorityQueueHelper.VerifyHeapProperty(items, 2);
        await Assert.That(result).IsFalse();
    }

    /// <summary>
    /// Verifies that VerifyHeapProperty returns false when right child violates heap property.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_InvalidRightChild_ReturnsFalse()
    {
        var items = new[] { new TestItem(10), new TestItem(15), new TestItem(5) };
        var result = PriorityQueueHelper.VerifyHeapProperty(items, 3);
        await Assert.That(result).IsFalse();
    }

    /// <summary>
    /// Verifies that VerifyHeapProperty returns true for complex valid heap.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_ComplexValidHeap_ReturnsTrue()
    {
        var items = new[]
        {
            new TestItem(1),   new TestItem(3),   new TestItem(5),
            new TestItem(7),   new TestItem(9),   new TestItem(11),  new TestItem(13),
            new TestItem(15),  new TestItem(17),  new TestItem(19),  new TestItem(21),
            new TestItem(23),  new TestItem(25),  new TestItem(27),  new TestItem(29),
        };

        var result = PriorityQueueHelper.VerifyHeapProperty(items, 15);
        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Verifies that VerifyHeapProperty returns false for complex invalid quaternary heap.
    /// Quaternary heap: index 0 has children 1,2,3,4; index 1 has children 5,6,7,8; etc.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_ComplexInvalidHeap_ReturnsFalse()
    {
        // Quaternary heap structure:
        // Index 0: children at 1, 2, 3, 4
        // Index 1: children at 5, 6, 7, 8
        // Index 2: children at 9, 10, 11, 12
        // Index 3: children at 13, 14, 15, 16
        var items = new[]
        {
            new TestItem(1),   new TestItem(3),   new TestItem(5),
            new TestItem(7),   new TestItem(9),   new TestItem(11),  new TestItem(13),
            new TestItem(15),  new TestItem(17),  new TestItem(2),   new TestItem(21), // Index 9 has priority 2, parent is index 2 (priority 5)
            new TestItem(23),  new TestItem(25),  new TestItem(27),  new TestItem(29),
        };

        // Index 9's parent is (9-1)/4 = 2, so parent is index 2 with priority 5
        // Child at index 9 has priority 2, which is HIGHER priority (lower value) than parent priority 5
        // This violates the max-heap property
        var result = PriorityQueueHelper.VerifyHeapProperty(items, 15);
        await Assert.That(result).IsFalse();
    }

    /// <summary>
    /// Test item that is comparable by priority.
    /// </summary>
    private sealed record TestItem(int Priority, int Id = 0) : IComparable<TestItem>
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
