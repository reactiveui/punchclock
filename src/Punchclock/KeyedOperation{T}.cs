// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace Punchclock;

/// <summary>
/// Typed operation that can be enqueued in an <see cref="OperationQueue"/>.
/// Wraps a user-provided function that returns an observable of <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of value produced by this operation.</typeparam>
internal sealed class KeyedOperation<T> : KeyedOperation
{
    /// <summary>
    /// Gets the function that produces the observable result for this operation.
    /// </summary>
    /// <value>
    /// A function that returns an <see cref="IObservable{T}"/> when invoked, or null if not set.
    /// </value>
    public Func<IObservable<T>>? Func { get; init; }

    /// <summary>
    /// Gets the replay subject that multicasts the operation result to all subscribers.
    /// Results are cached so late subscribers receive the same values.
    /// </summary>
    /// <value>
    /// A <see cref="ReplaySignal{T}"/> that replays all emitted values to new subscribers.
    /// </value>
    public ReplaySignal<T> Result { get; } = new();

    /// <summary>
    /// Evaluates the operation function and multicasts the result through <see cref="Result"/>.
    /// Respects early cancellation and the cancellation signal.
    /// </summary>
    /// <returns>
    /// An observable of <see cref="RxVoid"/> that completes when the operation finishes.
    /// Returns an empty observable if the function is null or the operation was cancelled early.
    /// </returns>
    public override IObservable<RxVoid> EvaluateFunc()
    {
        if (Func is null)
        {
            return Signal.None<RxVoid>();
        }

        if (CancelledEarly)
        {
            return Signal.None<RxVoid>();
        }

        var signal = CancelSignal ?? Signal.None<RxVoid>();
        var ret = Func().TakeUntil(signal).Multicast(Result);
        ret.Connect();

        return ret.Map(_ => RxVoid.Default);
    }
}
