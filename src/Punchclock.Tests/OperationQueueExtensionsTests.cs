// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !REACTIVE_SHIM_TESTS
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
#endif
using ReactiveUI.Primitives.Signals;
#if REACTIVE_SHIM_TESTS
using QueueScheduler = System.Reactive.Concurrency.ImmediateScheduler;
using RxVoid = System.Reactive.Unit;
#else
using QueueScheduler = ReactiveUI.Primitives.Concurrency.ImmediateSequencer;
using RxVoid = ReactiveUI.Primitives.RxVoid;
#endif

namespace Punchclock.Tests;

/// <summary>Tests for <see cref="OperationQueueExtensions"/> convenience APIs and related semantics.</summary>
public class OperationQueueExtensionsTests
{
    /// <summary>The value 0.</summary>
    private const int Zero = 0;

    /// <summary>The value 1.</summary>
    private const int One = 1;

    /// <summary>The value 2.</summary>
    private const int Two = 2;

    /// <summary>The value 4.</summary>
    private const int Four = 4;

    /// <summary>The value 5.</summary>
    private const int Five = 5;

    /// <summary>The value 10.</summary>
    private const int Ten = 10;

    /// <summary>The value 20.</summary>
    private const int Twenty = 20;

    /// <summary>The value 42.</summary>
    private const int FortyTwo = 42;

    /// <summary>The value 123.</summary>
    private const int OneHundredAndTwentyThree = 123;

    /// <summary>Verifies that passing a null queue throws <see cref="ArgumentNullException"/> with the correct parameter name.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WithNullQueue_ThrowsArgumentNullException()
    {
        using (Assert.Multiple())
        {
            OperationQueue? q = null;
            var ex1 = await Assert.That(() => OperationQueueExtensions.Enqueue(q!, One, static () => Task.CompletedTask))
                .Throws<ArgumentNullException>();
            const string Expected = "operationQueue";
            await Assert.That(ex1!.ParamName).IsEqualTo(Expected);

            var ex2 = await Assert.That(() => OperationQueueExtensions.Enqueue(q!, One, "k", static () => Task.CompletedTask))
                .Throws<ArgumentNullException>();
            await Assert.That(ex2!.ParamName).IsEqualTo(Expected);

            var ex3 = await Assert.That(() => OperationQueueExtensions.Enqueue(q!, One, static () => Task.FromResult(FortyTwo)))
                .Throws<ArgumentNullException>();
            await Assert.That(ex3!.ParamName).IsEqualTo(Expected);

            var ex4 = await Assert.That(() => OperationQueueExtensions.Enqueue(q!, One, "k", static () => Task.FromResult(FortyTwo)))
                .Throws<ArgumentNullException>();
            await Assert.That(ex4!.ParamName).IsEqualTo(Expected);
        }
    }

    /// <summary>Ensures Task-based overloads execute and return expected results.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_TaskOverloads_RunAndReturnResults()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(Two);

            await q.Enqueue(Five, static () => Task.CompletedTask);
            var r1 = await q.Enqueue(Five, static () => Task.FromResult(OneHundredAndTwentyThree));
            await Assert.That(r1).IsEqualTo(OneHundredAndTwentyThree);

            await q.Enqueue(Five, "key", static () => Task.CompletedTask);
            var r2 = await q.Enqueue(Five, "key", static () => Task.FromResult("hi"));
            await Assert.That(r2).IsEqualTo("hi");
        }
    }

    /// <summary>If the <see cref="CancellationToken"/> is already canceled, the returned task should be canceled immediately.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WithAlreadyCanceledToken_CancelsImmediately()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(One);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Task-returning overload
            var t1 = q.Enqueue(One, "k", static () => Task.FromResult(One), cts.Token);
            await Assert.That(() => t1).Throws<TaskCanceledException>();

            var t2 = q.Enqueue(One, "k", static () => Task.CompletedTask, cts.Token);
            await Assert.That(() => t2).Throws<TaskCanceledException>();
        }
    }

    /// <summary>Pending operations should be canceled by the supplied token before evaluation starts.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WithCancellationToken_CancelsPendingOperation()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(One);

            // Block the queue with a subject that we complete later
            using var gate = new Signal<int>();
            var hold = q.EnqueueObservableOperation(One, () => gate);
            using var sub = ObservableExtensions.Subscribe(hold, static _ => { });

            using var cts = new CancellationTokenSource();
            var started = false;
            var pending = q.Enqueue(
                One,
                "foo",
                () =>
                {
                    started = true;
                    return Task.FromResult(FortyTwo);
                },
                cts.Token);

            await Assert.That(started).IsFalse();
            await cts.CancelAsync();

            await Assert.That(() => pending).Throws<TaskCanceledException>();

            await Assert.That(started).IsFalse();
            gate.OnNext(Zero);
            gate.OnCompleted();
        }
    }

    /// <summary>Shutdown should complete once outstanding work finishes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ShutdownQueue_CompletesAfterOutstandingWork()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(One);
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var work = q.Enqueue(One, () => tcs.Task);

            var shutdownTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var sub = ObservableExtensions.Subscribe(
                q.ShutdownQueue(),
                _ => shutdownTcs.TrySetResult(true),
                ex => shutdownTcs.TrySetException(ex),
                () => shutdownTcs.TrySetResult(true));

            await Assert.That(shutdownTcs.Task.IsCompleted).IsFalse();

            tcs.SetResult(Ten);
            await Assert.That(await work).IsEqualTo(Ten);
            await shutdownTcs.Task; // should complete without throwing
        }
    }

    /// <summary>PauseQueue should be ref-counted; resuming only when the last handle is disposed.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task PauseQueue_IsRefCounted()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(One);

            var p1 = q.PauseQueue();
            var p2 = q.PauseQueue();

            // Enqueue work while paused; nothing should run until both are disposed
            var ran = false;
            var obs = q.EnqueueObservableOperation(One, () =>
                Signal.Defer(() =>
                {
                    ran = true;
                    return Signal.Emit(One);
                }));

            using var sub = ObservableExtensions.Subscribe(obs, static _ => { });
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
            using var q = new OperationQueue(Two);

            // CancellationToken.None should take fast path
            var result1 = await q.Enqueue(
                One,
                "key",
                static () => Task.FromResult(FortyTwo),
                CancellationToken.None);

            await Assert.That(result1).IsEqualTo(FortyTwo);

            // Non-generic overload with CancellationToken.None
            var executed = false;
            await q.Enqueue(
                One,
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
            using var q = new OperationQueue(Two);

            // Create a token source that will never be cancelled
            using var cts = new CancellationTokenSource();
            var token = cts.Token;

            var result = await q.Enqueue(
                One,
                "key",
                static () => Task.FromResult(OneHundredAndTwentyThree),
                token);

            await Assert.That(result).IsEqualTo(OneHundredAndTwentyThree);
            await Assert.That(cts.IsCancellationRequested).IsFalse();
        }
    }

    /// <summary>Verifies that an already-cancelled token throws <see cref="OperationCanceledException"/> (not <see cref="ArgumentException"/>) when the observable is subscribed to.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_WithAlreadyCanceledToken_ThrowsOperationCanceledException()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(One);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Should throw OperationCanceledException, not ArgumentException
            var task = q.Enqueue(
                One,
                "key",
                static () => Task.FromResult(FortyTwo),
                cts.Token);

            var ex = await Assert.That(() => task).Throws<OperationCanceledException>();
            await Assert.That(ex!.CancellationToken).IsEqualTo(cts.Token);
        }
    }

    /// <summary>Verifies that multiple operations with <see cref="CancellationToken.None"/> execute correctly in parallel without interference.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_MultipleOperationsWithTokenNone_ExecuteInParallel()
    {
        using (Assert.Multiple())
        {
            using var q = new OperationQueue(Four);

            // Queue multiple operations with CancellationToken.None
            var tasks = new Task<int>[Ten];

            for (var i = Zero; i < Ten; i++)
            {
                var index = i;
                tasks[index] = q.Enqueue(
                    One,
                    $"key{index}",
                    () => Task.FromResult(index * Two),
                    CancellationToken.None);
            }

            var results = await Task.WhenAll(tasks);

            // Verify all results
            for (var i = Zero; i < Ten; i++)
            {
                await Assert.That(results[i]).IsEqualTo(i * Two);
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
            using var q = new OperationQueue(One);

            // Block the queue
            using var gate = new Signal<int>();
            var hold = q.EnqueueObservableOperation(One, () => gate);
            using var sub = ObservableExtensions.Subscribe(hold, static _ => { });

            // Enqueue with CancellationToken.None
            var nonCancellable = q.Enqueue(
                One,
                "noncancellable",
                static () => Task.FromResult(One),
                CancellationToken.None);

            // Enqueue with cancellable token
            using var cts = new CancellationTokenSource();
            var cancellable = q.Enqueue(
                One,
                "cancellable",
                static () => Task.FromResult(Two),
                cts.Token);

            // Cancel the cancellable token
            await cts.CancelAsync();

            // Release the queue
            gate.OnNext(Zero);
            gate.OnCompleted();

            // Non-cancellable should succeed
            var result = await nonCancellable;
            await Assert.That(result).IsEqualTo(One);

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
            using var queue = new OperationQueue(Two);
            using var cts = new CancellationTokenSource();

            // Token is cancellable but not cancelled - should use normal path (lines 108/109)
            var result = await queue.Enqueue(One, "key", static () => Task.FromResult(FortyTwo), cts.Token);

            await Assert.That(result).IsEqualTo(FortyTwo);
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
            using var queue = new OperationQueue(One, QueueScheduler.Instance);

            // Block the queue
            var blocker = new Signal<int>();
            using var blockerSubscription = ObservableExtensions.Subscribe(queue.EnqueueObservableOperation(One, () => blocker), static _ => { });

            using var cts = new CancellationTokenSource();

            // Enqueue with cancellable token - operation won't start until queue is unblocked
            var task = queue.Enqueue(
                One,
                "key",
                static () => Task.FromResult(FortyTwo),
                cts.Token);

            // Cancel before the operation starts
            await cts.CancelAsync();

            await Assert.That(() => task).Throws<TaskCanceledException>();

            blocker.OnCompleted(); // Unblock queue for cleanup
        }
    }

    /// <summary>
    /// Covers OperationQueueExtensions line 249 - ConvertTokenToObservable with non-cancellable token.
    /// Verifies that non-cancellable tokens return Observable.Never (never completes).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ConvertTokenToObservable_WithNonCancellableToken_ReturnsNever()
    {
        using (Assert.Multiple())
        {
            var token = CancellationToken.None;
            var observable = OperationQueueExtensions.ConvertTokenToObservable(token);

            var completed = false;
            List<RxVoid> receivedValues = [];

            using var subscription = ObservableExtensions.Subscribe(
                observable,
                receivedValues.Add,
                static _ => { },
                () => completed = true);

            // Observable.Never never emits or completes
            await Assert.That(receivedValues).IsEmpty();
            await Assert.That(completed).IsFalse();
        }
    }

    /// <summary>
    /// Covers OperationQueueExtensions lines 255/257 - ConvertTokenToObservable with already-cancelled token.
    /// Verifies that already-cancelled tokens return Observable.Throw immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ConvertTokenToObservable_WithAlreadyCancelledToken_ThrowsImmediately()
    {
        using (Assert.Multiple())
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var observable = OperationQueueExtensions.ConvertTokenToObservable(cts.Token);

            Exception? caughtException = null;
            using var subscription = ObservableExtensions.Subscribe(
                observable,
                static _ => { },
                ex => caughtException = ex);

            // Observable.Throw emits error synchronously
            await Assert.That(caughtException).IsNotNull();
            await Assert.That(caughtException).IsTypeOf<OperationCanceledException>();
        }
    }

    /// <summary>
    /// Covers the normal cancellation path - token registration and callback.
    /// Verifies that cancelling a token after subscription emits <see cref="RxVoid.Default"/> and completes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ConvertTokenToObservable_WithCancellableToken_CanBeCancelled()
    {
        using (Assert.Multiple())
        {
            using var cts = new CancellationTokenSource();

            var observable = OperationQueueExtensions.ConvertTokenToObservable(cts.Token);

            List<RxVoid> receivedValues = [];
            var completed = false;

            using var subscription = ObservableExtensions.Subscribe(
                observable,
                receivedValues.Add,
                static _ => { },
                () => completed = true);

            // Cancel the token - should emit RxVoid.Default and complete synchronously
            await cts.CancelAsync();

            await Assert.That(receivedValues).Count().IsEqualTo(One);
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>
    /// Covers lines 108-109 - normal enqueue path with cancellable token (non-generic overload).
    /// Verifies that operations with cancellable tokens that don't get cancelled execute normally.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_NonGeneric_WithCancellableTokenNotCancelled_ExecutesOperation()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(Two, QueueScheduler.Instance);
            using var cts = new CancellationTokenSource();

            var executed = false;

            // Enqueue with cancellable token but don't cancel it (non-generic overload)
            await queue.Enqueue(
                One,
                "key",
                () =>
                {
                    executed = true;
                    return Task.CompletedTask;
                },
                cts.Token);

            await Assert.That(executed).IsTrue();
            await Assert.That(cts.IsCancellationRequested).IsFalse();
        }
    }

    /// <summary>
    /// Covers lines 108-109 - normal enqueue path with cancellable token (generic overload).
    /// Verifies that operations with cancellable tokens that don't get cancelled execute normally.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_Generic_WithCancellableTokenNotCancelled_ExecutesOperation()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(Two, QueueScheduler.Instance);
            using var cts = new CancellationTokenSource();

            // Enqueue with cancellable token but don't cancel it (generic overload)
            var result = await queue.Enqueue(One, "key", static () => Task.FromResult(FortyTwo), cts.Token);

            await Assert.That(result).IsEqualTo(FortyTwo);
            await Assert.That(cts.IsCancellationRequested).IsFalse();
        }
    }

#if !REACTIVE_SHIM_TESTS
    /// <summary>
    /// Covers lines 264/266-267 (now 281/283-284) - race condition where token is cancelled
    /// between ConvertTokenToObservable call and Observable.Create subscription.
    /// Uses TestScheduler to control when the cancellation happens.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ConvertTokenToObservable_WithRaceCancellation_HandlesCorrectly()
    {
        using (Assert.Multiple())
        {
            var testScheduler = new VirtualClock();
            using var cts = new CancellationTokenSource();

            // Schedule cancellation to happen during subscription
            _ = testScheduler.ScheduleRelative(TimeSpan.FromTicks(Five), cts.Cancel);

            var observable = OperationQueueExtensions.ConvertTokenToObservable(testScheduler, cts.Token);

            Exception? caughtException = null;
            List<RxVoid> receivedValues = [];
            IDisposable? subscription = null;

            // Subscribe at time 0
            _ = testScheduler.ScheduleRelative(TimeSpan.FromTicks(Ten), () =>
            {
                subscription = ObservableExtensions.Subscribe(
                    observable,
                    receivedValues.Add,
                    ex => caughtException = ex);
            });

            // Advance scheduler to trigger subscription and cancellation
            testScheduler.AdvanceBy(TimeSpan.FromTicks(Twenty));

            // Should have caught the race condition and errored
            await Assert.That(caughtException).IsNotNull();
            await Assert.That(caughtException).IsTypeOf<OperationCanceledException>();
            await Assert.That(receivedValues).IsEmpty();
            subscription?.Dispose();
        }
    }
#endif

    /// <summary>
    /// Verifies line 103 - both branches of IsCancellationRequested check.
    /// Tests both the true case (already cancelled) and false case (not cancelled).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Enqueue_CancellationTokenCheck_HandlesBothPaths()
    {
        using (Assert.Multiple())
        {
            using var queue = new OperationQueue(Two, QueueScheduler.Instance);

            // Test false branch (not cancelled) - lines 108-109
            using var cts1 = new CancellationTokenSource();
            var result1 = await queue.Enqueue(One, "key1", static () => Task.FromResult(One), cts1.Token);
            await Assert.That(result1).IsEqualTo(One);

            // Test true branch (already cancelled) - line 105
            using var cts2 = new CancellationTokenSource();
            await cts2.CancelAsync();
            await Assert.That(() =>
                queue.Enqueue(One, "key2", static () => Task.FromResult(Two), cts2.Token))
                .Throws<TaskCanceledException>();
        }
    }
}
