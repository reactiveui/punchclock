// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Subjects;

namespace Punchclock.Tests;

/// <summary>
/// Tests for <see cref="ScheduledSubject{T}"/>.
/// </summary>
public class ScheduledSubjectTests
{
    /// <summary>
    /// Verifies that the constructor throws <see cref="ArgumentNullException"/> when scheduler is null.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithNullScheduler_ThrowsArgumentNullException()
    {
        var ex = await Assert.That(() => new ScheduledSubject<int>(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(ex!.ParamName).IsEqualTo("scheduler");
    }

    /// <summary>
    /// Verifies that the constructor accepts a scheduler without default observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithSchedulerOnly_Succeeds()
    {
        using var subject = new ScheduledSubject<int>(ImmediateScheduler.Instance);
        await Assert.That(subject).IsNotNull();
    }

    /// <summary>
    /// Verifies that the constructor accepts a scheduler with default observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithSchedulerAndDefaultObserver_Succeeds()
    {
        var observer = new TestObserver<int>();
        using var subject = new ScheduledSubject<int>(ImmediateScheduler.Instance, observer);
        await Assert.That(subject).IsNotNull();
    }

    /// <summary>
    /// Verifies that Subscribe throws <see cref="ArgumentNullException"/> when observer is null.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Subscribe_WithNullObserver_ThrowsArgumentNullException()
    {
        using var subject = new ScheduledSubject<int>(ImmediateScheduler.Instance);
        var ex = await Assert.That(() => subject.Subscribe(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(ex!.ParamName).IsEqualTo("observer");
    }

    /// <summary>
    /// Verifies that OnNext emits values to subscribers on the specified scheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnNext_WithSubscriber_EmitsValue()
    {
        using (Assert.Multiple())
        {
            using var subject = new ScheduledSubject<int>(ImmediateScheduler.Instance);
            var observer = new TestObserver<int>();

            using var subscription = subject.Subscribe(observer);
            subject.OnNext(42);

            await Assert.That(observer.Values).IsEquivalentTo(new[] { 42 });
            await Assert.That(observer.Completed).IsFalse();
            await Assert.That(observer.Error).IsNull();
        }
    }

    /// <summary>
    /// Verifies that OnCompleted completes all subscribers.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnCompleted_WithSubscriber_CompletesObserver()
    {
        using (Assert.Multiple())
        {
            using var subject = new ScheduledSubject<int>(ImmediateScheduler.Instance);
            var observer = new TestObserver<int>();

            using var subscription = subject.Subscribe(observer);
            subject.OnNext(1);
            subject.OnCompleted();

            await Assert.That(observer.Values).IsEquivalentTo(new[] { 1 });
            await Assert.That(observer.Completed).IsTrue();
            await Assert.That(observer.Error).IsNull();
        }
    }

    /// <summary>
    /// Verifies that OnError sends error to all subscribers.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnError_WithSubscriber_SendsErrorToObserver()
    {
        using (Assert.Multiple())
        {
            using var subject = new ScheduledSubject<int>(ImmediateScheduler.Instance);
            var observer = new TestObserver<int>();
            var exception = new InvalidOperationException("test error");

            using var subscription = subject.Subscribe(observer);
            subject.OnNext(1);
            subject.OnError(exception);

            await Assert.That(observer.Values).IsEquivalentTo(new[] { 1 });
            await Assert.That(observer.Completed).IsFalse();
            await Assert.That(observer.Error).IsEqualTo(exception);
        }
    }

    /// <summary>
    /// Verifies that the default observer receives values when no other subscribers are active.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DefaultObserver_WithNoSubscribers_ReceivesValues()
    {
        using (Assert.Multiple())
        {
            var defaultObserver = new TestObserver<int>();
            using var subject = new ScheduledSubject<int>(ImmediateScheduler.Instance, defaultObserver);

            subject.OnNext(1);
            subject.OnNext(2);

            await Assert.That(defaultObserver.Values).IsEquivalentTo(new[] { 1, 2 });
        }
    }

    /// <summary>
    /// Verifies that the default observer stops receiving values when a subscriber is active.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DefaultObserver_WithActiveSubscriber_DoesNotReceiveValues()
    {
        using (Assert.Multiple())
        {
            var defaultObserver = new TestObserver<int>();
            using var subject = new ScheduledSubject<int>(ImmediateScheduler.Instance, defaultObserver);
            var subscriber = new TestObserver<int>();

            subject.OnNext(1);
            await Assert.That(defaultObserver.Values).IsEquivalentTo(new[] { 1 });

            using var subscription = subject.Subscribe(subscriber);
            subject.OnNext(2);

            await Assert.That(subscriber.Values).IsEquivalentTo(new[] { 2 });
            await Assert.That(defaultObserver.Values).IsEquivalentTo(new[] { 1 }); // No new values
        }
    }

    /// <summary>
    /// Verifies that the default observer resumes receiving values after all subscribers dispose.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DefaultObserver_AfterSubscriberDispose_ResumesReceivingValues()
    {
        using (Assert.Multiple())
        {
            var defaultObserver = new TestObserver<int>();
            using var subject = new ScheduledSubject<int>(ImmediateScheduler.Instance, defaultObserver);
            var subscriber = new TestObserver<int>();

            subject.OnNext(1);
            await Assert.That(defaultObserver.Values).IsEquivalentTo(new[] { 1 });

            var subscription = subject.Subscribe(subscriber);
            subject.OnNext(2);
            subscription.Dispose();

            subject.OnNext(3);

            await Assert.That(defaultObserver.Values).IsEquivalentTo(new[] { 1, 3 });
            await Assert.That(subscriber.Values).IsEquivalentTo(new[] { 2 });
        }
    }

    /// <summary>
    /// Verifies that multiple subscribers can be active simultaneously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task MultipleSubscribers_ReceiveValues()
    {
        using (Assert.Multiple())
        {
            using var subject = new ScheduledSubject<int>(ImmediateScheduler.Instance);
            var observer1 = new TestObserver<int>();
            var observer2 = new TestObserver<int>();

            using var sub1 = subject.Subscribe(observer1);
            using var sub2 = subject.Subscribe(observer2);

            subject.OnNext(42);

            await Assert.That(observer1.Values).IsEquivalentTo(new[] { 42 });
            await Assert.That(observer2.Values).IsEquivalentTo(new[] { 42 });
        }
    }

    /// <summary>
    /// Verifies that the default observer only resumes when ref count reaches zero.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DefaultObserver_WithMultipleSubscribers_ResumesOnlyWhenAllDispose()
    {
        using (Assert.Multiple())
        {
            var defaultObserver = new TestObserver<int>();
            using var subject = new ScheduledSubject<int>(ImmediateScheduler.Instance, defaultObserver);

            subject.OnNext(1);
            await Assert.That(defaultObserver.Values).IsEquivalentTo(new[] { 1 });

            var sub1 = subject.Subscribe(new TestObserver<int>());
            var sub2 = subject.Subscribe(new TestObserver<int>());

            subject.OnNext(2);
            await Assert.That(defaultObserver.Values).IsEquivalentTo(new[] { 1 }); // Still paused

            sub1.Dispose();
            subject.OnNext(3);
            await Assert.That(defaultObserver.Values).IsEquivalentTo(new[] { 1 }); // Still paused

            sub2.Dispose();
            subject.OnNext(4);
            await Assert.That(defaultObserver.Values).IsEquivalentTo(new[] { 1, 4 }); // Resumed
        }
    }

    /// <summary>
    /// Verifies that Dispose can be called multiple times safely.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var subject = new ScheduledSubject<int>(ImmediateScheduler.Instance);
        subject.Dispose();
        subject.Dispose(); // Should not throw
        await Task.CompletedTask;
    }

    /// <summary>
    /// Test observer that records all events.
    /// </summary>
    /// <typeparam name="T">The type of values observed.</typeparam>
    private sealed class TestObserver<T> : IObserver<T>
    {
        public List<T> Values { get; } = [];

        public bool Completed { get; private set; }

        public Exception? Error { get; private set; }

        public void OnNext(T value) => Values.Add(value);

        public void OnCompleted() => Completed = true;

        public void OnError(Exception error) => Error = error;
    }
}
