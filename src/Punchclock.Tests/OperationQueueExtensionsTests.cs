// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Punchclock.Tests
{
    /// <summary>
    /// Tests for <see cref="OperationQueueExtensions"/> convenience APIs and related semantics.
    /// </summary>
    public class OperationQueueExtensionsTests
    {
        /// <summary>
        /// Verifies that passing a null queue throws <see cref="ArgumentNullException"/> with the correct parameter name.
        /// </summary>
        [Test]
        public void Enqueue_WithNullQueue_ThrowsArgumentNullException()
        {
            using var assertScope = Assert.EnterMultipleScope();

            OperationQueue? q = null;
            var ex1 = Assert.Throws<ArgumentNullException>(() => OperationQueueExtensions.Enqueue(q!, 1, () => Task.CompletedTask));
            Assert.That(ex1!.ParamName, Is.EqualTo("operationQueue"));

            var ex2 = Assert.Throws<ArgumentNullException>(() => OperationQueueExtensions.Enqueue(q!, 1, "k", () => Task.CompletedTask));
            Assert.That(ex2!.ParamName, Is.EqualTo("operationQueue"));

            var ex3 = Assert.Throws<ArgumentNullException>(() => OperationQueueExtensions.Enqueue<int>(q!, 1, () => Task.FromResult(42)));
            Assert.That(ex3!.ParamName, Is.EqualTo("operationQueue"));

            var ex4 = Assert.Throws<ArgumentNullException>(() => OperationQueueExtensions.Enqueue<int>(q!, 1, "k", () => Task.FromResult(42)));
            Assert.That(ex4!.ParamName, Is.EqualTo("operationQueue"));
        }

        /// <summary>
        /// Ensures Task-based overloads execute and return expected results.
        /// </summary>
        /// <returns>A task representing the async unit test.</returns>
        [Test]
        public async Task Enqueue_TaskOverloads_RunAndReturnResults()
        {
            using var assertScope = Assert.EnterMultipleScope();

            var q = new OperationQueue(2);

            await OperationQueueExtensions.Enqueue(q, 5, () => Task.CompletedTask);
            var r1 = await OperationQueueExtensions.Enqueue(q, 5, () => Task.FromResult(123));
            Assert.That(r1, Is.EqualTo(123));

            await OperationQueueExtensions.Enqueue(q, 5, "key", () => Task.CompletedTask);
            var r2 = await OperationQueueExtensions.Enqueue(q, 5, "key", () => Task.FromResult("hi"));
            Assert.That(r2, Is.EqualTo("hi"));
        }

        /// <summary>
        /// If the <see cref="CancellationToken"/> is already canceled, the returned task should be canceled immediately.
        /// </summary>
        [Test]
        public void Enqueue_WithAlreadyCanceledToken_CancelsImmediately()
        {
            using var assertScope = Assert.EnterMultipleScope();

            var q = new OperationQueue(1);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Task-returning overload
            var t1 = OperationQueueExtensions.Enqueue(q, 1, "k", () => Task.FromResult(1), cts.Token);
            Assert.Throws<TaskCanceledException>(() => t1.GetAwaiter().GetResult());

            var t2 = OperationQueueExtensions.Enqueue(q, 1, "k", () => Task.CompletedTask, cts.Token);
            Assert.Throws<TaskCanceledException>(() => t2.GetAwaiter().GetResult());
        }

        /// <summary>
        /// Pending operations should be canceled by the supplied token before evaluation starts.
        /// </summary>
        /// <returns>A task representing the async unit test.</returns>
        [Test]
        public async Task Enqueue_WithCancellationToken_CancelsPendingOperation()
        {
            using var assertScope = Assert.EnterMultipleScope();

            var q = new OperationQueue(1);

            // Block the queue with a subject that we complete later
            var gate = new Subject<int>();
            var hold = q.EnqueueObservableOperation(1, () => gate.AsObservable());
            using var sub = hold.Subscribe(_ => { });

            using var cts = new CancellationTokenSource();
            var started = false;
            var pending = OperationQueueExtensions.Enqueue(
                q,
                1,
                "foo",
                () =>
                {
                    started = true;
                    return Task.FromResult(42);
                },
                cts.Token);

            Assert.That(started, Is.False);
            cts.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(async () => await pending);

            Assert.That(started, Is.False);
            gate.OnNext(0);
            gate.OnCompleted();
        }

        /// <summary>
        /// Shutdown should complete once outstanding work finishes.
        /// </summary>
        /// <returns>A task representing the async unit test.</returns>
        [Test]
        public async Task ShutdownQueue_CompletesAfterOutstandingWork()
        {
            using var assertScope = Assert.EnterMultipleScope();

            var q = new OperationQueue(1);
            var tcs = new TaskCompletionSource<int>();
            var work = OperationQueueExtensions.Enqueue(q, 1, () => tcs.Task);

            var shutdownTcs = new TaskCompletionSource<bool>();
            using var sub = q.ShutdownQueue().Subscribe(
                _ => shutdownTcs.TrySetResult(true),
                ex => shutdownTcs.TrySetException(ex),
                () => shutdownTcs.TrySetResult(true));

            Assert.That(shutdownTcs.Task.IsCompleted, Is.False);

            tcs.SetResult(10);
            Assert.That(await work, Is.EqualTo(10));
            await shutdownTcs.Task; // should complete without throwing
        }

        /// <summary>
        /// PauseQueue should be ref-counted; resuming only when the last handle is disposed.
        /// </summary>
        [Test]
        public void PauseQueue_IsRefCounted()
        {
            using var assertScope = Assert.EnterMultipleScope();

            var q = new OperationQueue(1);

            var p1 = q.PauseQueue();
            var p2 = q.PauseQueue();

            // Enqueue work while paused; nothing should run until both are disposed
            var ran = false;
            var obs = q.EnqueueObservableOperation(1, () =>
                Observable.Defer(() =>
                {
                    ran = true;
                    return Observable.Return(1);
                }));

            using var sub = obs.Subscribe(_ => { });
            Assert.That(ran, Is.False);

            p1.Dispose();
            Assert.That(ran, Is.False);

            p2.Dispose();
            Assert.That(ran, Is.True);
        }
    }
}
