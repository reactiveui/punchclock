// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Punchclock.Tests;

/// <summary>Unit tests for PriorityQueueHelper static methods. These tests verify the correctness of heap operations in isolation.</summary>
public class PriorityQueueHelperTests
{
    private const int Two = 2;

    private const int Three = 3;

    private const int Five = 5;

    private const int Six = 6;

    private const int Seven = 7;

    private const int Nine = 9;

    private const int Ten = 10;

    private const int Thirty = 30;

    private const int OneHundred = 100;

    /// <summary>Verifies that Percolate handles an empty array correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_EmptyArray_NoChange()
    {
        var items = Array.Empty<TestItem>();
        PriorityQueueHelper.Percolate(items, 0, 0);
        await Assert.That(items.Length).IsEqualTo(0);
    }

    /// <summary>Verifies that Percolate handles a single element correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_SingleElement_NoChange()
    {
        var items = new[] { new TestItem(Five) };
        var expected = items[0];
        PriorityQueueHelper.Percolate(items, 0, 1);
        await Assert.That(items[0]).IsEqualTo(expected);
    }

    /// <summary>Verifies that Percolate handles out-of-bounds index correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_IndexOutOfBounds_NoChange()
    {
        var items = new[] { new TestItem(Five), new TestItem(Ten) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Percolate(items, Five, Two);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
        }
    }

    /// <summary>Verifies that Percolate handles negative index correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_NegativeIndex_NoChange()
    {
        var items = new[] { new TestItem(Five), new TestItem(Ten) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Percolate(items, -1, Two);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
        }
    }

    /// <summary>Verifies that Percolate does not move a leaf node that satisfies heap property.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_LeafNode_NoPercolation()
    {
        var items = new[] { new TestItem(1), new TestItem(Five), new TestItem(Ten) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Percolate(items, Two, Three);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
            await Assert.That(items[Two]).IsEqualTo(originalItems[Two]);
        }
    }

    /// <summary>Verifies that Percolate does not swap when node has lower priority than parent.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_NodeSmallerThanParent_NoSwap()
    {
        var items = new[] { new TestItem(1), new TestItem(Five) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Percolate(items, 1, Two);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
        }
    }

    /// <summary>Verifies that Percolate swaps once when node has higher priority than parent.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_NodeLargerThanParent_SwapsOnce()
    {
        var items = new[] { new TestItem(Ten), new TestItem(Five) };
        PriorityQueueHelper.Percolate(items, 1, Two);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(Five);
            await Assert.That(items[1].Priority).IsEqualTo(Ten);
        }
    }

    /// <summary>Verifies that Percolate bubbles high-priority item to root through multiple levels.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_MultiLevel_PercolatesToRoot()
    {
        var items = new[]
        {
            new TestItem(Five),
            new TestItem(Ten),
            new TestItem(15),
            new TestItem(20),
            new TestItem(25),
            new TestItem(1),
            new TestItem(Thirty),
        };

        PriorityQueueHelper.Percolate(items, Five, Seven);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(1);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, Seven)).IsTrue();
        }
    }

    /// <summary>Verifies that Percolate works correctly when inserting as left child.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_LeftChildPath_Correct()
    {
        var items = new[] { new TestItem(Ten), new TestItem(Five) };
        PriorityQueueHelper.Percolate(items, 1, Two);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(Five);
            await Assert.That(items[1].Priority).IsEqualTo(Ten);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, Two)).IsTrue();
        }
    }

    /// <summary>Verifies that Percolate works correctly when inserting as right child.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_RightChildPath_Correct()
    {
        var items = new[] { new TestItem(Ten), new TestItem(15), new TestItem(Five) };
        PriorityQueueHelper.Percolate(items, Two, Three);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(Five);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, Three)).IsTrue();
        }
    }

    /// <summary>Verifies that Percolate stops recursion at root.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_ParentEqualsIndex_StopsRecursion()
    {
        var items = new[] { new TestItem(Five) };
        PriorityQueueHelper.Percolate(items, 0, 1);
        await Assert.That(items[0].Priority).IsEqualTo(Five);
    }

    /// <summary>Verifies that Percolate maintains heap property after each insertion.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Percolate_MaintainsHeapProperty()
    {
        var items = new TestItem[Ten];
        var priorities = new[] { 50, Thirty, 40, Ten, 20, Five, 15, 25, 35, 1 };
        var count = 0;

        foreach (var priority in priorities)
        {
            items[count] = new TestItem(priority);
            PriorityQueueHelper.Percolate(items, count, count + 1);
            count++;

            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, count)).IsTrue();
        }
    }

    /// <summary>Verifies that Heapify handles an empty array correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_EmptyArray_NoChange()
    {
        var items = Array.Empty<TestItem>();
        PriorityQueueHelper.Heapify(items, 0, 0);
        await Assert.That(items.Length).IsEqualTo(0);
    }

    /// <summary>Verifies that Heapify handles a single element correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_SingleElement_NoChange()
    {
        var items = new[] { new TestItem(Five) };
        var expected = items[0];
        PriorityQueueHelper.Heapify(items, 0, 1);
        await Assert.That(items[0]).IsEqualTo(expected);
    }

    /// <summary>Verifies that Heapify handles out-of-bounds index correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_IndexOutOfBounds_NoChange()
    {
        var items = new[] { new TestItem(Five), new TestItem(Ten) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Heapify(items, Five, Two);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
        }
    }

    /// <summary>Verifies that Heapify handles negative index correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_NegativeIndex_NoChange()
    {
        var items = new[] { new TestItem(Five), new TestItem(Ten) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Heapify(items, -1, Two);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
        }
    }

    /// <summary>Verifies that Heapify does not move a leaf node.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_LeafNode_NoHeapify()
    {
        var items = new[] { new TestItem(1), new TestItem(Five), new TestItem(Ten) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Heapify(items, Two, Three);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
            await Assert.That(items[Two]).IsEqualTo(originalItems[Two]);
        }
    }

    /// <summary>Verifies that Heapify does not swap when node is larger than children.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_NodeLargerThanBothChildren_NoSwap()
    {
        var items = new[] { new TestItem(1), new TestItem(Five), new TestItem(Ten) };
        var originalItems = items.ToArray();
        PriorityQueueHelper.Heapify(items, 0, Three);

        using (Assert.Multiple())
        {
            await Assert.That(items[0]).IsEqualTo(originalItems[0]);
            await Assert.That(items[1]).IsEqualTo(originalItems[1]);
            await Assert.That(items[Two]).IsEqualTo(originalItems[Two]);
        }
    }

    /// <summary>Verifies that Heapify swaps with left child when only left exists.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_NodeSmallerThanLeftOnly_SwapsWithLeft()
    {
        var items = new[] { new TestItem(Ten), new TestItem(Five) };
        PriorityQueueHelper.Heapify(items, 0, Two);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(Five);
            await Assert.That(items[1].Priority).IsEqualTo(Ten);
        }
    }

    /// <summary>Verifies that Heapify swaps with right child when right has higher priority.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_NodeSmallerThanRightOnly_SwapsWithRight()
    {
        var items = new[] { new TestItem(Ten), new TestItem(15), new TestItem(Five) };
        PriorityQueueHelper.Heapify(items, 0, Three);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(Five);
            await Assert.That(items[Two].Priority).IsEqualTo(Ten);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, Three)).IsTrue();
        }
    }

    /// <summary>Verifies that Heapify swaps with highest-priority child when both children exist.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_NodeSmallerThanBoth_SwapsWithHigherChild()
    {
        var items = new[] { new TestItem(Thirty), new TestItem(Ten), new TestItem(15) };
        PriorityQueueHelper.Heapify(items, 0, Three);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(Ten);
            await Assert.That(items[1].Priority).IsEqualTo(Thirty);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, Three)).IsTrue();
        }
    }

    /// <summary>Verifies that Heapify sinks low-priority item from root to leaf.</summary>
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
            new TestItem(OneHundred), // Replace root with low-priority item
            new TestItem(Ten), // Valid subtree
            new TestItem(15), // Valid subtree
            new TestItem(20),
            new TestItem(25),
            new TestItem(Thirty),
        };

        PriorityQueueHelper.Heapify(items, 0, Six);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsNotEqualTo(OneHundred);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, Six)).IsTrue();
        }
    }

    /// <summary>Verifies that Heapify from middle node only affects subtree. Quaternary heap: node at index 1 has children at 5,6,7,8.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_MiddleNode_SinksPartially()
    {
        // Quaternary heap structure:
        // Index 0: children at 1, 2, 3, 4
        // Index 1: children at 5, 6, 7, 8
        var items = new[]
        {
            new TestItem(1), // Root (index 0)
            new TestItem(OneHundred), // Child of root (index 1) - HIGH value, will sink
            new TestItem(Six), // Child of root (index 2)
            new TestItem(Seven), // Child of root (index 3)
            new TestItem(8), // Child of root (index 4)
            new TestItem(Ten), // Child of node 1 (index 5)
            new TestItem(15), // Child of node 1 (index 6)
            new TestItem(20), // Child of node 1 (index 7)
            new TestItem(25), // Child of node 1 (index 8)
        };

        // Heapify node at index 1 (priority 100) - should sink down to its subtree
        PriorityQueueHelper.Heapify(items, 1, Nine);

        using (Assert.Multiple())
        {
            await Assert.That(items[1].Priority).IsNotEqualTo(OneHundred); // Should have sunk
            await Assert.That(items[1].Priority).IsLessThan(OneHundred); // Should be swapped with a child
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, Nine)).IsTrue();
        }
    }

    /// <summary>Verifies that Heapify handles node with only left child correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_OnlyLeftChild_NoRightChild()
    {
        var items = new[] { new TestItem(Ten), new TestItem(Five) };
        PriorityQueueHelper.Heapify(items, 0, Two);

        using (Assert.Multiple())
        {
            await Assert.That(items[0].Priority).IsEqualTo(Five);
            await Assert.That(items[1].Priority).IsEqualTo(Ten);
            await Assert.That(PriorityQueueHelper.VerifyHeapProperty(items, Two)).IsTrue();
        }
    }

    /// <summary>Verifies that Heapify maintains heap property through repeated operations.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Heapify_MaintainsHeapProperty()
    {
        var items = new[]
        {
            new TestItem(1),
            new TestItem(Five),
            new TestItem(Ten),
            new TestItem(15),
            new TestItem(20),
            new TestItem(25),
            new TestItem(Thirty),
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

    /// <summary>Verifies that VerifyHeapProperty returns true for an empty array.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_EmptyArray_ReturnsTrue()
    {
        var items = Array.Empty<TestItem>();
        var result = PriorityQueueHelper.VerifyHeapProperty(items, 0);
        await Assert.That(result).IsTrue();
    }

    /// <summary>Verifies that VerifyHeapProperty returns true for a single element.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_SingleElement_ReturnsTrue()
    {
        var items = new[] { new TestItem(Five) };
        var result = PriorityQueueHelper.VerifyHeapProperty(items, 1);
        await Assert.That(result).IsTrue();
    }

    /// <summary>Verifies that VerifyHeapProperty returns true for valid two-element heap.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_ValidTwoElements_ReturnsTrue()
    {
        var items = new[] { new TestItem(Five), new TestItem(Ten) };
        var result = PriorityQueueHelper.VerifyHeapProperty(items, Two);
        await Assert.That(result).IsTrue();
    }

    /// <summary>Verifies that VerifyHeapProperty returns true for valid max-heap.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_ValidMaxHeap_ReturnsTrue()
    {
        var items = new[]
        {
            new TestItem(1),
            new TestItem(Five),
            new TestItem(Ten),
            new TestItem(15),
            new TestItem(20),
            new TestItem(25),
        };

        var result = PriorityQueueHelper.VerifyHeapProperty(items, Six);
        await Assert.That(result).IsTrue();
    }

    /// <summary>Verifies that VerifyHeapProperty returns false when left child violates heap property.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_InvalidLeftChild_ReturnsFalse()
    {
        var items = new[] { new TestItem(Ten), new TestItem(Five) };
        var result = PriorityQueueHelper.VerifyHeapProperty(items, Two);
        await Assert.That(result).IsFalse();
    }

    /// <summary>Verifies that VerifyHeapProperty returns false when right child violates heap property.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_InvalidRightChild_ReturnsFalse()
    {
        var items = new[] { new TestItem(Ten), new TestItem(15), new TestItem(Five) };
        var result = PriorityQueueHelper.VerifyHeapProperty(items, Three);
        await Assert.That(result).IsFalse();
    }

    /// <summary>Verifies that VerifyHeapProperty returns true for complex valid heap.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task VerifyHeapProperty_ComplexValidHeap_ReturnsTrue()
    {
        var items = new[]
        {
            new TestItem(1), new TestItem(Three), new TestItem(Five),
            new TestItem(Seven), new TestItem(Nine), new TestItem(11), new TestItem(13),
            new TestItem(15), new TestItem(17), new TestItem(19), new TestItem(21),
            new TestItem(23), new TestItem(25), new TestItem(27), new TestItem(29),
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
            new TestItem(1), new TestItem(Three), new TestItem(Five),
            new TestItem(Seven), new TestItem(Nine), new TestItem(11), new TestItem(13),
            new TestItem(15), new TestItem(17), new TestItem(Two), new TestItem(21), // Index 9 has priority 2, parent is index 2 (priority 5)
            new TestItem(23), new TestItem(25), new TestItem(27), new TestItem(29),
        };

        // Index 9's parent is (9-1)/4 = 2, so parent is index 2 with priority 5
        // Child at index 9 has priority 2, which is HIGHER priority (lower value) than parent priority 5
        // This violates the max-heap property
        var result = PriorityQueueHelper.VerifyHeapProperty(items, 15);
        await Assert.That(result).IsFalse();
    }

    /// <summary>Test item that is comparable by priority.</summary>
    /// <param name="Priority">The priority of the item.</param>
    /// <param name="Id">The ID of the item.</param>
    private sealed record TestItem(int Priority, int Id = 0) : IComparable<TestItem>
    {
        /// <summary>Compares this TestItem with another TestItem based on priority.</summary>
        /// <param name="other">The other TestItem to compare with.</param>
        /// <returns>A value indicating the relative order of the items.</returns>
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
