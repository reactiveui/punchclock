// Copyright (c) 2025 .NET Foundation and Contributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
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
using Shouldly; // use Shouldly assertions

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

            // Alright, we've got the first two subjects taking up our two live
            // slots, and 3,4,5 queued up. However, the order of completion should
            // be "4,3,5" because of the priority.
            outputs.All(x => x.Count == 0).ShouldBeTrue();

            subjects[0].OnNext(42);
            subjects[0].OnCompleted();
            outputs.Select(x => x.Count).ToArray().ShouldBe([1, 0, 0, 0, 0,]);

            // 0 => completed, 1,3 => live, 2,4 => queued. Make sure 4 *doesn't* fire because
            // the priority should invert it.
            subjects[4].OnNext(42);
            subjects[4].OnCompleted();
            outputs.Select(x => x.Count).ToArray().ShouldBe([1, 0, 0, 0, 0,]);

            // At the end, 0,1 => completed, 3,2 => live, 4 is queued
            subjects[1].OnNext(42);
            subjects[1].OnCompleted();
            outputs.Select(x => x.Count).ToArray().ShouldBe([1, 1, 0, 0, 0,]);

            // At the end, 0,1,2,4 => completed, 3 is live (remember, we completed
            // 4 early)
            subjects[2].OnNext(42);
            subjects[2].OnCompleted();
            outputs.Select(x => x.Count).ToArray().ShouldBe([1, 1, 1, 0, 1,]);

            subjects[3].OnNext(42);
            subjects[3].OnCompleted();
            outputs.Select(x => x.Count).ToArray().ShouldBe([1, 1, 1, 1, 1,]);
        }

        /// <summary>
        /// Checks to make sure that keyed items are serialized.
        /// </summary>
        [Test]
        public void KeyedItemsShouldBeSerialized()
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

            subscribeCount1.ShouldBe(0);
            subscribeCount2.ShouldBe(0);

            // Dispatch both subj1 and subj2, we should end up with input1 live,
            // but input2 in queue because of the key
            subj1.OnNext(42);
            subj1.OnCompleted();
            subj2.OnNext(42);
            subj2.OnCompleted();
            subscribeCount1.ShouldBe(1);
            subscribeCount2.ShouldBe(0);
            out1.ShouldBeEmpty();
            out2.ShouldBeEmpty();

            // Dispatch input1, input2 can now execute
            input1Subj.OnNext(42);
            input1Subj.OnCompleted();
            subscribeCount1.ShouldBe(1);
            subscribeCount2.ShouldBe(1);
            out1.Count.ShouldBe(1);
            out2.ShouldBeEmpty();

            // Dispatch input2, everything is finished
            input2Subj.OnNext(42);
            input2Subj.OnCompleted();
            subscribeCount1.ShouldBe(1);
            subscribeCount2.ShouldBe(1);
            out1.Count.ShouldBe(1);
            out2.Count.ShouldBe(1);
        }

        /// <summary>
        /// Checks to make sure that non key items are run in parallel.
        /// </summary>
        [Test]
        public void NonkeyedItemsShouldRunInParallel()
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
            unkeyed1SubCount.ShouldBe(0);
            unkeyed2SubCount.ShouldBe(0);

            fixture.EnqueueObservableOperation(5, () => unkeyed1);
            fixture.EnqueueObservableOperation(5, () => unkeyed2);
            unkeyed1SubCount.ShouldBe(1);
            unkeyed2SubCount.ShouldBe(1);
        }

        /// <summary>
        /// Checks to make sure that shutdown signals once everything completes.
        /// </summary>
        [Test]
        public void ShutdownShouldSignalOnceEverythingCompletes()
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

            outputs.All(x => x.Count == 0).ShouldBeTrue();
            shutdown.ShouldBeEmpty();

            for (int i = 0; i < 4; i++)
            {
                subjects[i].OnNext(42);
                subjects[i].OnCompleted();
            }

            shutdown.ShouldBeEmpty();

            // Complete the last one, that should signal that we're shut down
            subjects[4].OnNext(42);
            subjects[4].OnCompleted();
            outputs.All(x => x.Count == 1).ShouldBeTrue();
            shutdown.Count.ShouldBe(1);
        }

        /// <summary>
        /// Checks to make sure that the queue holds items until unpaused.
        /// </summary>
        [Test]
        public void PausingTheQueueShouldHoldItemsUntilUnpaused()
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

            prePauseOutput.Count.ShouldBe(2);

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

            pauseOutput.ShouldBeEmpty();

            var unpause2 = fixture.PauseQueue();
            pauseOutput.ShouldBeEmpty();

            unpause1.Dispose();
            pauseOutput.ShouldBeEmpty();

            unpause2.Dispose();
            pauseOutput.Count.ShouldBe(2);
        }

        /// <summary>
        /// Checks that cancelling items should not result in them being returned.
        /// </summary>
        [Test]
        public void CancellingItemsShouldNotResultInThemBeingReturned()
        {
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

            // Still blocked by subj1,2
            output.ShouldBeEmpty();

            // Still blocked by subj1,2, only baz is in queue
            cancel1.OnNext(Unit.Default);
            cancel1.OnCompleted();
            output.ShouldBeEmpty();

            // foo was cancelled, baz is still good
            subj1.OnNext(42);
            subj1.OnCompleted();
            output.Count.ShouldBe(1);

            // don't care that cancelled item finished
            item1.OnNext(42);
            item1.OnCompleted();
            output.Count.ShouldBe(1);

            // still shouldn't see anything
            subj2.OnNext(42);
            subj2.OnCompleted();
            output.Count.ShouldBe(1);
        }

        /// <summary>
        /// Checks that the cancelling of items, that the items won't be evaluated.
        /// </summary>
        [Test]
        public void CancellingItemsShouldntEvenBeEvaluated()
        {
            var subj1 = new AsyncSubject<int>();
            var subj2 = new AsyncSubject<int>();

            var fixture = new OperationQueue(2);

            // Block up the queue
            foreach (var v in new[] { subj1, subj2, })
            {
                fixture.EnqueueObservableOperation(5, () => v);
            }

            var cancel1 = new Subject<Unit>();
            bool wasCalled = false;
            var item1 = new AsyncSubject<int>();

            fixture.EnqueueObservableOperation(5, "foo", cancel1, () =>
            {
                wasCalled = true;
                return item1;
            }).ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
              .Bind(out var output).Subscribe();

            // Still blocked by subj1,2
            output.ShouldBeEmpty();
            wasCalled.ShouldBeFalse();

            // Still blocked by subj1,2 - however, we've cancelled foo before
            // it even had a chance to run - if that's the case, we shouldn't
            // even call the evaluation func
            cancel1.OnNext(Unit.Default);
            cancel1.OnCompleted();
            output.ShouldBeEmpty();
            wasCalled.ShouldBeFalse();

            // Unblock subj1,2, we still shouldn't see wasCalled = true
            subj1.OnNext(42);
            subj1.OnCompleted();
            output.ShouldBeEmpty();
            wasCalled.ShouldBeFalse();

            subj2.OnNext(42);
            subj2.OnCompleted();
            output.ShouldBeEmpty();
            wasCalled.ShouldBeFalse();
        }

        /// <summary>
        /// Checks to make sure the queue respects maximum concurrency.
        /// </summary>
        [Test]
        public void QueueShouldRespectMaximumConcurrent()
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
            unkeyed1SubCount.ShouldBe(0);
            unkeyed2SubCount.ShouldBe(0);
            unkeyed3SubCount.ShouldBe(0);

            fixture.EnqueueObservableOperation(5, () => unkeyed1);
            fixture.EnqueueObservableOperation(5, () => unkeyed2);
            fixture.EnqueueObservableOperation(5, () => unkeyed3);

            unkeyed1SubCount.ShouldBe(1);
            unkeyed2SubCount.ShouldBe(1);
            unkeyed3SubCount.ShouldBe(0);
        }

        /// <summary>
        /// Checks to see if the maximum concurrency is increased that the existing queue adapts.
        /// </summary>
        [Test]
        public void ShouldBeAbleToIncreaseTheMaximunConcurrentValueOfAnExistingQueue()
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
            unkeyed1SubCount.ShouldBe(0);
            unkeyed2SubCount.ShouldBe(0);
            unkeyed3SubCount.ShouldBe(0);
            unkeyed4SubCount.ShouldBe(0);

            fixture.EnqueueObservableOperation(5, () => unkeyed1);
            fixture.EnqueueObservableOperation(5, () => unkeyed2);
            fixture.EnqueueObservableOperation(5, () => unkeyed3);
            fixture.EnqueueObservableOperation(5, () => unkeyed4);

            unkeyed1SubCount.ShouldBe(1);
            unkeyed2SubCount.ShouldBe(1);
            unkeyed3SubCount.ShouldBe(0);
            unkeyed4SubCount.ShouldBe(0);

            fixture.SetMaximumConcurrent(3);

            unkeyed1SubCount.ShouldBe(1);
            unkeyed2SubCount.ShouldBe(1);
            unkeyed3SubCount.ShouldBe(1);
            unkeyed4SubCount.ShouldBe(0);
        }

        /// <summary>
        /// Checks to make sure that decreasing the maximum concurrency the queue adapts.
        /// </summary>
        [Test]
        public void ShouldBeAbleToDecreaseTheMaximunConcurrentValueOfAnExistingQueue()
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

            new[] { true, true, true, false, false, false, }.Zip(
                    subjects,
                    (expected, subj) => new { expected, actual = subj.HasObservers, })
                .All(x => x.expected == x.actual).ShouldBeTrue();

            fixture.SetMaximumConcurrent(2);

            // Complete the first one, the last three subjects should still have
            // no observers because we reduced maximum concurrent
            subjects[0].OnNext(42);
            subjects[0].OnCompleted();

            new[] { false, true, true, false, false, false, }.Zip(
                    subjects,
                    (expected, subj) => new { expected, actual = subj.HasObservers, })
                .All(x => x.expected == x.actual).ShouldBeTrue();

            // Complete subj[1], now 2,3 are live
            subjects[1].OnNext(42);
            subjects[1].OnCompleted();

            new[] { false, false, true, true, false, false, }.Zip(
                    subjects,
                    (expected, subj) => new { expected, actual = subj.HasObservers, })
                .All(x => x.expected == x.actual).ShouldBeTrue();
        }
    }
}
