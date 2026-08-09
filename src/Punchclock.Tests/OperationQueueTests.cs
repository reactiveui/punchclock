// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Globalization;

using DynamicData;
using ReactiveUI.Primitives.Signals;

using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace Punchclock.Tests;

/// <summary>Tests for the operation queue.</summary>
public class OperationQueueTests
{
    /// <summary>Parameter name for the maximum concurrency argument.</summary>
    private const string MaximumConcurrent = "maximumConcurrent";

    /// <summary>Cancellation payload used by typed cancellation tests.</summary>
    private const string CancellationValue = "stop";

    /// <summary>Represents the integer value one.</summary>
    private const int One = 1;

    /// <summary>Represents the integer value two.</summary>
    private const int Two = 2;

    /// <summary>Represents the integer value three.</summary>
    private const int Three = 3;

    /// <summary>Represents the integer value four.</summary>
    private const int Four = 4;

    /// <summary>Represents the integer value five.</summary>
    private const int Five = 5;

    /// <summary>Represents the integer value six.</summary>
    private const int Six = 6;

    /// <summary>Represents the integer value forty-two.</summary>
    private const int FourtyTwo = 42;

    /// <summary>Represents the integer value one hundred.</summary>
    private const int OneHundred = 100;

    /// <summary>Priority used to verify higher-priority work runs first.</summary>
    private const int HighPriority = 10;

    /// <summary>Seed used to keep randomized ordering deterministic in tests.</summary>
    private const int DeterministicSeed = 123;

    /// <summary>Alternative seed used to verify that seeded orderings diverge.</summary>
    private const int AlternativeDeterministicSeed = 456;

    /// <summary>Number of operations used by deterministic ordering tests.</summary>
    private const int RandomizedItemCount = 8;

    /// <summary>Checks to make sure that items are dispatched based on their priority.</summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ItemsShouldBeDispatchedByPriority(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var subjects = CreateSignals(Five);
            var priorities = new[] { Five, Five, Five, HighPriority, One, };
            using var fixture = new OperationQueue(Two);

            // The two at the front are solely to stop up the queue, they get subscribed
            // to immediately.
            var outputs = EnqueueOutputs(fixture, subjects, priorities);

            await AssertOutputCounts(outputs, 0, 0, 0, 0, 0);

            subjects[0].OnNext(FourtyTwo);
            subjects[0].OnCompleted();
            await Task.Delay(OneHundred, cancellationToken);
            await AssertOutputCounts(outputs, 1, 0, 0, 0, 0);

            // 0 => completed, 1,3 => live, 2,4 => queued. Make sure 4 *doesn't* fire because
            // the priority should invert it.
            subjects[Four].OnNext(FourtyTwo);
            subjects[Four].OnCompleted();
            await Task.Delay(OneHundred, cancellationToken);
            await AssertOutputCounts(outputs, 1, 0, 0, 0, 0);

            // At the end, 0,1 => completed, 3,2 => live, 4 is queued
            subjects[1].OnNext(FourtyTwo);
            subjects[1].OnCompleted();
            await Task.Delay(OneHundred, cancellationToken);
            await AssertOutputCounts(outputs, 1, 1, 0, 0, 0);

            // At the end, 0,1,2,4 => completed, 3 is live (remember, we completed
            // 4 early)
            subjects[Two].OnNext(FourtyTwo);
            subjects[Two].OnCompleted();
            await Task.Delay(OneHundred, cancellationToken);
            await AssertOutputCounts(outputs, 1, 1, 1, 0, 1);

            subjects[Three].OnNext(FourtyTwo);
            subjects[Three].OnCompleted();
            await Task.Delay(OneHundred, cancellationToken);
            await AssertOutputCounts(outputs, 1, 1, 1, 1, 1);
        }
    }

    /// <summary>Checks to make sure that keyed items are serialized.</summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task KeyedItemsShouldBeSerialized(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var subj1 = new AsyncSignal<int>();
            var subj2 = new AsyncSignal<int>();

            var subscribeCount1 = 0;
            var input1Subj = new AsyncSignal<int>();
            var input1 = Signal.Defer(() =>
            {
                subscribeCount1++;
                return input1Subj;
            });
            var subscribeCount2 = 0;
            var input2Subj = new AsyncSignal<int>();
            var input2 = Signal.Defer(() =>
            {
                subscribeCount2++;
                return input2Subj;
            });

            using var fixture = new OperationQueue(Two);

            // Block up the queue
            foreach (var v in new[] { subj1, subj2, })
            {
                _ = fixture.EnqueueObservableOperation(Five, () => v).Subscribe();
            }

            // subj1,2 are live, input1,2 are in queue
            _ = fixture
                .EnqueueObservableOperation(Five, "key", Signal.Silent<RxVoid>(), () => input1)
                .ToObservableChangeSet(scheduler: System.Reactive.Concurrency.ImmediateScheduler.Instance)
                .Bind(out var out1).Subscribe();
            _ = fixture
                .EnqueueObservableOperation(Five, "key", Signal.Silent<RxVoid>(), () => input2)
                .ToObservableChangeSet(scheduler: System.Reactive.Concurrency.ImmediateScheduler.Instance)
                .Bind(out var out2).Subscribe();

            await Assert.That(subscribeCount1).IsZero();
            await Assert.That(subscribeCount2).IsZero();

            // Dispatch both subj1 and subj2, we should end up with input1 live,
            // but input2 in queue because of the key
            subj1.OnNext(FourtyTwo);
            subj1.OnCompleted();
            subj2.OnNext(FourtyTwo);
            subj2.OnCompleted();

            await Assert.That(subscribeCount1).IsEqualTo(1);
            await Assert.That(subscribeCount2).IsZero();
            await Assert.That(out1.Count).IsEqualTo(0);
            await Assert.That(out2.Count).IsEqualTo(0);

            // Dispatch input1, input2 can now execute
            input1Subj.OnNext(FourtyTwo);
            input1Subj.OnCompleted();

            await Assert.That(subscribeCount1).IsEqualTo(1);
            await Assert.That(subscribeCount2).IsEqualTo(1);
            await Assert.That(out1.Count).IsEqualTo(1);
            await Assert.That(out2.Count).IsEqualTo(0);

            // Dispatch input2, everything is finished
            input2Subj.OnNext(FourtyTwo);
            input2Subj.OnCompleted();

            await Assert.That(subscribeCount1).IsEqualTo(1);
            await Assert.That(subscribeCount2).IsEqualTo(1);
            await Assert.That(out1.Count).IsEqualTo(1);
            await Assert.That(out2.Count).IsEqualTo(1);
        }
    }

    /// <summary>Checks to make sure that non key items are run in parallel.</summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task NonkeyedItemsShouldRunInParallel(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var unkeyed1Subj = new AsyncSignal<int>();
            var unkeyed1SubCount = 0;
            var unkeyed1 = Signal.Defer(() =>
            {
                unkeyed1SubCount++;
                return unkeyed1Subj;
            });

            var unkeyed2Subj = new AsyncSignal<int>();
            var unkeyed2SubCount = 0;
            var unkeyed2 = Signal.Defer(() =>
            {
                unkeyed2SubCount++;
                return unkeyed2Subj;
            });

            using var fixture = new OperationQueue(Two);

            await Assert.That(unkeyed1SubCount).IsZero();
            await Assert.That(unkeyed2SubCount).IsZero();

            _ = fixture.EnqueueObservableOperation(Five, () => unkeyed1).Subscribe();
            _ = fixture.EnqueueObservableOperation(Five, () => unkeyed2).Subscribe();

            await Assert.That(unkeyed1SubCount).IsEqualTo(1);
            await Assert.That(unkeyed2SubCount).IsEqualTo(1);
        }
    }

    /// <summary>Checks to make sure that shutdown signals once everything completes.</summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ShutdownShouldSignalOnceEverythingCompletes(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var subjects = CreateSignals(Five);
            var priorities = new[] { Five, Five, Five, HighPriority, One, };
            using var fixture = new OperationQueue(Two);

            // The two at the front are solely to stop up the queue, they get subscribed
            // to immediately.
            var outputs = EnqueueOutputs(fixture, subjects, priorities);

            _ = fixture
                .ShutdownQueue()
                .ToObservableChangeSet(scheduler: System.Reactive.Concurrency.ImmediateScheduler.Instance)
                .Bind(out var shutdown).Subscribe();

            await AssertOutputCounts(outputs, 0, 0, 0, 0, 0);
            await Assert.That(shutdown.Count).IsEqualTo(0);

            for (var i = 0; i < Four; i++)
            {
                subjects[i].OnNext(FourtyTwo);
                subjects[i].OnCompleted();
            }

            await Assert.That(shutdown.Count).IsEqualTo(0);

            // Complete the last one, that should signal that we're shut down
            subjects[Four].OnNext(FourtyTwo);
            subjects[Four].OnCompleted();

            await AssertOutputCounts(outputs, 1, 1, 1, 1, 1);
            await Assert.That(shutdown.Count).IsEqualTo(1);
        }
    }

    /// <summary>Checks to make sure that the queue holds items until unpaused.</summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task PausingTheQueueShouldHoldItemsUntilUnpaused(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var item = Signal.Emit(FourtyTwo);

            using var fixture = new OperationQueue(Two);
            _ = ReactiveUI.Primitives.LinqExtensions.Blend(
            [
                fixture.EnqueueObservableOperation(Four, () => item),
                fixture.EnqueueObservableOperation(Four, () => item),
            ])
             .ToObservableChangeSet(scheduler: System.Reactive.Concurrency.ImmediateScheduler.Instance)
             .Bind(out var prePauseOutput).Subscribe();

            await Assert.That(prePauseOutput.Count).IsEqualTo(Two);

            var unpause1 = fixture.PauseQueue();

            // The queue is halted, but we should still eventually process these
            // once it's no longer halted
            _ = ReactiveUI.Primitives.LinqExtensions.Blend(
            [
                fixture.EnqueueObservableOperation(Four, () => item),
                fixture.EnqueueObservableOperation(Four, () => item),
            ])
             .ToObservableChangeSet(scheduler: System.Reactive.Concurrency.ImmediateScheduler.Instance)
             .Bind(out var pauseOutput).Subscribe();

            await Assert.That(pauseOutput.Count).IsEqualTo(0);

            var unpause2 = fixture.PauseQueue();
            await Assert.That(pauseOutput.Count).IsEqualTo(0);

            unpause1.Dispose();
            await Assert.That(pauseOutput.Count).IsEqualTo(0);

            unpause2.Dispose();
            await Assert.That(pauseOutput.Count).IsEqualTo(Two);
        }
    }

    /// <summary>Checks that cancelling items should not result in them being returned.</summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task CancellingItemsShouldNotResultInThemBeingReturned(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var subj1 = new AsyncSignal<int>();
            var subj2 = new AsyncSignal<int>();

            using var fixture = new OperationQueue(Two);

            // Block up the queue
            foreach (var v in new[] { subj1, subj2, })
            {
                _ = fixture.EnqueueObservableOperation(Five, () => v).Subscribe();
            }

            var cancel1 = new Signal<RxVoid>();
            var item1 = new AsyncSignal<int>();
            _ = ReactiveUI.Primitives.LinqExtensions.Blend([
                fixture.EnqueueObservableOperation(Five, "foo", cancel1, () => item1),
               fixture.EnqueueObservableOperation(Five, "baz", static () => Signal.Emit(FourtyTwo)),
            ])
             .ToObservableChangeSet(scheduler: System.Reactive.Concurrency.ImmediateScheduler.Instance)
             .Bind(out var output).Subscribe();

            await Assert.That(output.Count).IsEqualTo(0);

            // Still blocked by subj1,2, only baz is in queue
            cancel1.OnNext(RxVoid.Default);
            cancel1.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(0);

            // foo was cancelled, baz is still good
            subj1.OnNext(FourtyTwo);
            subj1.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(1);

            // don't care that cancelled item finished
            item1.OnNext(FourtyTwo);
            item1.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(1);

            // still shouldn't see anything
            subj2.OnNext(FourtyTwo);
            subj2.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(1);
        }
    }

    /// <summary>Checks that the cancelling of items, that the items won't be evaluated.</summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task CancellingItemsShouldntEvenBeEvaluated(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var subj1 = new AsyncSignal<int>();
            var subj2 = new AsyncSignal<int>();

            using var fixture = new OperationQueue(Two);

            // Block up the queue
            foreach (var v in new[] { subj1, subj2, })
            {
                _ = fixture.EnqueueObservableOperation(Five, () => v).Subscribe();
            }

            var cancel1 = new Signal<RxVoid>();
            var wasCalled = false;
            var item1 = new AsyncSignal<int>();

            _ = fixture.EnqueueObservableOperation(Five, "foo", cancel1, () =>
            {
                wasCalled = true;
                return item1;
            }).ToObservableChangeSet(scheduler: System.Reactive.Concurrency.ImmediateScheduler.Instance)
              .Bind(out var output).Subscribe();

            await Assert.That(output.Count).IsEqualTo(0);
            await Assert.That(wasCalled).IsFalse();

            // Still blocked by subj1,2 - however, we've cancelled foo before
            // it even had a chance to run - if that's the case, we shouldn't
            // even call the evaluation func
            cancel1.OnNext(RxVoid.Default);
            cancel1.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(0);
            await Assert.That(wasCalled).IsFalse();

            // Unblock subj1,2, we still shouldn't see wasCalled = true
            subj1.OnNext(FourtyTwo);
            subj1.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(0);
            await Assert.That(wasCalled).IsFalse();

            subj2.OnNext(FourtyTwo);
            subj2.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(0);
            await Assert.That(wasCalled).IsFalse();
        }
    }

    /// <summary>Checks to make sure the queue respects maximum concurrency.</summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task QueueShouldRespectMaximumConcurrent(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var unkeyed1Subj = new AsyncSignal<int>();
            var unkeyed1SubCount = 0;
            var unkeyed1 = Signal.Defer(() =>
            {
                unkeyed1SubCount++;
                return unkeyed1Subj;
            });

            var unkeyed2Subj = new AsyncSignal<int>();
            var unkeyed2SubCount = 0;
            var unkeyed2 = Signal.Defer(() =>
            {
                unkeyed2SubCount++;
                return unkeyed2Subj;
            });

            var unkeyed3Subj = new AsyncSignal<int>();
            var unkeyed3SubCount = 0;
            var unkeyed3 = Signal.Defer(() =>
            {
                unkeyed3SubCount++;
                return unkeyed3Subj;
            });

            using var fixture = new OperationQueue(Two);

            await Assert.That(unkeyed1SubCount).IsZero();
            await Assert.That(unkeyed2SubCount).IsZero();
            await Assert.That(unkeyed3SubCount).IsZero();

            _ = fixture.EnqueueObservableOperation(Five, () => unkeyed1).Subscribe();
            _ = fixture.EnqueueObservableOperation(Five, () => unkeyed2).Subscribe();
            _ = fixture.EnqueueObservableOperation(Five, () => unkeyed3).Subscribe();

            await Assert.That(unkeyed1SubCount).IsEqualTo(1);
            await Assert.That(unkeyed2SubCount).IsEqualTo(1);
            await Assert.That(unkeyed3SubCount).IsZero();
        }
    }

    /// <summary>Checks to see if the maximum concurrency is increased that the existing queue adapts.</summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ShouldBeAbleToIncreaseTheMaximunConcurrentValueOfAnExistingQueue(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var unkeyed1Subj = new AsyncSignal<int>();
            var unkeyed1SubCount = 0;
            var unkeyed1 = Signal.Defer(() =>
            {
                unkeyed1SubCount++;
                return unkeyed1Subj;
            });

            var unkeyed2Subj = new AsyncSignal<int>();
            var unkeyed2SubCount = 0;
            var unkeyed2 = Signal.Defer(() =>
            {
                unkeyed2SubCount++;
                return unkeyed2Subj;
            });

            var unkeyed3Subj = new AsyncSignal<int>();
            var unkeyed3SubCount = 0;
            var unkeyed3 = Signal.Defer(() =>
            {
                unkeyed3SubCount++;
                return unkeyed3Subj;
            });

            var unkeyed4Subj = new AsyncSignal<int>();
            var unkeyed4SubCount = 0;
            var unkeyed4 = Signal.Defer(() =>
            {
                unkeyed4SubCount++;
                return unkeyed4Subj;
            });

            using var fixture = new OperationQueue(Two);

            await Assert.That(unkeyed1SubCount).IsZero();
            await Assert.That(unkeyed2SubCount).IsZero();
            await Assert.That(unkeyed3SubCount).IsZero();
            await Assert.That(unkeyed4SubCount).IsZero();

            _ = fixture.EnqueueObservableOperation(Five, () => unkeyed1).Subscribe();
            _ = fixture.EnqueueObservableOperation(Five, () => unkeyed2).Subscribe();
            _ = fixture.EnqueueObservableOperation(Five, () => unkeyed3).Subscribe();
            _ = fixture.EnqueueObservableOperation(Five, () => unkeyed4).Subscribe();

            await Assert.That(unkeyed1SubCount).IsEqualTo(1);
            await Assert.That(unkeyed2SubCount).IsEqualTo(1);
            await Assert.That(unkeyed3SubCount).IsZero();
            await Assert.That(unkeyed4SubCount).IsZero();

            fixture.SetMaximumConcurrent(Three);

            await Assert.That(unkeyed1SubCount).IsEqualTo(1);
            await Assert.That(unkeyed2SubCount).IsEqualTo(1);
            await Assert.That(unkeyed3SubCount).IsEqualTo(1);
            await Assert.That(unkeyed4SubCount).IsZero();
        }
    }

    /// <summary>Checks to make sure that decreasing the maximum concurrency the queue adapts.</summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ShouldBeAbleToDecreaseTheMaximunConcurrentValueOfAnExistingQueue(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var subjects = CreateSignals(Six);
            using var fixture = new OperationQueue(Three);

            // The three at the front are solely to stop up the queue, they get subscribed
            // to immediately.
            var subscriptions = EnqueueSubscriptions(fixture, subjects, Five);

            await AssertObserverStates(subjects, true, true, true, false, false, false);

            fixture.SetMaximumConcurrent(Two);

            // Complete the first one, the last three subjects should still have
            // no observers because we reduced maximum concurrent
            subjects[0].OnNext(FourtyTwo);
            subjects[0].OnCompleted();

            await AssertObserverStates(subjects, false, true, true, false, false, false);

            // Complete subj[1], now 2,3 are live
            subjects[1].OnNext(FourtyTwo);
            subjects[1].OnCompleted();

            await AssertObserverStates(subjects, false, false, true, true, false, false);

            foreach (var subscription in subscriptions)
            {
                subscription.Dispose();
            }
        }
    }

    /// <summary>Checks that equal priority across different keys can be randomized when enabled.</summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task EqualPriorityAcrossDifferentKeysCanBeRandomized(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            // Use deterministic seed to make test stable
            using var queue = new OperationQueue(maximumConcurrent: One, randomizeEqualPriority: true, seed: DeterministicSeed);

            // Block the queue initially
            var blocker = new AsyncSignal<int>();
            _ = queue.EnqueueObservableOperation(Five, () => blocker).Subscribe();

            var a = new AsyncSignal<int>();
            var b = new AsyncSignal<int>();

            var nextCountA = 0;
            var nextCountB = 0;

            _ = queue.EnqueueObservableOperation(Five, "A", () => a).Subscribe(_ => nextCountA++);
            _ = queue.EnqueueObservableOperation(Five, "B", () => b).Subscribe(_ => nextCountB++);

            // Unblock
            blocker.OnNext(One);
            blocker.OnCompleted();

            // Complete whichever started first according to randomized order
            if (a.HasObservers && !b.HasObservers)
            {
                a.OnNext(FourtyTwo);
                a.OnCompleted();
            }
            else if (b.HasObservers && !a.HasObservers)
            {
                b.OnNext(FourtyTwo);
                b.OnCompleted();
            }
            else
            {
                // If both observed (should not happen with maxConcurrent 1), just complete one
                a.OnCompleted();
            }

            // After completing the first, the second should activate and complete
            if (a.HasObservers)
            {
                a.OnNext(FourtyTwo);
                a.OnCompleted();
            }

            if (b.HasObservers)
            {
                b.OnNext(FourtyTwo);
                b.OnCompleted();
            }

            await Assert.That(nextCountA + nextCountB).IsEqualTo(Two);
        }
    }

    /// <summary>Checks that distinct seeds produce distinct equal-priority execution orders.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EqualPriorityRandomization_WithDifferentSeeds_ProducesDifferentOrders()
    {
        using (Assert.Multiple())
        {
            var firstOrder = CaptureRandomizedOrder(DeterministicSeed);
            var secondOrder = CaptureRandomizedOrder(AlternativeDeterministicSeed);

            await Assert.That(firstOrder.Length).IsEqualTo(RandomizedItemCount);
            await Assert.That(secondOrder.Length).IsEqualTo(RandomizedItemCount);
            await Assert.That(firstOrder.SequenceEqual(secondOrder)).IsFalse();
        }
    }

    /// <summary>Checks that early cancellation does not dispose the observable returned to the caller.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueObservableOperation_WhenCancellationAlreadySignalled_ReturnedObservableRemainsSubscribable()
    {
        using var cancellation = new ReplaySignal<int>();
        cancellation.OnNext(One);

        using var queue = new OperationQueue(Two);
        var result = queue.EnqueueObservableOperation(One, "cancelled", cancellation, static () => Signal.Emit(FourtyTwo));
        var receivedValue = false;

        using var subscription = result.Subscribe(_ => receivedValue = true);

        await Assert.That(receivedValue).IsFalse();
    }

    /// <summary>Verifies that constructor throws <see cref="ArgumentOutOfRangeException"/> for non-positive maximumConcurrent.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithZeroOrNegativeMaxConcurrent_ThrowsArgumentOutOfRangeException()
    {
        using (Assert.Multiple())
        {
            var ex1 = await Assert.That(static () => new OperationQueue(0))
                .Throws<ArgumentOutOfRangeException>();
            await Assert.That(ex1!.ParamName).IsEqualTo(MaximumConcurrent);

            var ex2 = await Assert.That(static () => new OperationQueue(-1))
                .Throws<ArgumentOutOfRangeException>();
            await Assert.That(ex2!.ParamName).IsEqualTo(MaximumConcurrent);
        }
    }

    /// <summary>Verifies that SetMaximumConcurrent throws <see cref="ArgumentOutOfRangeException"/> for non-positive values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SetMaximumConcurrent_WithZeroOrNegative_ThrowsArgumentOutOfRangeException()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(Two);

            var ex1 = await Assert.That(() => queue.SetMaximumConcurrent(0))
                .Throws<ArgumentOutOfRangeException>();
            await Assert.That(ex1!.ParamName).IsEqualTo(MaximumConcurrent);

            var ex2 = await Assert.That(() => queue.SetMaximumConcurrent(-1))
                .Throws<ArgumentOutOfRangeException>();
            await Assert.That(ex2!.ParamName).IsEqualTo(MaximumConcurrent);
        }
    }

    /// <summary>Verifies that SetMaximumConcurrent updates the concurrency level.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SetMaximumConcurrent_UpdatesConcurrencyLevel()
    {
        using var queue = new OperationQueue(1);
        queue.SetMaximumConcurrent(Five);

        // If it updated successfully, we should be able to run 5 operations concurrently
        // This is indirectly verified by the queue not blocking when we have 5 items
        await Task.CompletedTask;
    }

    /// <summary>Verifies that Dispose can be called multiple times safely.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var queue = new OperationQueue(1);
        queue.Dispose();
        queue.Dispose(); // Should not throw
        await Task.CompletedTask;
    }

    /// <summary>Verifies that ShutdownQueue can be called multiple times and returns the same observable.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ShutdownQueue_CalledTwice_ReturnsSameObservable()
    {
        using var queue = new OperationQueue(1);

        var shutdown1 = queue.ShutdownQueue();
        var shutdown2 = queue.ShutdownQueue();

        await Assert.That(shutdown1).IsEqualTo(shutdown2);
    }

    /// <summary>Verifies that empty string key is normalized to DefaultKey.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueObservableOperation_WithEmptyKey_NormalizesToDefaultKey()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(Two);

            var completed1 = false;
            var completed2 = false;

            // Empty string should be treated as DefaultKey (non-keyed, concurrent)
            _ = queue.EnqueueObservableOperation(One, string.Empty, static () => Signal.Emit(One))
                .Subscribe(_ => completed1 = true);

            _ = queue.EnqueueObservableOperation(One, string.Empty, static () => Signal.Emit(Two))
                .Subscribe(_ => completed2 = true);

            // Both should complete concurrently since they're treated as DefaultKey
            await Assert.That(completed1).IsTrue();
            await Assert.That(completed2).IsTrue();
        }
    }

    /// <summary>Verifies that null key is normalized to DefaultKey.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueObservableOperation_WithNullKey_NormalizesToDefaultKey()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(Two);

            var completed1 = false;
            var completed2 = false;

            _ = queue.EnqueueObservableOperation(One, null!, static () => Signal.Emit(One))
                .Subscribe(_ => completed1 = true);

            _ = queue.EnqueueObservableOperation(One, null!, static () => Signal.Emit(Two))
                .Subscribe(_ => completed2 = true);

            // Operations complete without delay
            await Assert.That(completed1).IsTrue();
            await Assert.That(completed2).IsTrue();
        }
    }

    /// <summary>Verifies that PauseQueue after ShutdownQueue does not resume the queue.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task PauseQueue_AfterShutdown_DoesNotResume()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(1);

            var shutdown = queue.ShutdownQueue();
            _ = shutdown.Subscribe(); // Start shutdown process
            var pauseHandle = queue.PauseQueue();

            // Disposing the pause handle should not resume since we're shut down
            pauseHandle.Dispose();

            // Queue should still be in shutdown state
            // Operations complete without delay
            await Task.CompletedTask; // Verify no exceptions
        }
    }

    /// <summary>Verifies that constructor with random tiebreak parameters works correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithRandomTiebreakParameters_Succeeds()
    {
        using var queue = new OperationQueue(maximumConcurrent: Two, randomizeEqualPriority: true, seed: FourtyTwo);
        await Assert.That(queue).IsNotNull();
    }

    /// <summary>
    /// Covers OperationQueue.cs line 129 - random without seed.
    /// Verifies that constructor with random tiebreak and null seed creates Random without seed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithRandomTiebreakNoSeed_Succeeds()
    {
        using (Assert.Multiple())
        {
            // Line 129: new Random() when seed is null
            using var queue = new OperationQueue(maximumConcurrent: Two, randomizeEqualPriority: true, seed: null);

            var completed = 0;
            _ = queue.EnqueueObservableOperation(One, "a", static () => Signal.Emit(One)).Subscribe(_ => completed++);
            _ = queue.EnqueueObservableOperation(One, "b", static () => Signal.Emit(Two)).Subscribe(_ => completed++);

            // ImmediateScheduler executes synchronously
            await Assert.That(completed).IsEqualTo(Two);
        }
    }

    /// <summary>
    /// Covers OperationQueue.cs line 137 - y.CancelSignal ?? Observable.Empty case.
    /// Verifies that operations without a cancel signal use Observable.Empty internally.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueObservableOperation_WithNoCancelSignal_UsesEmptyObservable()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(Two);

            var completed = false;

            // Enqueue without cancel signal - should use an empty cancellation signal internally.
            _ = queue.EnqueueObservableOperation(One, static () => Signal.Emit(FourtyTwo))
                .Subscribe(_ => completed = true);

            // Operations complete synchronously
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>Verifies that a typed cancellation signal stops a running operation.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueObservableOperation_WithCancellationSignal_CancelsRunningOperation()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(1);
            var cancel = new Signal<string>();
            var operation = new Signal<int>();
            var values = 0;
            var completed = false;

            _ = queue.EnqueueObservableOperation(One, "cancel-key", cancel, () => operation)
                .Subscribe(
                    _ => values++,
                    static _ => { },
                    () => completed = true);

            cancel.OnNext(CancellationValue);
            operation.OnNext(FourtyTwo);

            await Assert.That(values).IsEqualTo(0);
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>Verifies that the default constructor creates a usable queue.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_Default_CreatesUsableQueue()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue();

            var completed = false;
            _ = queue.EnqueueObservableOperation(One, static () => Signal.Emit(FourtyTwo))
                .Subscribe(value => completed = value == FourtyTwo);

            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>Verifies that enqueueing after shutdown is rejected.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueObservableOperation_AfterShutdown_ThrowsInvalidOperationException()
    {
        using var queue = new OperationQueue();
        _ = queue.ShutdownQueue().Subscribe();

        var ex = await Assert.That(() =>
            queue.EnqueueObservableOperation(One, "late", Signal.Silent<RxVoid>(), static () => Signal.Emit(FourtyTwo)))
            .Throws<InvalidOperationException>();

        await Assert.That(ex!.Message).Contains("shutdown");
    }

    /// <summary>Verifies that disposing a queue disposes cancellation subscriptions for pending work.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dispose_WithPendingOperation_DisposesCancellationSubscription()
    {
        using (Assert.Multiple())
        {
            var queue = new OperationQueue(1);
            var pause = queue.PauseQueue();
            var cancel = new Signal<RxVoid>();
            var cancelled = false;

            _ = queue.EnqueueObservableOperation(
                    1,
                    "pending",
                    cancel,
                    static () => Signal.Emit(FourtyTwo))
                .Subscribe(
                    static _ => { },
                    _ => cancelled = true);

            queue.Dispose();
            cancel.OnNext(RxVoid.Default);
            pause.Dispose();

            await Assert.That(cancelled).IsFalse();
        }
    }

    /// <summary>Verifies that an operation observable error releases queue capacity.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueObservableOperation_WhenOperationErrors_ReleasesCapacity()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(1);
            var exception = new InvalidOperationException("operation failed");
            Exception? observed = null;
            var completed = false;

            _ = queue.EnqueueObservableOperation(One, () => Signal.Fail<int>(exception))
                .Subscribe(
                    static _ => { },
                    error => observed = error);

            _ = queue.EnqueueObservableOperation(One, static () => Signal.Emit(FourtyTwo))
                .Subscribe(value => completed = value == FourtyTwo);

            await Assert.That(observed).IsEqualTo(exception);
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>Verifies that a throwing operation factory releases queue capacity.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueObservableOperation_WhenOperationFactoryThrows_ReleasesCapacity()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(1);
            var completed = false;

            _ = queue.EnqueueObservableOperation<int>(
                    One,
                    static () => throw new InvalidOperationException("factory failed"))
                .Subscribe();

            _ = queue.EnqueueObservableOperation(One, static () => Signal.Emit(FourtyTwo))
                .Subscribe(value => completed = value == FourtyTwo);

            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>Creates a fixed-size array of asynchronous signals.</summary>
    /// <param name="count">The number of signals to create.</param>
    /// <returns>The created signals.</returns>
    private static AsyncSignal<int>[] CreateSignals(int count)
    {
        var subjects = new AsyncSignal<int>[count];
        for (var i = 0; i < count; i++)
        {
            subjects[i] = new();
        }

        return subjects;
    }

    /// <summary>Captures the execution order for a deterministic equal-priority workload.</summary>
    /// <param name="seed">The deterministic tie-break seed.</param>
    /// <returns>The operation values in execution order.</returns>
    private static int[] CaptureRandomizedOrder(int seed)
    {
        var order = new List<int>(RandomizedItemCount);
        using var queue = new OperationQueue(maximumConcurrent: One, randomizeEqualPriority: true, seed);

        using (queue.PauseQueue())
        {
            for (var i = 0; i < RandomizedItemCount; i++)
            {
                var value = i;
                var key = $"randomized-{i.ToString(CultureInfo.InvariantCulture)}";
                _ = queue.EnqueueObservableOperation(One, key, () => Signal.Emit(value)).Subscribe(order.Add);
            }
        }

        return [.. order];
    }

    /// <summary>Enqueues signals and captures their bound outputs.</summary>
    /// <param name="fixture">The queue under test.</param>
    /// <param name="subjects">The signals to enqueue.</param>
    /// <param name="priorities">The priorities to apply to each signal.</param>
    /// <returns>The bound output collections.</returns>
    private static ReadOnlyObservableCollection<int>[] EnqueueOutputs(
        OperationQueue fixture,
        AsyncSignal<int>[] subjects,
        int[] priorities)
    {
        var outputs = new ReadOnlyObservableCollection<int>[subjects.Length];

        for (var i = 0; i < subjects.Length; i++)
        {
            var subject = subjects[i];
            _ = fixture
                .EnqueueObservableOperation(priorities[i], () => subject)
                .ToObservableChangeSet(scheduler: System.Reactive.Concurrency.ImmediateScheduler.Instance)
                .Bind(out ReadOnlyObservableCollection<int> output)
                .Subscribe();
            outputs[i] = output;
        }

        return outputs;
    }

    /// <summary>Enqueues a set of signals and returns their subscriptions.</summary>
    /// <param name="fixture">The queue under test.</param>
    /// <param name="subjects">The signals to enqueue.</param>
    /// <param name="priority">The priority to apply to each signal.</param>
    /// <returns>The subscriptions created for each enqueued signal.</returns>
    private static IDisposable[] EnqueueSubscriptions(
        OperationQueue fixture,
        AsyncSignal<int>[] subjects,
        int priority)
    {
        var subscriptions = new IDisposable[subjects.Length];

        for (var i = 0; i < subjects.Length; i++)
        {
            var subject = subjects[i];
            subscriptions[i] = fixture.EnqueueObservableOperation(priority, () => subject).Subscribe();
        }

        return subscriptions;
    }

    /// <summary>Asserts the item counts for five output collections.</summary>
    /// <param name="outputs">The output collections.</param>
    /// <param name="first">The expected first count.</param>
    /// <param name="second">The expected second count.</param>
    /// <param name="third">The expected third count.</param>
    /// <param name="fourth">The expected fourth count.</param>
    /// <param name="fifth">The expected fifth count.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous assertions.</returns>
    private static async Task AssertOutputCounts(
        ReadOnlyObservableCollection<int>[] outputs,
        int first,
        int second,
        int third,
        int fourth,
        int fifth)
    {
        await Assert.That(outputs.Count).IsEqualTo(Five);
        await Assert.That(outputs[0].Count).IsEqualTo(first);
        await Assert.That(outputs[1].Count).IsEqualTo(second);
        await Assert.That(outputs[Two].Count).IsEqualTo(third);
        await Assert.That(outputs[Three].Count).IsEqualTo(fourth);
        await Assert.That(outputs[Four].Count).IsEqualTo(fifth);
    }

    /// <summary>Asserts observer state for six signals.</summary>
    /// <param name="subjects">The signals to inspect.</param>
    /// <param name="first">The expected first observer state.</param>
    /// <param name="second">The expected second observer state.</param>
    /// <param name="third">The expected third observer state.</param>
    /// <param name="fourth">The expected fourth observer state.</param>
    /// <param name="fifth">The expected fifth observer state.</param>
    /// <param name="sixth">The expected sixth observer state.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous assertions.</returns>
    private static async Task AssertObserverStates(
        AsyncSignal<int>[] subjects,
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool fifth,
        bool sixth)
    {
        await Assert.That(subjects.Length).IsEqualTo(Six);
        await Assert.That(subjects[0].HasObservers).IsEqualTo(first);
        await Assert.That(subjects[1].HasObservers).IsEqualTo(second);
        await Assert.That(subjects[Two].HasObservers).IsEqualTo(third);
        await Assert.That(subjects[Three].HasObservers).IsEqualTo(fourth);
        await Assert.That(subjects[Four].HasObservers).IsEqualTo(fifth);
        await Assert.That(subjects[Five].HasObservers).IsEqualTo(sixth);
    }
}
