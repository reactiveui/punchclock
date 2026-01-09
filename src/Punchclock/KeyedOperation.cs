// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

namespace Punchclock;

/// <summary>
/// Base class for operations that can be enqueued in an <see cref="OperationQueue"/>.
/// Supports priority ordering, key-based serialization, and cancellation.
/// </summary>
internal abstract class KeyedOperation : IComparable<KeyedOperation>
{
    /// <summary>
    /// Gets or sets a value indicating whether this operation was cancelled before execution started.
    /// </summary>
    /// <value>
    /// <c>true</c> if the operation was cancelled early; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// This property has a setter because it is mutated after construction when cancellation occurs.
    /// </remarks>
    public bool CancelledEarly { get; set; }

    /// <summary>
    /// Gets the priority of this operation. Higher values indicate higher priority.
    /// </summary>
    /// <value>
    /// The priority value. Higher numbers are dequeued before lower numbers.
    /// </value>
    public int Priority { get; init; }

    /// <summary>
    /// Gets the unique identifier for this operation.
    /// Used for stable FIFO ordering among equal-priority operations.
    /// </summary>
    /// <value>
    /// The operation identifier, assigned sequentially at enqueue time.
    /// </value>
    public int Id { get; init; }

    /// <summary>
    /// Gets the key for this operation. Operations with the same key are executed serially.
    /// </summary>
    /// <value>
    /// The key string, or null/empty/<see cref="OperationQueue.DefaultKey"/> for non-keyed operations.
    /// </value>
    public string? Key { get; init; }

    /// <summary>
    /// Gets the observable that signals cancellation of this operation.
    /// </summary>
    /// <value>
    /// An observable that emits when cancellation is requested, or null if no cancellation signal is provided.
    /// </value>
    public IObservable<Unit>? CancelSignal { get; init; }

    /// <summary>
    /// Gets a value indicating whether this operation uses the default (non-keyed) key.
    /// Non-keyed operations can run concurrently with other non-keyed operations.
    /// </summary>
    /// <value>
    /// <c>true</c> if this operation has no key or uses the default key; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// This computed property is a hot path - the JIT will inline the getter automatically.
    /// </remarks>
    public bool KeyIsDefault => string.IsNullOrEmpty(Key) || Key == OperationQueue.DefaultKey;

    /// <summary>
    /// Gets the random order value used for tie-breaking when <see cref="UseRandomTiebreak"/> is enabled.
    /// </summary>
    /// <value>
    /// A random integer used for shuffling equal-priority items across different keys.
    /// </value>
    internal int RandomOrder { get; init; }

    /// <summary>
    /// Gets a value indicating whether random tie-breaking is enabled for this operation.
    /// </summary>
    /// <value>
    /// <c>true</c> if random tie-breaking should be used for equal priorities across different keys; otherwise, <c>false</c>.
    /// </value>
    internal bool UseRandomTiebreak { get; init; }

    /// <summary>
    /// Evaluates the operation function and returns an observable stream.
    /// </summary>
    /// <returns>An observable of <see cref="Unit"/> that completes when the operation finishes.</returns>
    public abstract IObservable<Unit> EvaluateFunc();

    /// <summary>
    /// Compares this operation to another for priority-based scheduling.
    /// Implements priority-based scheduling with key-based serialization and FIFO tie-breaking.
    /// </summary>
    /// <param name="other">The other operation to compare against.</param>
    /// <returns>
    /// A negative value if this operation should be dequeued first,
    /// a positive value if <paramref name="other"/> should be dequeued first,
    /// or zero if they have equivalent priority.
    /// </returns>
    /// <remarks>
    /// <para><strong>Design Decision: Partial Ordering</strong></para>
    /// <para>
    /// This CompareTo implementation intentionally violates the strict IComparable contract
    /// by returning 0 for distinct operations with equal priority/key. This is a deliberate
    /// architectural choice to separate concerns:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>KeyedOperation: Business logic ordering (priority, key, randomization)</description></item>
    ///   <item><description>PriorityQueue.IndexedItem: Data structure concern (FIFO sequencing)</description></item>
    /// </list>
    /// <para>
    /// This separation provides cleaner architecture and allows PriorityQueue to handle
    /// sequencing independently. The alternative (including Id in CompareTo) would:
    /// </para>
    /// <list type="number">
    ///   <item><description>Create tight coupling between KeyedOperation and OperationQueue's Id assignment</description></item>
    ///   <item><description>Add redundant comparisons (Id always agrees with IndexedItem sequence)</description></item>
    ///   <item><description>Reduce flexibility for alternative queue implementations</description></item>
    /// </list>
    /// <para>
    /// <strong>Safety:</strong> KeyedOperation is internal to OperationQueue and never used
    /// in containers (SortedSet, Array.Sort) that require strict CompareTo contracts.
    /// Do not use KeyedOperation in sorting containers outside of its intended context.
    /// </para>
    /// <para>
    /// <strong>Historical Note:</strong> Previously, KeyedOperation.Id (global sequence from OperationQueue)
    /// was used for tie-breaking. Now, tie-breaking uses IndexedItem.Id (per-queue sequence from PriorityQueue),
    /// which provides the same FIFO ordering within a single queue but isolates queues from each other's sequencing.
    /// </para>
    /// </remarks>
#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public int CompareTo(KeyedOperation? other)
    {
        if (other is null)
        {
            return 1;
        }

        // Fast path: check if both are default or both are non-default
        var thisIsDefault = KeyIsDefault;
        var otherIsDefault = other.KeyIsDefault;

        // Non-keyed operations always come before keyed operations
        // This ensures serialized (keyed) operations don't block concurrent slots
        if (thisIsDefault != otherIsDefault)
        {
            return thisIsDefault ? -1 : 1;
        }

        // Compare by priority (higher priority sorts first, so reverse the comparison)
        var priorityComparison = other.Priority.CompareTo(Priority);
        if (priorityComparison != 0)
        {
            return priorityComparison;
        }

        // Equal priority - optionally randomize across different keys
        if (UseRandomTiebreak || other.UseRandomTiebreak)
        {
            var randomComparison = RandomOrder.CompareTo(other.RandomOrder);
            if (randomComparison != 0)
            {
                return randomComparison;
            }
        }

        // Equal priority - defer final tie-breaking to PriorityQueue's IndexedItem FIFO sequencing.
        // See CompareTo documentation for design rationale.
        // GroupBy + Concat in OperationQueue ensures same-key operations run sequentially regardless of heap order.
        return 0;
    }
}
