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
using DynamicData;
using DynamicData.Binding;
using NUnit.Framework; // switched from Xunit

namespace Punchclock.Tests
{
    /// <summary>
    /// Tests for the operation queue.
    /// </summary>
    public class OperationQueueTests
    {
        /// <summary>
        /// Checks to make sure that items are dispatched based on their priority.
        /// </summary>
        [Test] // was [Fact]
        public void ItemsShouldBeDispatchedByPriority()
        {
            using var scope = Assert.EnterMultipleScope();

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

            Assert.That(outputs.All(x => x.Count == 0), Is.True);

            subjects[0].OnNext(42);
            subjects[0].OnCompleted();
            Assert.That(outputs.Select(x => x.Count).ToArray(), Is.EqualTo(new[] { 1, 0, 0, 0, 0, }));

            // 0 => completed, 1,3 => live, 2,4 => queued. Make sure 4 *doesn't* fire because
            // the priority should invert it.
            subjects[4].OnNext(42);
            subjects[4].OnCompleted();
            Assert.That(outputs.Select(x => x.Count).ToArray(), Is.EqualTo(new[] { 1, 0, 0, 0, 0, }));

            // At the end, 0,1 => completed, 3,2 => live, 4 is queued
            subjects[1].OnNext(42);
            subjects[1].OnCompleted();
            Assert.That(outputs.Select(x => x.Count).ToArray(), Is.EqualTo(new[] { 1, 1, 0, 0, 0, }));

            // At the end, 0,1,2,4 => completed, 3 is live (remember, we completed
            // 4 early)
            subjects[2].OnNext(42);
            subjects[2].OnCompleted();
            Assert.That(outputs.Select(x => x.Count).ToArray(), Is.EqualTo(new[] { 1, 1, 1, 0, 1, }));

            subjects[3].OnNext(42);
            subjects[3].OnCompleted();
            Assert.That(outputs.Select(x => x.Count).ToArray(), Is.EqualTo(new[] { 1, 1, 1, 1, 1, }));
        }

        /// <summary>
        /// Checks to make sure that keyed items are serialized.
        /// </summary>
        [Test]
        public void KeyedItemsShouldBeSerialized()
        {
            using var scope = Assert.EnterMultipleScope();

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
                fixture.EnqueueObservableOperation(5, () => v);
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

            Assert.That(subscribeCount1, Is.Zero);
            Assert.That(subscribeCount2, Is.Zero);

            // Dispatch both subj1 and subj2, we should end up with input1 live,
            // but input2 in queue because of the key
            subj1.OnNext(42);
            subj1.OnCompleted();
            subj2.OnNext(42);
            subj2.OnCompleted();

            Assert.That(subscribeCount1, Is.EqualTo(1));
            Assert.That(subscribeCount2, Is.Zero);
            Assert.That(out1, Has.Count.EqualTo(0));
            Assert.That(out2, Has.Count.EqualTo(0));

            // Dispatch input1, input2 can now execute
            input1Subj.OnNext(42);
            input1Subj.OnCompleted();

            Assert.That(subscribeCount1, Is.EqualTo(1));
            Assert.That(subscribeCount2, Is.EqualTo(1));
            Assert.That(out1, Has.Count.EqualTo(1));
            Assert.That(out2, Has.Count.EqualTo(0));

            // Dispatch input2, everything is finished
            input2Subj.OnNext(42);
            input2Subj.OnCompleted();

            Assert.That(subscribeCount1, Is.EqualTo(1));
            Assert.That(subscribeCount2, Is.EqualTo(1));
            Assert.That(out1, Has.Count.EqualTo(1));
            Assert.That(out2, Has.Count.EqualTo(1));
        }

        /// <summary>
        /// Checks to make sure that non key items are run in parallel.
        /// </summary>
        [Test]
        public void NonkeyedItemsShouldRunInParallel()
        {
            using var scope = Assert.EnterMultipleScope();

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

            Assert.That(unkeyed1SubCount, Is.Zero);
            Assert.That(unkeyed2SubCount, Is.Zero);

            fixture.EnqueueObservableOperation(5, () => unkeyed1);
            fixture.EnqueueObservableOperation(5, () => unkeyed2);

            Assert.That(unkeyed1SubCount, Is.EqualTo(1));
            Assert.That(unkeyed2SubCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Checks to make sure that shutdown signals once everything completes.
        /// </summary>
        [Test]
        public void ShutdownShouldSignalOnceEverythingCompletes()
        {
            using var scope = Assert.EnterMultipleScope();

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

            Assert.That(outputs.All(x => x.Count == 0), Is.True);
            Assert.That(shutdown, Has.Count.EqualTo(0));

            for (var i = 0; i < 4; i++)
            {
                subjects[i].OnNext(42);
                subjects[i].OnCompleted();
            }

            Assert.That(shutdown, Has.Count.EqualTo(0));

            // Complete the last one, that should signal that we're shut down
            subjects[4].OnNext(42);
            subjects[4].OnCompleted();

            Assert.That(outputs.All(x => x.Count == 1), Is.True);
            Assert.That(shutdown, Has.Count.EqualTo(1));
        }

        /// <summary>
        /// Checks to make sure that the queue holds items until unpaused.
        /// </summary>
        [Test]
        public void PausingTheQueueShouldHoldItemsUntilUnpaused()
        {
            using var scope = Assert.EnterMultipleScope();

            var item = System.Reactive.Linq.Observable.Return(42);

            var fixture = new OperationQueue(2);
            new[]
            {
                fixture.EnqueueObservableOperation(4, () => item),
                fixture.EnqueueObservableOperation(4, () => item),
            }.Merge()
             .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
             .Bind(out var prePauseOutput).Subscribe();

            Assert.That(prePauseOutput, Has.Count.EqualTo(2));

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

            Assert.That(pauseOutput, Has.Count.EqualTo(0));

            var unpause2 = fixture.PauseQueue();
            Assert.That(pauseOutput, Has.Count.EqualTo(0));

            unpause1.Dispose();
            Assert.That(pauseOutput, Has.Count.EqualTo(0));

            unpause2.Dispose();
            Assert.That(pauseOutput, Has.Count.EqualTo(2));
        }

        /// <summary>
        /// Checks that cancelling items should not result in them being returned.
        /// </summary>
        [Test]
        public void CancellingItemsShouldNotResultInThemBeingReturned()
        {
            using var scope = Assert.EnterMultipleScope();

            var subj1 = new AsyncSubject<int>();
            var subj2 = new AsyncSubject<int>();

            var fixture = new OperationQueue(2);

            // Block up the queue
            foreach (var v in new[] { subj1, subj2, })
            {
                fixture.EnqueueObservableOperation(5, () => v);
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

            Assert.That(output, Has.Count.EqualTo(0));

            // Still blocked by subj1,2, only baz is in queue
            cancel1.OnNext(Unit.Default);
            cancel1.OnCompleted();
            Assert.That(output, Has.Count.EqualTo(0));

            // foo was cancelled, baz is still good
            subj1.OnNext(42);
            subj1.OnCompleted();
            Assert.That(output, Has.Count.EqualTo(1));

            // don't care that cancelled item finished
            item1.OnNext(42);
            item1.OnCompleted();
            Assert.That(output, Has.Count.EqualTo(1));

            // still shouldn't see anything
            subj2.OnNext(42);
            subj2.OnCompleted();
            Assert.That(output, Has.Count.EqualTo(1));
        }

        /// <summary>
        /// Checks that the cancelling of items, that the items won't be evaluated.
        /// </summary>
        [Test]
        public void CancellingItemsShouldntEvenBeEvaluated()
        {
            using var scope = Assert.EnterMultipleScope();

            var subj1 = new AsyncSubject<int>();
            var subj2 = new AsyncSubject<int>();

            var fixture = new OperationQueue(2);

            // Block up the queue
            foreach (var v in new[] { subj1, subj2, })
            {
                fixture.EnqueueObservableOperation(5, () => v);
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

            Assert.That(output, Has.Count.EqualTo(0));
            Assert.That(wasCalled, Is.False);

            // Still blocked by subj1,2 - however, we've cancelled foo before
            // it even had a chance to run - if that's the case, we shouldn't
            // even call the evaluation func
            cancel1.OnNext(Unit.Default);
            cancel1.OnCompleted();
            Assert.That(output, Has.Count.EqualTo(0));
            Assert.That(wasCalled, Is.False);

            // Unblock subj1,2, we still shouldn't see wasCalled = true
            subj1.OnNext(42);
            subj1.OnCompleted();
            Assert.That(output, Has.Count.EqualTo(0));
            Assert.That(wasCalled, Is.False);

            subj2.OnNext(42);
            subj2.OnCompleted();
            Assert.That(output, Has.Count.EqualTo(0));
            Assert.That(wasCalled, Is.False);
        }

        /// <summary>
        /// Checks to make sure the queue respects maximum concurrency.
        /// </summary>
        [Test]
        public void QueueShouldRespectMaximumConcurrent()
        {
            using var scope = Assert.EnterMultipleScope();

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

            Assert.That(unkeyed1SubCount, Is.Zero);
            Assert.That(unkeyed2SubCount, Is.Zero);
            Assert.That(unkeyed3SubCount, Is.Zero);

            fixture.EnqueueObservableOperation(5, () => unkeyed1);
            fixture.EnqueueObservableOperation(5, () => unkeyed2);
            fixture.EnqueueObservableOperation(5, () => unkeyed3);

            Assert.That(unkeyed1SubCount, Is.EqualTo(1));
            Assert.That(unkeyed2SubCount, Is.EqualTo(1));
            Assert.That(unkeyed3SubCount, Is.Zero);
        }

        /// <summary>
        /// Checks to see if the maximum concurrency is increased that the existing queue adapts.
        /// </summary>
        [Test]
        public void ShouldBeAbleToIncreaseTheMaximunConcurrentValueOfAnExistingQueue()
        {
            using var scope = Assert.EnterMultipleScope();

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

            Assert.That(unkeyed1SubCount, Is.Zero);
            Assert.That(unkeyed2SubCount, Is.Zero);
            Assert.That(unkeyed3SubCount, Is.Zero);
            Assert.That(unkeyed4SubCount, Is.Zero);

            fixture.EnqueueObservableOperation(5, () => unkeyed1);
            fixture.EnqueueObservableOperation(5, () => unkeyed2);
            fixture.EnqueueObservableOperation(5, () => unkeyed3);
            fixture.EnqueueObservableOperation(5, () => unkeyed4);

            Assert.That(unkeyed1SubCount, Is.EqualTo(1));
            Assert.That(unkeyed2SubCount, Is.EqualTo(1));
            Assert.That(unkeyed3SubCount, Is.Zero);
            Assert.That(unkeyed4SubCount, Is.Zero);

            fixture.SetMaximumConcurrent(3);

            Assert.That(unkeyed1SubCount, Is.EqualTo(1));
            Assert.That(unkeyed2SubCount, Is.EqualTo(1));
            Assert.That(unkeyed3SubCount, Is.EqualTo(1));
            Assert.That(unkeyed4SubCount, Is.Zero);
        }

        /// <summary>
        /// Checks to make sure that decreasing the maximum concurrency the queue adapts.
        /// </summary>
        [Test]
        public void ShouldBeAbleToDecreaseTheMaximunConcurrentValueOfAnExistingQueue()
        {
            using var scope = Assert.EnterMultipleScope();

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

            Assert.That(
                new[] { true, true, true, false, false, false, }
                    .Zip(
                        subjects,
                        (expected, subj) => new { expected, actual = subj.HasObservers, })
                    .All(x => x.expected == x.actual),
                Is.True);

            fixture.SetMaximumConcurrent(2);

            // Complete the first one, the last three subjects should still have
            // no observers because we reduced maximum concurrent
            subjects[0].OnNext(42);
            subjects[0].OnCompleted();

            Assert.That(
                new[] { false, true, true, false, false, false, }
                    .Zip(
                        subjects,
                        (expected, subj) => new { expected, actual = subj.HasObservers, })
                    .All(x => x.expected == x.actual),
                Is.True);

            // Complete subj[1], now 2,3 are live
            subjects[1].OnNext(42);
            subjects[1].OnCompleted();

            Assert.That(
                new[] { false, false, true, true, false, false, }
                    .Zip(
                        subjects,
                        (expected, subj) => new { expected, actual = subj.HasObservers, })
                    .All(x => x.expected == x.actual),
                Is.True);
        }

        /// <summary>
        /// Checks that equal priority across different keys can be randomized when enabled.
        /// </summary>
        [Test]
        public void EqualPriorityAcrossDifferentKeysCanBeRandomized()
        {
            using var scope = Assert.EnterMultipleScope();

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

            Assert.That(nextCountA + nextCountB, Is.EqualTo(2));
        }
    }
}
