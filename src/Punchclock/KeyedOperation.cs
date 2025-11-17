// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Punchclock;

/// <summary>
/// KeyedOperation.
/// </summary>
/// <seealso cref="System.IComparable&lt;Punchclock.KeyedOperation&gt;" />
internal abstract class KeyedOperation : IComparable<KeyedOperation>
{
    /// <summary>
    /// Gets or sets a value indicating whether [cancelled early].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [cancelled early]; otherwise, <c>false</c>.
    /// </value>
    public bool CancelledEarly { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    /// <value>
    /// The priority.
    /// </value>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    /// <value>
    /// The identifier.
    /// </value>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    /// <value>
    /// The key.
    /// </value>
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the cancel signal.
    /// </summary>
    /// <value>
    /// The cancel signal.
    /// </value>
    public IObservable<Unit>? CancelSignal { get; set; }

    /// <summary>
    /// Gets a value indicating whether [key is default].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [key is default]; otherwise, <c>false</c>.
    /// </value>
    public bool KeyIsDefault => string.IsNullOrEmpty(Key) || Key == OperationQueue.DefaultKey;

    /// <summary>
    /// Gets or sets the random order.
    /// </summary>
    /// <value>
    /// The random order.
    /// </value>
    internal int RandomOrder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether [use random tiebreak].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [use random tiebreak]; otherwise, <c>false</c>.
    /// </value>
    internal bool UseRandomTiebreak { get; set; }

    /// <summary>
    /// Evaluates the function.
    /// </summary>
    /// <returns>An Observable of Unit.</returns>
    public abstract IObservable<Unit> EvaluateFunc();

    /// <summary>
    /// Compares to.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>The compare result.</returns>
    public int CompareTo(KeyedOperation other)
    {
        // NB: Non-keyed operations always come before keyed operations in
        // order to make sure that serialized keyed operations don't take
        // up concurrency slots
        if (KeyIsDefault != other.KeyIsDefault)
        {
            // If this is non-keyed (default), it should sort before keyed -> return -1
            return KeyIsDefault ? -1 : 1;
        }

        // Higher priority should sort before lower priority
        var c = other.Priority.CompareTo(Priority);
        if (c != 0)
        {
            return c;
        }

        // Same priority
        // Preserve FIFO within the same key group (non-default keys)
        if (!KeyIsDefault && string.Equals(Key, other.Key, StringComparison.Ordinal))
        {
            return Id.CompareTo(other.Id);
        }

        // For equal priority across different keys or unkeyed items, optionally randomize tie-break
        if (UseRandomTiebreak || other.UseRandomTiebreak)
        {
            c = RandomOrder.CompareTo(other.RandomOrder);
            if (c != 0)
            {
                return c;
            }
        }

        // Final stable tie-breaker: insertion order
        return Id.CompareTo(other.Id);
    }
}

[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Generic implementation of same class name.")]
internal class KeyedOperation<T> : KeyedOperation
{
    /// <summary>
    /// Gets or sets the function.
    /// </summary>
    /// <value>
    /// The function.
    /// </value>
    public Func<IObservable<T>>? Func { get; set; }

    /// <summary>
    /// Gets the result.
    /// </summary>
    /// <value>
    /// The result.
    /// </value>
    public ReplaySubject<T> Result { get; } = new ReplaySubject<T>();

    /// <summary>
    /// Evaluates the function.
    /// </summary>
    /// <returns>
    /// An Observable of Unit.
    /// </returns>
    public override IObservable<Unit> EvaluateFunc()
    {
        if (Func == null)
        {
            return Observable.Empty<Unit>();
        }

        if (CancelledEarly)
        {
            return Observable.Empty<Unit>();
        }

        var signal = CancelSignal ?? Observable.Empty<Unit>();
        var ret = Func().TakeUntil(signal).Multicast(Result);
        ret.Connect();

        return ret.Select(_ => Unit.Default);
    }
}
