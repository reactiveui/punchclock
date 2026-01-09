// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Punchclock.Tests;

/// <summary>
/// Tests for <see cref="OperationQueueExtensions"/> convenience APIs and related semantics.
/// </summary>
[SuppressMessage("Reliability", "CA2025:Ensure tasks using 'IDisposable' instances complete before the instances are disposed", Justification = "Test methods ensure proper task completion and disposal ordering")]
public class OperationQueueExtensionsTests
{
    /// <summary>
    /// Verifies that passing a null queue throws <see cref="ArgumentNullException"/> with the correct parameter name.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WithNullQueue_ThrowsArgumentNullException()
    {
        using (Assert.Multiple())
        {
            OperationQueue? q = null;
            var ex1 = await Assert.That(() => OperationQueueExtensions.Enqueue(q!, 1, () => Task.CompletedTask))
                .Throws<ArgumentNullException>();
            await Assert.That(ex1!.ParamName).IsEqualTo("operationQueue");

            var ex2 = await Assert.That(() => OperationQueueExtensions.Enqueue(q!, 1, "k", () => Task.CompletedTask))
                .Throws<ArgumentNullException>();
            await Assert.That(ex2!.ParamName).IsEqualTo("operationQueue");

            var ex3 = await Assert.That(() => OperationQueueExtensions.Enqueue<int>(q!, 1, () => Task.FromResult(42)))
                .Throws<ArgumentNullException>();
            await Assert.That(ex3!.ParamName).IsEqualTo("operationQueue");

            var ex4 = await Assert.That(() => OperationQueueExtensions.Enqueue<int>(q!, 1, "k", () => Task.FromResult(42)))
                .Throws<ArgumentNullException>();
            await Assert.That(ex4!.ParamName).IsEqualTo("operationQueue");
        }
    }

    /// <summary>
    /// Ensures Task-based overloads execute and return expected results.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_TaskOverloads_RunAndReturnResults()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(2);

            await OperationQueueExtensions.Enqueue(q, 5, () => Task.CompletedTask);
            var r1 = await OperationQueueExtensions.Enqueue(q, 5, () => Task.FromResult(123));
            await Assert.That(r1).IsEqualTo(123);

            await OperationQueueExtensions.Enqueue(q, 5, "key", () => Task.CompletedTask);
            var r2 = await OperationQueueExtensions.Enqueue(q, 5, "key", () => Task.FromResult("hi"));
            await Assert.That(r2).IsEqualTo("hi");
        }
    }

    /// <summary>
    /// If the <see cref="CancellationToken"/> is already canceled, the returned task should be canceled immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WithAlreadyCanceledToken_CancelsImmediately()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(1);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Task-returning overload
            var t1 = OperationQueueExtensions.Enqueue(q, 1, "k", () => Task.FromResult(1), cts.Token);
            await Assert.That(() => t1.GetAwaiter().GetResult()).Throws<TaskCanceledException>();

            var t2 = OperationQueueExtensions.Enqueue(q, 1, "k", () => Task.CompletedTask, cts.Token);
            await Assert.That(() => t2.GetAwaiter().GetResult()).Throws<TaskCanceledException>();
        }
    }

    /// <summary>
    /// Pending operations should be canceled by the supplied token before evaluation starts.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WithCancellationToken_CancelsPendingOperation()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(1);

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

            await Assert.That(started).IsFalse();
            cts.Cancel();

            await Assert.That(async () => await pending).Throws<TaskCanceledException>();

            await Assert.That(started).IsFalse();
            gate.OnNext(0);
            gate.OnCompleted();
        }
    }

    /// <summary>
    /// Shutdown should complete once outstanding work finishes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ShutdownQueue_CompletesAfterOutstandingWork()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(1);
            var tcs = new TaskCompletionSource<int>();
            var work = OperationQueueExtensions.Enqueue(q, 1, () => tcs.Task);

            var shutdownTcs = new TaskCompletionSource<bool>();
            using var sub = q.ShutdownQueue().Subscribe(
                _ => shutdownTcs.TrySetResult(true),
                ex => shutdownTcs.TrySetException(ex),
                () => shutdownTcs.TrySetResult(true));

            await Assert.That(shutdownTcs.Task.IsCompleted).IsFalse();

            tcs.SetResult(10);
            await Assert.That(await work).IsEqualTo(10);
            await shutdownTcs.Task; // should complete without throwing
        }
    }

    /// <summary>
    /// PauseQueue should be ref-counted; resuming only when the last handle is disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task PauseQueue_IsRefCounted()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(1);

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
            await Assert.That(ran).IsFalse();

            p1.Dispose();
            await Assert.That(ran).IsFalse();

            p2.Dispose();
            await Assert.That(ran).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that <see cref="CancellationToken.None"/> takes the fast path without allocating observable machinery.
    /// The operation should complete successfully without any cancellation overhead.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WithCancellationTokenNone_TakesFastPath()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(2);

            // CancellationToken.None should take fast path
            var result1 = await OperationQueueExtensions.Enqueue(
                q,
                1,
                "key",
                () => Task.FromResult(42),
                CancellationToken.None);

            await Assert.That(result1).IsEqualTo(42);

            // Non-generic overload with CancellationToken.None
            var executed = false;
            await OperationQueueExtensions.Enqueue(
                q,
                1,
                "key",
                () =>
                {
                    executed = true;
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            await Assert.That(executed).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that a non-cancellable token (created with <c>new CancellationTokenSource()</c> but never cancelled)
    /// takes the fast path since it can never be cancelled.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WithNonCancellableToken_TakesFastPath()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(2);

            // Create a token source that will never be cancelled
            using var cts = new CancellationTokenSource();
            var token = cts.Token;

            var result = await OperationQueueExtensions.Enqueue(
                q,
                1,
                "key",
                () => Task.FromResult(123),
                token);

            await Assert.That(result).IsEqualTo(123);
            await Assert.That(cts.IsCancellationRequested).IsFalse();
        }
    }

    /// <summary>
    /// Verifies that an already-cancelled token throws <see cref="OperationCanceledException"/>
    /// (not <see cref="ArgumentException"/>) when the observable is subscribed to.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WithAlreadyCanceledToken_ThrowsOperationCanceledException()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(1);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Should throw OperationCanceledException, not ArgumentException
            var task = OperationQueueExtensions.Enqueue(
                q,
                1,
                "key",
                () => Task.FromResult(42),
                cts.Token);

            var ex = await Assert.That(() => task).Throws<OperationCanceledException>();
            await Assert.That(ex!.CancellationToken).IsEqualTo(cts.Token);
        }
    }

    /// <summary>
    /// Verifies that multiple operations with <see cref="CancellationToken.None"/> execute correctly
    /// in parallel without interference.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_MultipleOperationsWithTokenNone_ExecuteInParallel()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(4);

            // Queue multiple operations with CancellationToken.None
            var tasks = Enumerable.Range(0, 10)
                .Select(i => OperationQueueExtensions.Enqueue(
                    q,
                    1,
                    $"key{i}",
                    () => Task.FromResult(i * 2),
                    CancellationToken.None))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            // Verify all results
            for (var i = 0; i < 10; i++)
            {
                await Assert.That(results[i]).IsEqualTo(i * 2);
            }
        }
    }

    /// <summary>
    /// Verifies that mixing operations with <see cref="CancellationToken.None"/> and cancellable tokens
    /// works correctly, with only the cancellable operations being affected by cancellation.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_MixedCancellableAndNonCancellable_WorksCorrectly()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(1);

            // Block the queue
            var gate = new Subject<int>();
            var hold = q.EnqueueObservableOperation(1, () => gate.AsObservable());
            using var sub = hold.Subscribe(_ => { });

            // Enqueue with CancellationToken.None
            var nonCancellable = OperationQueueExtensions.Enqueue(
                q,
                1,
                "noncancellable",
                () => Task.FromResult(1),
                CancellationToken.None);

            // Enqueue with cancellable token
            using var cts = new CancellationTokenSource();
            var cancellable = OperationQueueExtensions.Enqueue(
                q,
                1,
                "cancellable",
                () => Task.FromResult(2),
                cts.Token);

            // Cancel the cancellable token
            cts.Cancel();

            // Release the queue
            gate.OnNext(0);
            gate.OnCompleted();

            // Non-cancellable should succeed
            var result = await nonCancellable;
            await Assert.That(result).IsEqualTo(1);

            // Cancellable should be cancelled
            await Assert.That(() => cancellable).Throws<TaskCanceledException>();
        }
    }

    /// <summary>
    /// Covers OperationQueueExtensions lines 108/109 - normal cancellation token path.
    /// Verifies that a cancellable token that is not cancelled executes the operation normally.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WithCancellableTokenNotCancelled_ExecutesNormally()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(2);
            using var cts = new CancellationTokenSource();

            // Token is cancellable but not cancelled - should use normal path (lines 108/109)
            var result = await queue.Enqueue(1, "key", () => Task.FromResult(42), cts.Token);

            await Assert.That(result).IsEqualTo(42);
            await Assert.That(cts.IsCancellationRequested).IsFalse();
        }
    }

    /// <summary>
    /// Covers OperationQueueExtensions lines 266/267 - cancellation during operation execution.
    /// Verifies that cancelling a token after the operation has been enqueued but before execution
    /// completes properly cancels the task.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WithTokenCancelledDuringExecution_CancelsTask()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(1);

            // Block the queue
            var blocker = new Subject<int>();
            queue.EnqueueObservableOperation(1, () => blocker).Subscribe();

            using var cts = new CancellationTokenSource();

            // Enqueue with cancellable token - will enter Observable.Create path (lines 261-268)
            var task = queue.Enqueue(
                1,
                "key",
                async () =>
                {
                    await Task.Delay(5000); // Long delay
                    return 42;
                },
                cts.Token);

            // Cancel after a short delay - this tests the cancellation registration path
            await Task.Delay(50);
            cts.Cancel();

            await Assert.That(async () => await task).Throws<TaskCanceledException>();

            blocker.OnCompleted(); // Unblock queue for cleanup
        }
    }
}
