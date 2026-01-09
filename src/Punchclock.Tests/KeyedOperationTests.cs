// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;

namespace Punchclock.Tests;

/// <summary>
/// Tests for <see cref="KeyedOperation"/> and related functionality.
/// </summary>
public class KeyedOperationTests
{
    /// <summary>
    /// Verifies that CompareTo returns 1 when other is null.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CompareTo_WithNull_ReturnsOne()
    {
        var op = CreateOperation(priority: 1, key: "test");
        var result = op.CompareTo(null);
        await Assert.That(result).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that non-keyed operations come before keyed operations.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CompareTo_NonKeyedBeforeKeyed()
    {
        using (Assert.Multiple())
        {
            var nonKeyed = CreateOperation(priority: 1, key: OperationQueue.DefaultKey);
            var keyed = CreateOperation(priority: 1, key: "custom");

            var result = nonKeyed.CompareTo(keyed);
            await Assert.That(result).IsLessThan(0); // Non-keyed should come first
        }
    }

    /// <summary>
    /// Verifies that higher priority operations come before lower priority ones.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CompareTo_HigherPriorityFirst()
    {
        using (Assert.Multiple())
        {
            var highPriority = CreateOperation(priority: 10, key: "test");
            var lowPriority = CreateOperation(priority: 1, key: "test");

            var result = highPriority.CompareTo(lowPriority);
            await Assert.That(result).IsLessThan(0); // Higher priority should come first
        }
    }

    /// <summary>
    /// Verifies that equal priority operations return 0 for FIFO handling.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CompareTo_EqualPriority_ReturnsZero()
    {
        using (Assert.Multiple())
        {
            var op1 = CreateOperation(priority: 5, key: "key1", id: 1);
            var op2 = CreateOperation(priority: 5, key: "key2", id: 2);

            var result = op1.CompareTo(op2);
            await Assert.That(result).IsEqualTo(0); // FIFO tiebreaker handled by PriorityQueue
        }
    }

    /// <summary>
    /// Verifies that random tiebreak works when enabled.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CompareTo_WithRandomTiebreak_UsesRandomOrder()
    {
        using (Assert.Multiple())
        {
            var op1 = CreateOperation(priority: 5, key: "key1", useRandom: true, randomOrder: 10);
            var op2 = CreateOperation(priority: 5, key: "key2", useRandom: true, randomOrder: 20);

            var result = op1.CompareTo(op2);
            await Assert.That(result).IsLessThan(0); // Lower random order comes first
        }
    }

    /// <summary>
    /// Verifies that KeyIsDefault returns true for null key.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task KeyIsDefault_WithNullKey_ReturnsTrue()
    {
        var op = CreateOperation(priority: 1, key: null);
        await Assert.That(op.KeyIsDefault).IsTrue();
    }

    /// <summary>
    /// Verifies that KeyIsDefault returns true for empty string.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task KeyIsDefault_WithEmptyKey_ReturnsTrue()
    {
        var op = CreateOperation(priority: 1, key: string.Empty);
        await Assert.That(op.KeyIsDefault).IsTrue();
    }

    /// <summary>
    /// Verifies that KeyIsDefault returns true for DefaultKey.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task KeyIsDefault_WithDefaultKey_ReturnsTrue()
    {
        var op = CreateOperation(priority: 1, key: OperationQueue.DefaultKey);
        await Assert.That(op.KeyIsDefault).IsTrue();
    }

    /// <summary>
    /// Verifies that KeyIsDefault returns false for custom key.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task KeyIsDefault_WithCustomKey_ReturnsFalse()
    {
        var op = CreateOperation(priority: 1, key: "custom");
        await Assert.That(op.KeyIsDefault).IsFalse();
    }

    /// <summary>
    /// Verifies that EvaluateFunc returns empty when CancelledEarly is true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EvaluateFunc_WhenCancelledEarly_ReturnsEmpty()
    {
        using (Assert.Multiple())
        {
            var op = CreateOperation(priority: 1, key: "test");
            op.CancelledEarly = true;

            var results = new List<System.Reactive.Unit>();
            op.EvaluateFunc().Subscribe(results.Add);

            await Assert.That(results).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies that EvaluateFunc executes the function when not cancelled.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EvaluateFunc_WhenNotCancelled_ExecutesFunction()
    {
        using (Assert.Multiple())
        {
            var executed = false;
            var op = CreateOperation(
                priority: 1,
                key: "test",
                func: () =>
                {
                    executed = true;
                    return Observable.Return(42);
                });

            var results = new List<System.Reactive.Unit>();
            op.EvaluateFunc().Subscribe(results.Add);

            await Assert.That(executed).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that EvaluateFunc respects cancel signal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EvaluateFunc_WithCancelSignal_Cancels()
    {
        using (Assert.Multiple())
        {
            var cancelSubject = new Subject<System.Reactive.Unit>();
            var completed = false;
            var op = CreateOperation(
                priority: 1,
                key: "test",
                func: () => Observable.Never<int>(),
                cancelSignal: cancelSubject);

            op.EvaluateFunc().Subscribe(_ => { }, () => completed = true);

            cancelSubject.OnNext(System.Reactive.Unit.Default);
            cancelSubject.OnCompleted();

            // TakeUntil completes synchronously when cancel signal completes
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that Result subject is created and accessible.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Result_IsAccessible()
    {
        var op = CreateOperation(priority: 1, key: "test");
        await Assert.That(op.Result).IsNotNull();
    }

    /// <summary>
    /// Covers KeyedOperation.cs line 127 - keyed operation after non-keyed in comparison.
    /// Verifies that when comparing two operations with equal priority, a keyed operation
    /// returns positive (comes after) when compared to a non-keyed operation.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CompareTo_KeyedAfterNonKeyed_ReturnsPositive()
    {
        var keyed = new KeyedOperation<int>
        {
            Priority = 1,
            Key = "custom-key",
            Id = 1,
            Func = () => Observable.Return(1),
        };

        var nonKeyed = new KeyedOperation<int>
        {
            Priority = 1,
            Key = OperationQueue.DefaultKey,
            Id = 2,
            Func = () => Observable.Return(2),
        };

        // keyed.CompareTo(nonKeyed) should return 1 (line 127: return 1)
        var result = keyed.CompareTo(nonKeyed);
        await Assert.That(result).IsGreaterThan(0);
    }

    /// <summary>
    /// Covers KeyedOperation.cs line 190 - EvaluateFunc with null Func.
    /// Verifies that when Func is null, EvaluateFunc returns an empty observable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EvaluateFunc_WithNullFunc_ReturnsEmpty()
    {
        var op = new KeyedOperation<int>
        {
            Priority = 1,
            Key = "test",
            Id = 1,
            Func = null, // Null func - should hit line 190
        };

        var results = new List<System.Reactive.Unit>();
        op.EvaluateFunc().Subscribe(results.Add);

        await Assert.That(results).IsEmpty();
    }

    /// <summary>
    /// Helper to create a KeyedOperation for testing.
    /// </summary>
    private static KeyedOperation<int> CreateOperation(
        int priority,
        string? key,
        int id = 0,
        bool useRandom = false,
        int randomOrder = 0,
        Func<IObservable<int>>? func = null,
        IObservable<System.Reactive.Unit>? cancelSignal = null)
    {
        return new KeyedOperation<int>
        {
            Priority = priority,
            Key = key,
            Id = id,
            UseRandomTiebreak = useRandom,
            RandomOrder = randomOrder,
            Func = func ?? (() => Observable.Return(42)),
            CancelSignal = cancelSignal,
        };
    }
}
