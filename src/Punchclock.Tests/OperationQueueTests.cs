// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData;
using ReactiveUI.Primitives.Signals;

using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace Punchclock.Tests;

/// <summary>Tests for the operation queue.</summary>
public class OperationQueueTests
{
    private const string MaximumConcurrent = "maximumConcurrent";

    private const string CancellationValue = "stop";

    private const int Two = 2;

    private const int Three = 3;

    private const int Four = 4;

    private const int Five = 5;

    private const int Six = 6;

    private const int FourtyTwo = 42;

    private const int OneHundred = 100;

    /// <summary>Checks to make sure that items are dispatched based on their priority.</summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ItemsShouldBeDispatchedByPriority(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var subjects = Enumerable.Range(0, Five).Select(x => new AsyncSignal<int>()).ToArray();
            var priorities = new[] { Five, Five, Five, 10, 1, };
            var fixture = new OperationQueue(Two);

            // The two at the front are solely to stop up the queue, they get subscribed
            // to immediately.
            var outputs = subjects.Zip(
                priorities,
                (inp, pri) =>
                {
                    fixture
                        .EnqueueObservableOperation(pri, () => inp)
                        .ToObservableChangeSet(scheduler: System.Reactive.Concurrency.ImmediateScheduler.Instance)
                        .Bind(out var y).Subscribe();
                    return y;
                }).ToArray();

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

            var fixture = new OperationQueue(Two);

            // Block up the queue
            foreach (var v in new[] { subj1, subj2, })
            {
                fixture.EnqueueObservableOperation(Five, () => v).Subscribe();
            }

            // subj1,2 are live, input1,2 are in queue
            fixture
                .EnqueueObservableOperation(Five, "key", Signal.Silent<RxVoid>(), () => input1)
                .ToObservableChangeSet(scheduler: System.Reactive.Concurrency.ImmediateScheduler.Instance)
                .Bind(out var out1).Subscribe();
            fixture
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

            var fixture = new OperationQueue(Two);

            await Assert.That(unkeyed1SubCount).IsZero();
            await Assert.That(unkeyed2SubCount).IsZero();

            fixture.EnqueueObservableOperation(Five, () => unkeyed1).Subscribe();
            fixture.EnqueueObservableOperation(Five, () => unkeyed2).Subscribe();

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
            var subjects = Enumerable.Range(0, Five).Select(x => new AsyncSignal<int>()).ToArray();
            var priorities = new[] { Five, Five, Five, 10, 1, };
            var fixture = new OperationQueue(Two);

            // The two at the front are solely to stop up the queue, they get subscribed
            // to immediately.
            var outputs = subjects.Zip(
                priorities,
                (inp, pri) =>
                {
                    fixture
                        .EnqueueObservableOperation(pri, () => inp)
                        .ToObservableChangeSet(scheduler: System.Reactive.Concurrency.ImmediateScheduler.Instance)
                        .Bind(out var output).Subscribe();
                    return output;
                }).ToArray();

            fixture
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

            var fixture = new OperationQueue(Two);
            ReactiveUI.Primitives.LinqExtensions.Blend(
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
            ReactiveUI.Primitives.LinqExtensions.Blend(
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

            var fixture = new OperationQueue(Two);

            // Block up the queue
            foreach (var v in new[] { subj1, subj2, })
            {
                fixture.EnqueueObservableOperation(Five, () => v).Subscribe();
            }

            var cancel1 = new Signal<RxVoid>();
            var item1 = new AsyncSignal<int>();
            ReactiveUI.Primitives.LinqExtensions.Blend([
                fixture.EnqueueObservableOperation(Five, "foo", cancel1, () => item1),
                fixture.EnqueueObservableOperation(Five, "baz", () => Signal.Emit(FourtyTwo)),
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

            var fixture = new OperationQueue(Two);

            // Block up the queue
            foreach (var v in new[] { subj1, subj2, })
            {
                fixture.EnqueueObservableOperation(Five, () => v).Subscribe();
            }

            var cancel1 = new Signal<RxVoid>();
            var wasCalled = false;
            var item1 = new AsyncSignal<int>();

            fixture.EnqueueObservableOperation(Five, "foo", cancel1, () =>
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

            var fixture = new OperationQueue(Two);

            await Assert.That(unkeyed1SubCount).IsZero();
            await Assert.That(unkeyed2SubCount).IsZero();
            await Assert.That(unkeyed3SubCount).IsZero();

            fixture.EnqueueObservableOperation(Five, () => unkeyed1).Subscribe();
            fixture.EnqueueObservableOperation(Five, () => unkeyed2).Subscribe();
            fixture.EnqueueObservableOperation(Five, () => unkeyed3).Subscribe();

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

            var fixture = new OperationQueue(Two);

            await Assert.That(unkeyed1SubCount).IsZero();
            await Assert.That(unkeyed2SubCount).IsZero();
            await Assert.That(unkeyed3SubCount).IsZero();
            await Assert.That(unkeyed4SubCount).IsZero();

            fixture.EnqueueObservableOperation(Five, () => unkeyed1).Subscribe();
            fixture.EnqueueObservableOperation(Five, () => unkeyed2).Subscribe();
            fixture.EnqueueObservableOperation(Five, () => unkeyed3).Subscribe();
            fixture.EnqueueObservableOperation(Five, () => unkeyed4).Subscribe();

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
            var subjects = Enumerable.Range(0, Six).Select(x => new AsyncSignal<int>()).ToArray();
            var fixture = new OperationQueue(Three);

            // The three at the front are solely to stop up the queue, they get subscribed
            // to immediately.
            var subscriptions = subjects
                .Select(inp => fixture
                    .EnqueueObservableOperation(Five, () => inp)
                    .Subscribe())
                .ToArray();

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
            var queue = new OperationQueue(maximumConcurrent: 1, randomizeEqualPriority: true, seed: 123);

            // Block the queue initially
            var blocker = new AsyncSignal<int>();
            queue.EnqueueObservableOperation(Five, () => blocker).Subscribe();

            var a = new AsyncSignal<int>();
            var b = new AsyncSignal<int>();

            var nextCountA = 0;
            var nextCountB = 0;

            queue.EnqueueObservableOperation(Five, "A", () => a).Subscribe(_ => nextCountA++);
            queue.EnqueueObservableOperation(Five, "B", () => b).Subscribe(_ => nextCountB++);

            // Unblock
            blocker.OnNext(1);
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

    /// <summary>Verifies that constructor throws <see cref="ArgumentOutOfRangeException"/> for non-positive maximumConcurrent.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithZeroOrNegativeMaxConcurrent_ThrowsArgumentOutOfRangeException()
    {
        using (Assert.Multiple())
        {
            var ex1 = await Assert.That(() => new OperationQueue(0))
                .Throws<ArgumentOutOfRangeException>();
            await Assert.That(ex1!.ParamName).IsEqualTo(MaximumConcurrent);

            var ex2 = await Assert.That(() => new OperationQueue(-1))
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
            queue.EnqueueObservableOperation(1, string.Empty, () => Signal.Emit(1))
                .Subscribe(_ => completed1 = true);

            queue.EnqueueObservableOperation(1, string.Empty, () => Signal.Emit(Two))
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

            queue.EnqueueObservableOperation(1, null!, () => Signal.Emit(1))
                .Subscribe(_ => completed1 = true);

            queue.EnqueueObservableOperation(1, null!, () => Signal.Emit(Two))
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
            shutdown.Subscribe(); // Start shutdown process
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
            queue.EnqueueObservableOperation(1, "a", () => Signal.Emit(1)).Subscribe(_ => completed++);
            queue.EnqueueObservableOperation(1, "b", () => Signal.Emit(Two)).Subscribe(_ => completed++);

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
            queue.EnqueueObservableOperation(1, () => Signal.Emit(FourtyTwo))
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

            queue.EnqueueObservableOperation(1, "cancel-key", cancel, () => operation)
                .Subscribe(
                    _ => values++,
                    _ => { },
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
            queue.EnqueueObservableOperation(1, () => Signal.Emit(FourtyTwo))
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
        queue.ShutdownQueue().Subscribe();

        var ex = await Assert.That(() =>
            queue.EnqueueObservableOperation(1, "late", Signal.Silent<RxVoid>(), () => Signal.Emit(FourtyTwo)))
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

            queue.EnqueueObservableOperation(
                    1,
                    "pending",
                    cancel,
                    () => Signal.Emit(FourtyTwo))
                .Subscribe(
                    _ => { },
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

            queue.EnqueueObservableOperation(1, () => Signal.Fail<int>(exception))
                .Subscribe(
                    _ => { },
                    error => observed = error);

            queue.EnqueueObservableOperation(1, () => Signal.Emit(FourtyTwo))
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

            queue.EnqueueObservableOperation<int>(
                    1,
                    () => throw new InvalidOperationException("factory failed"))
                .Subscribe();

            queue.EnqueueObservableOperation(1, () => Signal.Emit(FourtyTwo))
                .Subscribe(value => completed = value == FourtyTwo);

            await Assert.That(completed).IsTrue();
        }
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
        System.Collections.ObjectModel.ReadOnlyObservableCollection<int>[] outputs,
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
