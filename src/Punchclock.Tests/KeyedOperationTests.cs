// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives.Signals;
#if REACTIVE_SHIM_TESTS
using Unit = System.Reactive.Unit;
#else
using Unit = ReactiveUI.Primitives.RxVoid;
#endif

namespace Punchclock.Tests;

/// <summary>Tests for <see cref="KeyedOperation" /> and related functionality.</summary>
public class KeyedOperationTests
{
    /// <summary>The default value emitted by helper observables.</summary>
    private const int DefaultValue = 42;

    /// <summary>The first operation identifier used in tests.</summary>
    private const int FirstOperationId = 1;

    /// <summary>The second operation identifier used in tests.</summary>
    private const int SecondOperationId = 2;

    /// <summary>The lower random ordering value used in tiebreak tests.</summary>
    private const int LowerRandomOrder = 10;

    /// <summary>The higher random ordering value used in tiebreak tests.</summary>
    private const int HigherRandomOrder = 20;

    /// <summary>Verifies that CompareTo returns 1 when other is null.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CompareTo_WithNull_ReturnsOne()
    {
        var op = CreateOperation(priority: FirstOperationId, key: "test");
        var result = op.CompareTo(null);
        await Assert.That(result).IsEqualTo(FirstOperationId);
    }

    /// <summary>Verifies that non-keyed operations come before keyed operations.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CompareTo_NonKeyedBeforeKeyed()
    {
        using (Assert.Multiple())
        {
            var nonKeyed = CreateOperation(priority: FirstOperationId, key: OperationQueue.DefaultKey);
            var keyed = CreateOperation(priority: FirstOperationId, key: "custom");

            var result = nonKeyed.CompareTo(keyed);
            await Assert.That(result).IsLessThan(0);
        }
    }

    /// <summary>Verifies that higher priority operations come before lower priority ones.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CompareTo_HigherPriorityFirst()
    {
        using (Assert.Multiple())
        {
            var highPriority = CreateOperation(priority: LowerRandomOrder, key: "test");
            var lowPriority = CreateOperation(priority: FirstOperationId, key: "test");

            var result = highPriority.CompareTo(lowPriority);
            await Assert.That(result).IsLessThan(0);
        }
    }

    /// <summary>Verifies that equal priority operations return 0 for FIFO handling.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CompareTo_EqualPriority_ReturnsZero()
    {
        using (Assert.Multiple())
        {
            var op1 = CreateOperation(priority: 5, key: "key1", id: FirstOperationId);
            var op2 = CreateOperation(priority: 5, key: "key2", id: SecondOperationId);

            var result = op1.CompareTo(op2);
            await Assert.That(result).IsEqualTo(0);
        }
    }

    /// <summary>Verifies that random tiebreak works when enabled.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CompareTo_WithRandomTiebreak_UsesRandomOrder()
    {
        using (Assert.Multiple())
        {
            var op1 = CreateOperation(priority: 5, key: "key1", useRandom: true, randomOrder: LowerRandomOrder);
            var op2 = CreateOperation(priority: 5, key: "key2", useRandom: true, randomOrder: HigherRandomOrder);

            var result = op1.CompareTo(op2);
            await Assert.That(result).IsLessThan(0);
        }
    }

    /// <summary>Verifies that KeyIsDefault returns true for null key.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task KeyIsDefault_WithNullKey_ReturnsTrue()
    {
        var op = CreateOperation(priority: FirstOperationId, key: null);
        await Assert.That(op.KeyIsDefault).IsTrue();
    }

    /// <summary>Verifies that KeyIsDefault returns true for empty string.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task KeyIsDefault_WithEmptyKey_ReturnsTrue()
    {
        var op = CreateOperation(priority: FirstOperationId, key: string.Empty);
        await Assert.That(op.KeyIsDefault).IsTrue();
    }

    /// <summary>Verifies that KeyIsDefault returns true for DefaultKey.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task KeyIsDefault_WithDefaultKey_ReturnsTrue()
    {
        var op = CreateOperation(priority: FirstOperationId, key: OperationQueue.DefaultKey);
        await Assert.That(op.KeyIsDefault).IsTrue();
    }

    /// <summary>Verifies that KeyIsDefault returns false for custom key.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task KeyIsDefault_WithCustomKey_ReturnsFalse()
    {
        var op = CreateOperation(priority: FirstOperationId, key: "custom");
        await Assert.That(op.KeyIsDefault).IsFalse();
    }

    /// <summary>Verifies that EvaluateFunc returns empty when CancelledEarly is true.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EvaluateFunc_WhenCancelledEarly_ReturnsEmpty()
    {
        using (Assert.Multiple())
        {
            var op = CreateOperation(priority: FirstOperationId, key: "test");
            op.CancelledEarly = true;

            var results = new List<Unit>();
            using var subscription = op.EvaluateFunc().Subscribe(results.Add);

            await Assert.That(results).IsEmpty();
        }
    }

    /// <summary>Verifies that EvaluateFunc executes the function when not cancelled.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EvaluateFunc_WhenNotCancelled_ExecutesFunction()
    {
        using (Assert.Multiple())
        {
            var executed = false;
            var op = CreateOperation(
                priority: FirstOperationId,
                key: "test",
                func: () =>
                {
                    executed = true;
                    return Signal.Emit(DefaultValue);
                });

            var results = new List<Unit>();
            using var subscription = op.EvaluateFunc().Subscribe(results.Add);

            await Assert.That(executed).IsTrue();
        }
    }

    /// <summary>Verifies that EvaluateFunc respects cancel signal.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EvaluateFunc_WithCancelSignal_Cancels()
    {
        using (Assert.Multiple())
        {
            using var cancelSubject = new Signal<Unit>();
            var completed = false;
            var op = CreateOperation(
                priority: FirstOperationId,
                key: "test",
                func: static () => Signal.Silent<int>(),
                cancelSignal: cancelSubject);

            using var subscription = op.EvaluateFunc().Subscribe(static _ => { }, () => completed = true);

            cancelSubject.OnNext(Unit.Default);
            cancelSubject.OnCompleted();

            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>Verifies that Result subject is created and accessible.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Result_IsAccessible()
    {
        var op = CreateOperation(priority: FirstOperationId, key: "test");
        await Assert.That(op.Result).IsNotNull();
    }

    /// <summary>Verifies that unmanaged-only disposal does not release the managed cancellation subscription.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dispose_WhenDisposingIsFalse_DoesNotDisposeManagedResources()
    {
        using var cancellationSubscription = new CancellationTokenSource();
        using var operation = new TestKeyedOperation { CancelSubscription = cancellationSubscription };

        operation.DisposeWithoutManagedResources();
        await cancellationSubscription.CancelAsync();

        await Assert.That(cancellationSubscription.IsCancellationRequested).IsTrue();
    }

    /// <summary>Verifies that keyed operations compare after non-keyed operations.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CompareTo_KeyedAfterNonKeyed_ReturnsPositive()
    {
        using var keyed = new KeyedOperation<int> { Priority = FirstOperationId, Key = "custom-key", Id = FirstOperationId, Func = static () => Signal.Emit(FirstOperationId) };
        using var nonKeyed = new KeyedOperation<int> { Priority = FirstOperationId, Key = OperationQueue.DefaultKey, Id = SecondOperationId, Func = static () => Signal.Emit(SecondOperationId) };

        var result = keyed.CompareTo(nonKeyed);
        await Assert.That(result).IsGreaterThan(0);
    }

    /// <summary>Verifies that a null Func causes EvaluateFunc to return an empty observable.</summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EvaluateFunc_WithNullFunc_ReturnsEmpty()
    {
        using var op = new KeyedOperation<int> { Priority = FirstOperationId, Key = "test", Id = FirstOperationId, Func = null };

        var results = new List<Unit>();
        using var subscription = op.EvaluateFunc().Subscribe(results.Add);

        await Assert.That(results).IsEmpty();
    }

    /// <summary>Helper to create a <see cref="KeyedOperation{T}" /> for testing.</summary>
    /// <param name="priority">The priority of the operation.</param>
    /// <param name="key">The key of the operation.</param>
    /// <param name="id">The ID of the operation.</param>
    /// <param name="useRandom">Whether to use random tiebreak.</param>
    /// <param name="randomOrder">The random order value.</param>
    /// <param name="func">The function to execute.</param>
    /// <param name="cancelSignal">The cancel signal observable.</param>
    /// <returns>A new instance of <see cref="KeyedOperation{T}" />.</returns>
    private static KeyedOperation<int> CreateOperation(
        int priority,
        string? key,
        int id = 0,
        bool useRandom = false,
        int randomOrder = 0,
        Func<IObservable<int>>? func = null,
        IObservable<Unit>? cancelSignal = null) => new()
        {
            Priority = priority,
            Key = key,
            Id = id,
            UseRandomTiebreak = useRandom,
            RandomOrder = randomOrder,
            Func = func ?? (static () => Signal.Emit(DefaultValue)),
            CancelSignal = cancelSignal,
        };

    /// <summary>Exposes the unmanaged-only disposal path for contract testing.</summary>
    private sealed class TestKeyedOperation : KeyedOperation
    {
        /// <summary>Invokes the disposal path that must not release managed resources.</summary>
        internal void DisposeWithoutManagedResources() => Dispose(false);

        /// <inheritdoc />
        internal override IObservable<Unit> EvaluateFunc() => Signal.None<Unit>();
    }
}
