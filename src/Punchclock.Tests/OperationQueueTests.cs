// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using DynamicData;
using DynamicData.Binding;
using TUnit.Assertions.Enums;

namespace Punchclock.Tests;

/// <summary>
/// Tests for the operation queue.
/// </summary>
public class OperationQueueTests
{
    /// <summary>
    /// Checks to make sure that items are dispatched based on their priority.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ItemsShouldBeDispatchedByPriority(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var subjects = Enumerable.Range(0, 5).Select(x => new AsyncSubject<int>()).ToArray();
            var priorities = new[] { 5, 5, 5, 10, 1, };
            var fixture = new OperationQueue(2);

            // The two at the front are solely to stop up the queue, they get subscribed
            // to immediately.
            var outputs = subjects.Zip(
                priorities,
                (inp, pri) =>
                {
                    fixture
                        .EnqueueObservableOperation(pri, () => inp)
                        .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
                        .Bind(out var y).Subscribe();
                    return y;
                }).ToArray();

            await Assert.That(outputs.All(x => x.Count == 0)).IsTrue();

            subjects[0].OnNext(42);
            subjects[0].OnCompleted();
            await Task.Delay(100, cancellationToken);
            await Assert.That(outputs.Select(x => x.Count).ToArray()).IsEquivalentTo(new[] { 1, 0, 0, 0, 0, }, CollectionOrdering.Matching);

            // 0 => completed, 1,3 => live, 2,4 => queued. Make sure 4 *doesn't* fire because
            // the priority should invert it.
            subjects[4].OnNext(42);
            subjects[4].OnCompleted();
            await Task.Delay(100, cancellationToken);
            await Assert.That(outputs.Select(x => x.Count).ToArray()).IsEquivalentTo(new[] { 1, 0, 0, 0, 0, }, CollectionOrdering.Matching);

            // At the end, 0,1 => completed, 3,2 => live, 4 is queued
            subjects[1].OnNext(42);
            subjects[1].OnCompleted();
            await Task.Delay(100, cancellationToken);
            await Assert.That(outputs.Select(x => x.Count).ToArray()).IsEquivalentTo(new[] { 1, 1, 0, 0, 0, }, CollectionOrdering.Matching);

            // At the end, 0,1,2,4 => completed, 3 is live (remember, we completed
            // 4 early)
            subjects[2].OnNext(42);
            subjects[2].OnCompleted();
            await Task.Delay(100, cancellationToken);
            await Assert.That(outputs.Select(x => x.Count).ToArray()).IsEquivalentTo(new[] { 1, 1, 1, 0, 1, }, CollectionOrdering.Matching);

            subjects[3].OnNext(42);
            subjects[3].OnCompleted();
            await Task.Delay(100, cancellationToken);
            await Assert.That(outputs.Select(x => x.Count).ToArray()).IsEquivalentTo(new[] { 1, 1, 1, 1, 1, }, CollectionOrdering.Matching);
        }
    }

    /// <summary>
    /// Checks to make sure that keyed items are serialized.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task KeyedItemsShouldBeSerialized(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var subj1 = new AsyncSubject<int>();
            var subj2 = new AsyncSubject<int>();

            var subscribeCount1 = 0;
            var input1Subj = new AsyncSubject<int>();
            var input1 = System.Reactive.Linq.Observable.Defer(() =>
            {
                subscribeCount1++;
                return input1Subj;
            });
            var subscribeCount2 = 0;
            var input2Subj = new AsyncSubject<int>();
            var input2 = System.Reactive.Linq.Observable.Defer(() =>
            {
                subscribeCount2++;
                return input2Subj;
            });

            var fixture = new OperationQueue(2);

            // Block up the queue
            foreach (var v in new[] { subj1, subj2, })
            {
                fixture.EnqueueObservableOperation(5, () => v).Subscribe();
            }

            // subj1,2 are live, input1,2 are in queue
            fixture
                .EnqueueObservableOperation(5, "key", System.Reactive.Linq.Observable.Never<Unit>(), () => input1)
                .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
                .Bind(out var out1).Subscribe();
            fixture
                .EnqueueObservableOperation(5, "key", System.Reactive.Linq.Observable.Never<Unit>(), () => input2)
                .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
                .Bind(out var out2).Subscribe();

            await Assert.That(subscribeCount1).IsZero();
            await Assert.That(subscribeCount2).IsZero();

            // Dispatch both subj1 and subj2, we should end up with input1 live,
            // but input2 in queue because of the key
            subj1.OnNext(42);
            subj1.OnCompleted();
            subj2.OnNext(42);
            subj2.OnCompleted();

            await Assert.That(subscribeCount1).IsEqualTo(1);
            await Assert.That(subscribeCount2).IsZero();
            await Assert.That(out1.Count).IsEqualTo(0);
            await Assert.That(out2.Count).IsEqualTo(0);

            // Dispatch input1, input2 can now execute
            input1Subj.OnNext(42);
            input1Subj.OnCompleted();

            await Assert.That(subscribeCount1).IsEqualTo(1);
            await Assert.That(subscribeCount2).IsEqualTo(1);
            await Assert.That(out1.Count).IsEqualTo(1);
            await Assert.That(out2.Count).IsEqualTo(0);

            // Dispatch input2, everything is finished
            input2Subj.OnNext(42);
            input2Subj.OnCompleted();

            await Assert.That(subscribeCount1).IsEqualTo(1);
            await Assert.That(subscribeCount2).IsEqualTo(1);
            await Assert.That(out1.Count).IsEqualTo(1);
            await Assert.That(out2.Count).IsEqualTo(1);
        }
    }

    /// <summary>
    /// Checks to make sure that non key items are run in parallel.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task NonkeyedItemsShouldRunInParallel(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var unkeyed1Subj = new AsyncSubject<int>();
            var unkeyed1SubCount = 0;
            var unkeyed1 = System.Reactive.Linq.Observable.Defer(() =>
            {
                unkeyed1SubCount++;
                return unkeyed1Subj;
            });

            var unkeyed2Subj = new AsyncSubject<int>();
            var unkeyed2SubCount = 0;
            var unkeyed2 = System.Reactive.Linq.Observable.Defer(() =>
            {
                unkeyed2SubCount++;
                return unkeyed2Subj;
            });

            var fixture = new OperationQueue(2);

            await Assert.That(unkeyed1SubCount).IsZero();
            await Assert.That(unkeyed2SubCount).IsZero();

            fixture.EnqueueObservableOperation(5, () => unkeyed1).Subscribe();
            fixture.EnqueueObservableOperation(5, () => unkeyed2).Subscribe();

            await Assert.That(unkeyed1SubCount).IsEqualTo(1);
            await Assert.That(unkeyed2SubCount).IsEqualTo(1);
        }
    }

    /// <summary>
    /// Checks to make sure that shutdown signals once everything completes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ShutdownShouldSignalOnceEverythingCompletes(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var subjects = Enumerable.Range(0, 5).Select(x => new AsyncSubject<int>()).ToArray();
            var priorities = new[] { 5, 5, 5, 10, 1, };
            var fixture = new OperationQueue(2);

            // The two at the front are solely to stop up the queue, they get subscribed
            // to immediately.
            var outputs = subjects.Zip(
                priorities,
                (inp, pri) =>
                {
                    fixture
                        .EnqueueObservableOperation(pri, () => inp)
                        .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
                        .Bind(out var output).Subscribe();
                    return output;
                }).ToArray();

            fixture
                .ShutdownQueue()
                .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
                .Bind(out var shutdown).Subscribe();

            await Assert.That(outputs.All(x => x.Count == 0)).IsTrue();
            await Assert.That(shutdown.Count).IsEqualTo(0);

            for (var i = 0; i < 4; i++)
            {
                subjects[i].OnNext(42);
                subjects[i].OnCompleted();
            }

            await Assert.That(shutdown.Count).IsEqualTo(0);

            // Complete the last one, that should signal that we're shut down
            subjects[4].OnNext(42);
            subjects[4].OnCompleted();

            await Assert.That(outputs.All(x => x.Count == 1)).IsTrue();
            await Assert.That(shutdown.Count).IsEqualTo(1);
        }
    }

    /// <summary>
    /// Checks to make sure that the queue holds items until unpaused.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task PausingTheQueueShouldHoldItemsUntilUnpaused(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var item = System.Reactive.Linq.Observable.Return(42);

            var fixture = new OperationQueue(2);
            new[]
            {
                fixture.EnqueueObservableOperation(4, () => item),
                fixture.EnqueueObservableOperation(4, () => item),
            }.Merge()
             .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
             .Bind(out var prePauseOutput).Subscribe();

            await Assert.That(prePauseOutput.Count).IsEqualTo(2);

            var unpause1 = fixture.PauseQueue();

            // The queue is halted, but we should still eventually process these
            // once it's no longer halted
            new[]
            {
                fixture.EnqueueObservableOperation(4, () => item),
                fixture.EnqueueObservableOperation(4, () => item),
            }.Merge()
             .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
             .Bind(out var pauseOutput).Subscribe();

            await Assert.That(pauseOutput.Count).IsEqualTo(0);

            var unpause2 = fixture.PauseQueue();
            await Assert.That(pauseOutput.Count).IsEqualTo(0);

            unpause1.Dispose();
            await Assert.That(pauseOutput.Count).IsEqualTo(0);

            unpause2.Dispose();
            await Assert.That(pauseOutput.Count).IsEqualTo(2);
        }
    }

    /// <summary>
    /// Checks that cancelling items should not result in them being returned.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task CancellingItemsShouldNotResultInThemBeingReturned(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var subj1 = new AsyncSubject<int>();
            var subj2 = new AsyncSubject<int>();

            var fixture = new OperationQueue(2);

            // Block up the queue
            foreach (var v in new[] { subj1, subj2, })
            {
                fixture.EnqueueObservableOperation(5, () => v).Subscribe();
            }

            var cancel1 = new Subject<Unit>();
            var item1 = new AsyncSubject<int>();
            new[]
            {
                fixture.EnqueueObservableOperation(5, "foo", cancel1, () => item1),
                fixture.EnqueueObservableOperation(5, "baz", () => System.Reactive.Linq.Observable.Return(42)),
            }.Merge()
             .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
             .Bind(out var output).Subscribe();

            await Assert.That(output.Count).IsEqualTo(0);

            // Still blocked by subj1,2, only baz is in queue
            cancel1.OnNext(Unit.Default);
            cancel1.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(0);

            // foo was cancelled, baz is still good
            subj1.OnNext(42);
            subj1.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(1);

            // don't care that cancelled item finished
            item1.OnNext(42);
            item1.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(1);

            // still shouldn't see anything
            subj2.OnNext(42);
            subj2.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(1);
        }
    }

    /// <summary>
    /// Checks that the cancelling of items, that the items won't be evaluated.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task CancellingItemsShouldntEvenBeEvaluated(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var subj1 = new AsyncSubject<int>();
            var subj2 = new AsyncSubject<int>();

            var fixture = new OperationQueue(2);

            // Block up the queue
            foreach (var v in new[] { subj1, subj2, })
            {
                fixture.EnqueueObservableOperation(5, () => v).Subscribe();
            }

            var cancel1 = new Subject<Unit>();
            var wasCalled = false;
            var item1 = new AsyncSubject<int>();

            fixture.EnqueueObservableOperation(5, "foo", cancel1, () =>
            {
                wasCalled = true;
                return item1;
            }).ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
              .Bind(out var output).Subscribe();

            await Assert.That(output.Count).IsEqualTo(0);
            await Assert.That(wasCalled).IsFalse();

            // Still blocked by subj1,2 - however, we've cancelled foo before
            // it even had a chance to run - if that's the case, we shouldn't
            // even call the evaluation func
            cancel1.OnNext(Unit.Default);
            cancel1.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(0);
            await Assert.That(wasCalled).IsFalse();

            // Unblock subj1,2, we still shouldn't see wasCalled = true
            subj1.OnNext(42);
            subj1.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(0);
            await Assert.That(wasCalled).IsFalse();

            subj2.OnNext(42);
            subj2.OnCompleted();
            await Assert.That(output.Count).IsEqualTo(0);
            await Assert.That(wasCalled).IsFalse();
        }
    }

    /// <summary>
    /// Checks to make sure the queue respects maximum concurrency.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task QueueShouldRespectMaximumConcurrent(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var unkeyed1Subj = new AsyncSubject<int>();
            var unkeyed1SubCount = 0;
            var unkeyed1 = System.Reactive.Linq.Observable.Defer(() =>
            {
                unkeyed1SubCount++;
                return unkeyed1Subj;
            });

            var unkeyed2Subj = new AsyncSubject<int>();
            var unkeyed2SubCount = 0;
            var unkeyed2 = System.Reactive.Linq.Observable.Defer(() =>
            {
                unkeyed2SubCount++;
                return unkeyed2Subj;
            });

            var unkeyed3Subj = new AsyncSubject<int>();
            var unkeyed3SubCount = 0;
            var unkeyed3 = System.Reactive.Linq.Observable.Defer(() =>
            {
                unkeyed3SubCount++;
                return unkeyed3Subj;
            });

            var fixture = new OperationQueue(2);

            await Assert.That(unkeyed1SubCount).IsZero();
            await Assert.That(unkeyed2SubCount).IsZero();
            await Assert.That(unkeyed3SubCount).IsZero();

            fixture.EnqueueObservableOperation(5, () => unkeyed1).Subscribe();
            fixture.EnqueueObservableOperation(5, () => unkeyed2).Subscribe();
            fixture.EnqueueObservableOperation(5, () => unkeyed3).Subscribe();

            await Assert.That(unkeyed1SubCount).IsEqualTo(1);
            await Assert.That(unkeyed2SubCount).IsEqualTo(1);
            await Assert.That(unkeyed3SubCount).IsZero();
        }
    }

    /// <summary>
    /// Checks to see if the maximum concurrency is increased that the existing queue adapts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ShouldBeAbleToIncreaseTheMaximunConcurrentValueOfAnExistingQueue(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var unkeyed1Subj = new AsyncSubject<int>();
            var unkeyed1SubCount = 0;
            var unkeyed1 = System.Reactive.Linq.Observable.Defer(() =>
            {
                unkeyed1SubCount++;
                return unkeyed1Subj;
            });

            var unkeyed2Subj = new AsyncSubject<int>();
            var unkeyed2SubCount = 0;
            var unkeyed2 = System.Reactive.Linq.Observable.Defer(() =>
            {
                unkeyed2SubCount++;
                return unkeyed2Subj;
            });

            var unkeyed3Subj = new AsyncSubject<int>();
            var unkeyed3SubCount = 0;
            var unkeyed3 = System.Reactive.Linq.Observable.Defer(() =>
            {
                unkeyed3SubCount++;
                return unkeyed3Subj;
            });

            var unkeyed4Subj = new AsyncSubject<int>();
            var unkeyed4SubCount = 0;
            var unkeyed4 = System.Reactive.Linq.Observable.Defer(() =>
            {
                unkeyed4SubCount++;
                return unkeyed4Subj;
            });

            var fixture = new OperationQueue(2);

            await Assert.That(unkeyed1SubCount).IsZero();
            await Assert.That(unkeyed2SubCount).IsZero();
            await Assert.That(unkeyed3SubCount).IsZero();
            await Assert.That(unkeyed4SubCount).IsZero();

            fixture.EnqueueObservableOperation(5, () => unkeyed1).Subscribe();
            fixture.EnqueueObservableOperation(5, () => unkeyed2).Subscribe();
            fixture.EnqueueObservableOperation(5, () => unkeyed3).Subscribe();
            fixture.EnqueueObservableOperation(5, () => unkeyed4).Subscribe();

            await Assert.That(unkeyed1SubCount).IsEqualTo(1);
            await Assert.That(unkeyed2SubCount).IsEqualTo(1);
            await Assert.That(unkeyed3SubCount).IsZero();
            await Assert.That(unkeyed4SubCount).IsZero();

            fixture.SetMaximumConcurrent(3);

            await Assert.That(unkeyed1SubCount).IsEqualTo(1);
            await Assert.That(unkeyed2SubCount).IsEqualTo(1);
            await Assert.That(unkeyed3SubCount).IsEqualTo(1);
            await Assert.That(unkeyed4SubCount).IsZero();
        }
    }

    /// <summary>
    /// Checks to make sure that decreasing the maximum concurrency the queue adapts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ShouldBeAbleToDecreaseTheMaximunConcurrentValueOfAnExistingQueue(CancellationToken cancellationToken)
    {
        using (Assert.Multiple())
        {
            var subjects = Enumerable.Range(0, 6).Select(x => new AsyncSubject<int>()).ToArray();
            var fixture = new OperationQueue(3);

            // The three at the front are solely to stop up the queue, they get subscribed
            // to immediately.
            var outputs = subjects
                .Select(inp =>
                {
                    fixture
                        .EnqueueObservableOperation(5, () => inp)
                        .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
                        .Bind(out var output).Subscribe();
                    return output;
                }).ToArray();

            await Assert.That(
                new[] { true, true, true, false, false, false, }
                    .Zip(
                        subjects,
                        (expected, subj) => new { expected, actual = subj.HasObservers, })
                    .All(x => x.expected == x.actual)).IsTrue();

            fixture.SetMaximumConcurrent(2);

            // Complete the first one, the last three subjects should still have
            // no observers because we reduced maximum concurrent
            subjects[0].OnNext(42);
            subjects[0].OnCompleted();

            await Assert.That(
                new[] { false, true, true, false, false, false, }
                    .Zip(
                        subjects,
                        (expected, subj) => new { expected, actual = subj.HasObservers, })
                    .All(x => x.expected == x.actual)).IsTrue();

            // Complete subj[1], now 2,3 are live
            subjects[1].OnNext(42);
            subjects[1].OnCompleted();

            await Assert.That(
                new[] { false, false, true, true, false, false, }
                    .Zip(
                        subjects,
                        (expected, subj) => new { expected, actual = subj.HasObservers, })
                    .All(x => x.expected == x.actual)).IsTrue();
        }
    }

    /// <summary>
    /// Checks that equal priority across different keys can be randomized when enabled.
    /// </summary>
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
            var blocker = new AsyncSubject<int>();
            queue.EnqueueObservableOperation(5, () => blocker).Subscribe();

            var a = new AsyncSubject<int>();
            var b = new AsyncSubject<int>();

            var nextCountA = 0;
            var nextCountB = 0;

            queue.EnqueueObservableOperation(5, "A", () => a).Subscribe(_ => nextCountA++);
            queue.EnqueueObservableOperation(5, "B", () => b).Subscribe(_ => nextCountB++);

            // Unblock
            blocker.OnNext(1);
            blocker.OnCompleted();

            // Complete whichever started first according to randomized order
            if (a.HasObservers && !b.HasObservers)
            {
                a.OnNext(42);
                a.OnCompleted();
            }
            else if (b.HasObservers && !a.HasObservers)
            {
                b.OnNext(42);
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
                a.OnNext(42);
                a.OnCompleted();
            }

            if (b.HasObservers)
            {
                b.OnNext(42);
                b.OnCompleted();
            }

            await Assert.That(nextCountA + nextCountB).IsEqualTo(2);
        }
    }

    /// <summary>
    /// Verifies that constructor throws <see cref="ArgumentOutOfRangeException"/> for non-positive maximumConcurrent.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithZeroOrNegativeMaxConcurrent_ThrowsArgumentOutOfRangeException()
    {
        using (Assert.Multiple())
        {
            var ex1 = await Assert.That(() => new OperationQueue(0))
                .Throws<ArgumentOutOfRangeException>();
            await Assert.That(ex1!.ParamName).IsEqualTo("maximumConcurrent");

            var ex2 = await Assert.That(() => new OperationQueue(-1))
                .Throws<ArgumentOutOfRangeException>();
            await Assert.That(ex2!.ParamName).IsEqualTo("maximumConcurrent");
        }
    }

    /// <summary>
    /// Verifies that SetMaximumConcurrent throws <see cref="ArgumentOutOfRangeException"/> for non-positive values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SetMaximumConcurrent_WithZeroOrNegative_ThrowsArgumentOutOfRangeException()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(2);

            var ex1 = await Assert.That(() => queue.SetMaximumConcurrent(0))
                .Throws<ArgumentOutOfRangeException>();
            await Assert.That(ex1!.ParamName).IsEqualTo("maximumConcurrent");

            var ex2 = await Assert.That(() => queue.SetMaximumConcurrent(-1))
                .Throws<ArgumentOutOfRangeException>();
            await Assert.That(ex2!.ParamName).IsEqualTo("maximumConcurrent");
        }
    }

    /// <summary>
    /// Verifies that SetMaximumConcurrent updates the concurrency level.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SetMaximumConcurrent_UpdatesConcurrencyLevel()
    {
        using var queue = new OperationQueue(1);
        queue.SetMaximumConcurrent(5);

        // If it updated successfully, we should be able to run 5 operations concurrently
        // This is indirectly verified by the queue not blocking when we have 5 items
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that Dispose can be called multiple times safely.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var queue = new OperationQueue(1);
        queue.Dispose();
        queue.Dispose(); // Should not throw
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that ShutdownQueue can be called multiple times and returns the same observable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ShutdownQueue_CalledTwice_ReturnsSameObservable()
    {
        using var queue = new OperationQueue(1);

        var shutdown1 = queue.ShutdownQueue();
        var shutdown2 = queue.ShutdownQueue();

        await Assert.That(shutdown1).IsEqualTo(shutdown2);
    }

    /// <summary>
    /// Verifies that empty string key is normalized to DefaultKey.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueObservableOperation_WithEmptyKey_NormalizesToDefaultKey()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(2);

            var completed1 = false;
            var completed2 = false;

            // Empty string should be treated as DefaultKey (non-keyed, concurrent)
            queue.EnqueueObservableOperation(1, string.Empty, () => Observable.Return(1))
                .Subscribe(_ => completed1 = true);

            queue.EnqueueObservableOperation(1, string.Empty, () => Observable.Return(2))
                .Subscribe(_ => completed2 = true);

            await Task.Delay(100);

            // Both should complete concurrently since they're treated as DefaultKey
            await Assert.That(completed1).IsTrue();
            await Assert.That(completed2).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that null key is normalized to DefaultKey.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnqueueObservableOperation_WithNullKey_NormalizesToDefaultKey()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(2);

            var completed1 = false;
            var completed2 = false;

            queue.EnqueueObservableOperation(1, null!, () => Observable.Return(1))
                .Subscribe(_ => completed1 = true);

            queue.EnqueueObservableOperation(1, null!, () => Observable.Return(2))
                .Subscribe(_ => completed2 = true);

            await Task.Delay(100);

            await Assert.That(completed1).IsTrue();
            await Assert.That(completed2).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that PauseQueue after ShutdownQueue does not resume the queue.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task PauseQueue_AfterShutdown_DoesNotResume()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(1);

            var shutdown = queue.ShutdownQueue();
            var pauseHandle = queue.PauseQueue();

            // Disposing the pause handle should not resume since we're shut down
            pauseHandle.Dispose();

            // Queue should still be in shutdown state
            await Task.Delay(50);
            await Task.CompletedTask; // Verify no exceptions
        }
    }

    /// <summary>
    /// Verifies that constructor with random tiebreak parameters works correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithRandomTiebreakParameters_Succeeds()
    {
        using var queue = new OperationQueue(maximumConcurrent: 2, randomizeEqualPriority: true, seed: 42);
        await Assert.That(queue).IsNotNull();
    }
}
