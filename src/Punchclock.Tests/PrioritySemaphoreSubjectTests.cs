// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

namespace Punchclock.Tests;

/// <summary>Tests for <see cref="PrioritySemaphoreSignal{T}"/>.</summary>
public class PrioritySemaphoreSubjectTests
{
    private const int Two = 2;

    private const int Three = 3;

    /// <summary>Verifies that the constructor creates a subject with the specified max count.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithMaxCount_Succeeds()
    {
        var subject = new PrioritySemaphoreSignal<TestItem>(Two);
        await Assert.That(subject.MaximumCount).IsEqualTo(Two);
    }

    /// <summary>Verifies that the constructor with scheduler creates a subject correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithScheduler_Succeeds()
    {
        var subject = new PrioritySemaphoreSignal<TestItem>(Two, ImmediateSequencer.Instance);
        await Assert.That(subject.MaximumCount).IsEqualTo(Two);
    }

    /// <summary>Verifies that OnNext enqueues items.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnNext_QueuesItems()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSignal<TestItem>(1, ImmediateSequencer.Instance);
            var received = new List<TestItem>();

            using var subscription = subject.Subscribe(received.Add);

            var item1 = new TestItem(1);
            var item2 = new TestItem(Two);

            subject.OnNext(item1);
            subject.Release();

            // ImmediateScheduler executes synchronously
            await Assert.That(received).Count().IsEqualTo(1);
            await Assert.That(received[0]).IsEqualTo(item1);

            subject.OnNext(item2);
            subject.Release();

            // ImmediateScheduler executes synchronously
            await Assert.That(received).Count().IsEqualTo(Two);
        }
    }

    /// <summary>Verifies that items are dispatched based on priority.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnNext_WithDifferentPriorities_DispatchesInPriorityOrder()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSignal<TestItem>(1, ImmediateSequencer.Instance);
            var received = new List<TestItem>();

            using var subscription = subject.Subscribe(received.Add);

            var item1 = new TestItem(5);
            var item2 = new TestItem(1); // Higher priority (lower value)
            var item3 = new TestItem(Three);

            subject.OnNext(item1);
            subject.OnNext(item2);
            subject.OnNext(item3);

            subject.Release();
            subject.Release();
            subject.Release();

            // ImmediateScheduler executes synchronously
            await Assert.That(received).Count().IsEqualTo(Three);
            await Assert.That(received[0]).IsEqualTo(item1); // First one goes through immediately
            await Assert.That(received[1]).IsEqualTo(item2); // Higher priority
            await Assert.That(received[Two]).IsEqualTo(item3);
        }
    }

    /// <summary>Verifies that MaximumCount can be changed dynamically.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task MaximumCount_CanBeChanged()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSignal<TestItem>(1, ImmediateSequencer.Instance);
            await Assert.That(subject.MaximumCount).IsEqualTo(1);

            subject.MaximumCount = Three;
            await Assert.That(subject.MaximumCount).IsEqualTo(Three);
        }
    }

    /// <summary>Verifies that increasing MaximumCount triggers draining of queued items.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task MaximumCount_Increase_TriggersDraining()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSignal<TestItem>(0, ImmediateSequencer.Instance); // Start paused
            var received = new List<TestItem>();

            using var subscription = subject.Subscribe(received.Add);

            subject.OnNext(new TestItem(1));
            subject.OnNext(new TestItem(Two));

            // ImmediateScheduler executes synchronously
            await Assert.That(received).IsEmpty();

            subject.MaximumCount = Two; // Resume

            // ImmediateScheduler executes synchronously
            await Assert.That(received).Count().IsEqualTo(Two);
        }
    }

    /// <summary>Verifies that Release decrements the count and allows new items through.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Release_AllowsNewItemsThrough()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSignal<TestItem>(1, ImmediateSequencer.Instance);
            var received = new List<TestItem>();

            using var subscription = subject.Subscribe(received.Add);

            subject.OnNext(new TestItem(1));
            subject.OnNext(new TestItem(Two));

            // ImmediateScheduler executes synchronously
            await Assert.That(received).Count().IsEqualTo(1);

            subject.Release(); // Allow second item

            // ImmediateScheduler executes synchronously
            await Assert.That(received).Count().IsEqualTo(Two);
        }
    }

    /// <summary>Verifies that OnCompleted drains all remaining items and completes the subject.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnCompleted_DrainsAllItemsAndCompletes()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSignal<TestItem>(1, ImmediateSequencer.Instance);
            var received = new List<TestItem>();
            var completed = false;

            using var subscription = subject.Subscribe(
                received.Add,
                () => completed = true);

            subject.OnNext(new TestItem(1));
            subject.OnNext(new TestItem(Two));
            subject.OnNext(new TestItem(Three));

            subject.OnCompleted();

            // ImmediateScheduler executes synchronously
            await Assert.That(received).Count().IsEqualTo(Three); // All drained
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>Verifies that OnError clears the queue and sends error to subscribers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnError_ClearsQueueAndSendsError()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSignal<TestItem>(1, ImmediateSequencer.Instance);
            var received = new List<TestItem>();
            Exception? error = null;

            using var subscription = subject.Subscribe(
                received.Add,
                ex => error = ex);

            subject.OnNext(new TestItem(1));
            subject.OnNext(new TestItem(Two));

            var expectedException = new InvalidOperationException("test error");
            subject.OnError(expectedException);

            // ImmediateScheduler executes synchronously
            await Assert.That(error).IsEqualTo(expectedException);
        }
    }

    /// <summary>Verifies that OnNext after OnCompleted does nothing.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnNext_AfterOnCompleted_DoesNothing()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSignal<TestItem>(Two, ImmediateSequencer.Instance);
            var received = new List<TestItem>();

            using var subscription = subject.Subscribe(received.Add);

            subject.OnNext(new TestItem(1));
            subject.OnCompleted();

            // ImmediateScheduler executes synchronously
            var countAfterComplete = received.Count;

            subject.OnNext(new TestItem(Two)); // Should be ignored

            // ImmediateScheduler executes synchronously
            await Assert.That(received.Count).IsEqualTo(countAfterComplete);
        }
    }

    /// <summary>Verifies that OnNext after OnError does nothing.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnNext_AfterOnError_DoesNothing()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSignal<TestItem>(Two, ImmediateSequencer.Instance);
            var received = new List<TestItem>();

            using var subscription = subject.Subscribe(
                received.Add,
                _ => { });

            subject.OnNext(new TestItem(1));
            subject.OnError(new InvalidOperationException("error"));

            // ImmediateScheduler executes synchronously
            var countAfterError = received.Count;

            subject.OnNext(new TestItem(Two)); // Should be ignored

            // ImmediateScheduler executes synchronously
            await Assert.That(received.Count).IsEqualTo(countAfterError);
        }
    }

    /// <summary>Verifies that multiple calls to OnCompleted are safe.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnCompleted_CalledTwice_DoesNotThrow()
    {
        var subject = new PrioritySemaphoreSignal<TestItem>(1, ImmediateSequencer.Instance);
        subject.OnCompleted();
        subject.OnCompleted(); // Should not throw
        await Task.CompletedTask;
    }

    /// <summary>
    /// Covers PrioritySemaphoreSubject.cs line 94 - OnNext after completion.
    /// Verifies that calling OnNext after OnCompleted is safely ignored (queue is null).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnNext_AfterCompletion_IsIgnored()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSignal<TestItem>(Two, ImmediateSequencer.Instance);
            var received = new List<TestItem>();

            subject.Subscribe(received.Add);

            subject.OnNext(new TestItem(1));
            subject.OnCompleted();

            // This should hit line 94 - queue is null after completion
            subject.OnNext(new TestItem(Two));

            // ImmediateScheduler executes synchronously
            await Assert.That(received).Count().IsEqualTo(1); // Only the first item
        }
    }

    /// <summary>Test item that is comparable by priority.</summary>
    /// <param name="Priority">The priority of the item.</param>
    private sealed record TestItem(int Priority) : IComparable<TestItem>
    {
        /// <summary>Compares this instance with another TestItem based on priority.</summary>
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
