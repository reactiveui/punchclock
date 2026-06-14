// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

namespace Punchclock.Tests;

/// <summary>Tests for <see cref="ScheduledSignal{T}"/>.</summary>
public class ScheduledSignalTests
{
    private const int One = 1;

    private const int Two = 2;

    private const int Three = 3;

    private const int Four = 4;

    private const int FourtyTwo = 42;

    /// <summary>Verifies that the constructor throws <see cref="ArgumentNullException"/> when scheduler is null.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithNullSequencer_ThrowsArgumentNullException()
    {
        var ex = await Assert.That(() => new ScheduledSignal<int>(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(ex!.ParamName).IsEqualTo("scheduler");
    }

    /// <summary>Verifies that the constructor accepts a sequencer without default observer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithSequencerOnly_Succeeds()
    {
        using var subject = new ScheduledSignal<int>(ImmediateSequencer.Instance);
        await Assert.That(subject).IsNotNull();
    }

    /// <summary>Verifies that the constructor accepts a sequencer with default observer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithSequencerAndDefaultObserver_Succeeds()
    {
        var observer = new TestObserver<int>();
        using var subject = new ScheduledSignal<int>(ImmediateSequencer.Instance, observer);
        await Assert.That(subject).IsNotNull();
    }

    /// <summary>Verifies that Subscribe throws <see cref="ArgumentNullException"/> when observer is null.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Subscribe_WithNullObserver_ThrowsArgumentNullException()
    {
        using var subject = new ScheduledSignal<int>(ImmediateSequencer.Instance);
        var ex = await Assert.That(() => subject.Subscribe(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(ex!.ParamName).IsEqualTo("observer");
    }

    /// <summary>Verifies that OnNext emits values to subscribers on the specified sequencer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnNext_WithSubscriber_EmitsValue()
    {
        using (Assert.Multiple())
        {
            using var subject = new ScheduledSignal<int>(ImmediateSequencer.Instance);
            var observer = new TestObserver<int>();

            using var subscription = subject.Subscribe(observer);
            subject.OnNext(FourtyTwo);

            await AssertValues(observer.Values, FourtyTwo);
            await Assert.That(observer.Completed).IsFalse();
            await Assert.That(observer.Error).IsNull();
        }
    }

    /// <summary>Verifies that OnCompleted completes all subscribers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnCompleted_WithSubscriber_CompletesObserver()
    {
        using (Assert.Multiple())
        {
            using var subject = new ScheduledSignal<int>(ImmediateSequencer.Instance);
            var observer = new TestObserver<int>();

            using var subscription = subject.Subscribe(observer);
            subject.OnNext(One);
            subject.OnCompleted();

            await AssertValues(observer.Values, One);
            await Assert.That(observer.Completed).IsTrue();
            await Assert.That(observer.Error).IsNull();
        }
    }

    /// <summary>Verifies that OnError sends error to all subscribers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnError_WithSubscriber_SendsErrorToObserver()
    {
        using (Assert.Multiple())
        {
            using var subject = new ScheduledSignal<int>(ImmediateSequencer.Instance);
            var observer = new TestObserver<int>();
            var exception = new InvalidOperationException("test error");

            using var subscription = subject.Subscribe(observer);
            subject.OnNext(One);
            subject.OnError(exception);

            await AssertValues(observer.Values, One);
            await Assert.That(observer.Completed).IsFalse();
            await Assert.That(observer.Error).IsEqualTo(exception);
        }
    }

    /// <summary>Verifies that the default observer receives values when no other subscribers are active.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DefaultObserver_WithNoSubscribers_ReceivesValues()
    {
        using (Assert.Multiple())
        {
            var defaultObserver = new TestObserver<int>();
            using var subject = new ScheduledSignal<int>(ImmediateSequencer.Instance, defaultObserver);

            subject.OnNext(One);
            subject.OnNext(Two);

            await AssertValues(defaultObserver.Values, One, Two);
        }
    }

    /// <summary>Verifies that the default observer stops receiving values when a subscriber is active.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DefaultObserver_WithActiveSubscriber_DoesNotReceiveValues()
    {
        using (Assert.Multiple())
        {
            var defaultObserver = new TestObserver<int>();
            using var subject = new ScheduledSignal<int>(ImmediateSequencer.Instance, defaultObserver);
            var subscriber = new TestObserver<int>();

            subject.OnNext(One);
            await AssertValues(defaultObserver.Values, One);

            using var subscription = subject.Subscribe(subscriber);
            subject.OnNext(Two);

            await AssertValues(subscriber.Values, Two);
            await AssertValues(defaultObserver.Values, One); // No new values
        }
    }

    /// <summary>Verifies that the default observer resumes receiving values after all subscribers dispose.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DefaultObserver_AfterSubscriberDispose_ResumesReceivingValues()
    {
        using (Assert.Multiple())
        {
            var defaultObserver = new TestObserver<int>();
            using var subject = new ScheduledSignal<int>(ImmediateSequencer.Instance, defaultObserver);
            var subscriber = new TestObserver<int>();

            subject.OnNext(One);
            await AssertValues(defaultObserver.Values, One);

            var subscription = subject.Subscribe(subscriber);
            subject.OnNext(Two);
            subscription.Dispose();

            subject.OnNext(Three);

            await AssertValues(defaultObserver.Values, One, Three);
            await AssertValues(subscriber.Values, Two);
        }
    }

    /// <summary>Verifies that multiple subscribers can be active simultaneously.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task MultipleSubscribers_ReceiveValues()
    {
        using (Assert.Multiple())
        {
            using var subject = new ScheduledSignal<int>(ImmediateSequencer.Instance);
            var observer1 = new TestObserver<int>();
            var observer2 = new TestObserver<int>();

            using var sub1 = subject.Subscribe(observer1);
            using var sub2 = subject.Subscribe(observer2);

            subject.OnNext(FourtyTwo);

            await AssertValues(observer1.Values, FourtyTwo);
            await AssertValues(observer2.Values, FourtyTwo);
        }
    }

    /// <summary>Verifies that the default observer only resumes when ref count reaches zero.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DefaultObserver_WithMultipleSubscribers_ResumesOnlyWhenAllDispose()
    {
        using (Assert.Multiple())
        {
            var defaultObserver = new TestObserver<int>();
            using var subject = new ScheduledSignal<int>(ImmediateSequencer.Instance, defaultObserver);

            subject.OnNext(One);
            await AssertValues(defaultObserver.Values, One);

            var sub1 = subject.Subscribe(new TestObserver<int>());
            var sub2 = subject.Subscribe(new TestObserver<int>());

            subject.OnNext(Two);
            await AssertValues(defaultObserver.Values, One); // Still paused

            sub1.Dispose();
            subject.OnNext(Three);
            await AssertValues(defaultObserver.Values, One); // Still paused

            sub2.Dispose();
            subject.OnNext(Four);
            await AssertValues(defaultObserver.Values, One, Four); // Resumed
        }
    }

    /// <summary>Verifies that Dispose can be called multiple times safely.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var subject = new ScheduledSignal<int>(ImmediateSequencer.Instance);
        subject.Dispose();
        subject.Dispose(); // Should not throw
        await Task.CompletedTask;
    }

    /// <summary>Asserts that the observed values match a single expected value.</summary>
    /// <param name="values">The observed values.</param>
    /// <param name="first">The expected first value.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous assertion.</returns>
    private static async Task AssertValues(List<int> values, int first)
    {
        await Assert.That(values.Count).IsEqualTo(1);
        await Assert.That(values[0]).IsEqualTo(first);
    }

    /// <summary>Asserts that the observed values match two expected values.</summary>
    /// <param name="values">The observed values.</param>
    /// <param name="first">The expected first value.</param>
    /// <param name="second">The expected second value.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous assertion.</returns>
    private static async Task AssertValues(List<int> values, int first, int second)
    {
        await Assert.That(values.Count).IsEqualTo(Two);
        await Assert.That(values[0]).IsEqualTo(first);
        await Assert.That(values[1]).IsEqualTo(second);
    }

    /// <summary>Test observer that records all events.</summary>
    /// <typeparam name="T">The type of values observed.</typeparam>
    private sealed class TestObserver<T> : IObserver<T>
    {
        /// <summary>Gets the list of values received by the observer.</summary>
        public List<T> Values { get; } = [];

        /// <summary>Gets a value indicating whether the observer has completed.</summary>
        public bool Completed { get; private set; }

        /// <summary>Gets the exception received by the observer, if any.</summary>
        public Exception? Error { get; private set; }

        /// <summary>Called when the observer receives a new value.</summary>
        /// <param name="value">The value received by the observer.</param>
        public void OnNext(T value) => Values.Add(value);

        /// <summary>Called when the observer has completed.</summary>
        public void OnCompleted() => Completed = true;

        /// <summary>Called when the observer receives an error.</summary>
        /// <param name="error">The exception received by the observer.</param>
        public void OnError(Exception error) => Error = error;
    }
}
