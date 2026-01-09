// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Punchclock.Tests;

/// <summary>
/// Tests for <see cref="PrioritySemaphoreSubject{T}"/>.
/// </summary>
public class PrioritySemaphoreSubjectTests
{
    /// <summary>
    /// Verifies that the constructor creates a subject with the specified max count.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithMaxCount_Succeeds()
    {
        var subject = new PrioritySemaphoreSubject<TestItem>(2);
        await Assert.That(subject.MaximumCount).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that the constructor with scheduler creates a subject correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithScheduler_Succeeds()
    {
        var subject = new PrioritySemaphoreSubject<TestItem>(2, ImmediateScheduler.Instance);
        await Assert.That(subject.MaximumCount).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that OnNext enqueues items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnNext_QueuesItems()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSubject<TestItem>(1, ImmediateScheduler.Instance);
            var received = new List<TestItem>();

            using var subscription = subject.Subscribe(item => received.Add(item));

            var item1 = new TestItem(1);
            var item2 = new TestItem(2);

            subject.OnNext(item1);
            subject.Release();

            await Task.Delay(50);
            await Assert.That(received).Count().IsEqualTo(1);
            await Assert.That(received[0]).IsEqualTo(item1);

            subject.OnNext(item2);
            subject.Release();

            await Task.Delay(50);
            await Assert.That(received).Count().IsEqualTo(2);
        }
    }

    /// <summary>
    /// Verifies that items are dispatched based on priority.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnNext_WithDifferentPriorities_DispatchesInPriorityOrder()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSubject<TestItem>(1, ImmediateScheduler.Instance);
            var received = new List<TestItem>();

            using var subscription = subject.Subscribe(item => received.Add(item));

            var item1 = new TestItem(5);
            var item2 = new TestItem(1); // Higher priority (lower value)
            var item3 = new TestItem(3);

            subject.OnNext(item1);
            subject.OnNext(item2);
            subject.OnNext(item3);

            subject.Release();
            subject.Release();
            subject.Release();

            await Task.Delay(100);

            await Assert.That(received).Count().IsEqualTo(3);
            await Assert.That(received[0]).IsEqualTo(item1); // First one goes through immediately
            await Assert.That(received[1]).IsEqualTo(item2); // Higher priority
            await Assert.That(received[2]).IsEqualTo(item3);
        }
    }

    /// <summary>
    /// Verifies that MaximumCount can be changed dynamically.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task MaximumCount_CanBeChanged()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSubject<TestItem>(1, ImmediateScheduler.Instance);
            await Assert.That(subject.MaximumCount).IsEqualTo(1);

            subject.MaximumCount = 3;
            await Assert.That(subject.MaximumCount).IsEqualTo(3);
        }
    }

    /// <summary>
    /// Verifies that increasing MaximumCount triggers draining of queued items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task MaximumCount_Increase_TriggersDraining()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSubject<TestItem>(0, ImmediateScheduler.Instance); // Start paused
            var received = new List<TestItem>();

            using var subscription = subject.Subscribe(item => received.Add(item));

            subject.OnNext(new TestItem(1));
            subject.OnNext(new TestItem(2));

            await Task.Delay(50);
            await Assert.That(received).IsEmpty();

            subject.MaximumCount = 2; // Resume

            await Task.Delay(50);
            await Assert.That(received).Count().IsEqualTo(2);
        }
    }

    /// <summary>
    /// Verifies that Release decrements the count and allows new items through.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Release_AllowsNewItemsThrough()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSubject<TestItem>(1, ImmediateScheduler.Instance);
            var received = new List<TestItem>();

            using var subscription = subject.Subscribe(item => received.Add(item));

            subject.OnNext(new TestItem(1));
            subject.OnNext(new TestItem(2));

            await Task.Delay(50);
            await Assert.That(received).Count().IsEqualTo(1);

            subject.Release(); // Allow second item

            await Task.Delay(50);
            await Assert.That(received).Count().IsEqualTo(2);
        }
    }

    /// <summary>
    /// Verifies that OnCompleted drains all remaining items and completes the subject.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnCompleted_DrainsAllItemsAndCompletes()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSubject<TestItem>(1, ImmediateScheduler.Instance);
            var received = new List<TestItem>();
            var completed = false;

            using var subscription = subject.Subscribe(
                item => received.Add(item),
                () => completed = true);

            subject.OnNext(new TestItem(1));
            subject.OnNext(new TestItem(2));
            subject.OnNext(new TestItem(3));

            subject.OnCompleted();

            await Task.Delay(100);
            await Assert.That(received).Count().IsEqualTo(3); // All drained
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that OnError clears the queue and sends error to subscribers.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnError_ClearsQueueAndSendsError()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSubject<TestItem>(1, ImmediateScheduler.Instance);
            var received = new List<TestItem>();
            Exception? error = null;

            using var subscription = subject.Subscribe(
                item => received.Add(item),
                ex => error = ex);

            subject.OnNext(new TestItem(1));
            subject.OnNext(new TestItem(2));

            var expectedException = new InvalidOperationException("test error");
            subject.OnError(expectedException);

            await Task.Delay(50);
            await Assert.That(error).IsEqualTo(expectedException);
        }
    }

    /// <summary>
    /// Verifies that OnNext after OnCompleted does nothing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnNext_AfterOnCompleted_DoesNothing()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSubject<TestItem>(2, ImmediateScheduler.Instance);
            var received = new List<TestItem>();

            using var subscription = subject.Subscribe(item => received.Add(item));

            subject.OnNext(new TestItem(1));
            subject.OnCompleted();

            await Task.Delay(50);
            var countAfterComplete = received.Count;

            subject.OnNext(new TestItem(2)); // Should be ignored

            await Task.Delay(50);
            await Assert.That(received.Count).IsEqualTo(countAfterComplete);
        }
    }

    /// <summary>
    /// Verifies that OnNext after OnError does nothing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnNext_AfterOnError_DoesNothing()
    {
        using (Assert.Multiple())
        {
            var subject = new PrioritySemaphoreSubject<TestItem>(2, ImmediateScheduler.Instance);
            var received = new List<TestItem>();

            using var subscription = subject.Subscribe(
                item => received.Add(item),
                _ => { });

            subject.OnNext(new TestItem(1));
            subject.OnError(new InvalidOperationException("error"));

            await Task.Delay(50);
            var countAfterError = received.Count;

            subject.OnNext(new TestItem(2)); // Should be ignored

            await Task.Delay(50);
            await Assert.That(received.Count).IsEqualTo(countAfterError);
        }
    }

    /// <summary>
    /// Verifies that multiple calls to OnCompleted are safe.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnCompleted_CalledTwice_DoesNotThrow()
    {
        var subject = new PrioritySemaphoreSubject<TestItem>(1, ImmediateScheduler.Instance);
        subject.OnCompleted();
        subject.OnCompleted(); // Should not throw
        await Task.CompletedTask;
    }

    /// <summary>
    /// Test item that is comparable by priority.
    /// </summary>
    private sealed record TestItem(int Priority) : IComparable<TestItem>
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
