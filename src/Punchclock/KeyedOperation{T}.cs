// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace Punchclock.Reactive;
#else

namespace Punchclock;
#endif

/// <summary>Typed operation that can be enqueued in an <see cref="OperationQueue"/>. Wraps a user-provided function that returns an observable of <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The type of value produced by this operation.</typeparam>
internal sealed class KeyedOperation<T> : KeyedOperation
{
    /// <summary>Gets the function that produces the observable result for this operation.</summary>
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
    /// An observable of <see cref="Unit"/> that completes when the operation finishes.
    /// Returns an empty observable if the function is null or the operation was cancelled early.
    /// </returns>
    public override IObservable<Unit> EvaluateFunc()
    {
        if (Func is null)
        {
            return Signal.None<Unit>();
        }

        if (CancelledEarly)
        {
            return Signal.None<Unit>();
        }

        var signal = CancelSignal ?? Signal.None<Unit>();
        var ret = Func().TakeUntil(signal).Multicast(Result);
        ret.Connect();

        return ret.Map(_ => Unit.Default);
    }
}
