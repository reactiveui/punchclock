// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Punchclock.Tests;

/// <summary>Adapts the shared tests to the System.Reactive subscription extensions.</summary>
internal static class ObservableExtensions
{
    /// <summary>Subscribes an observer callback to an observable sequence.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The observable sequence.</param>
    /// <param name="onNext">The value callback.</param>
    /// <returns>The subscription.</returns>
    internal static IDisposable Subscribe<T>(IObservable<T> source, Action<T> onNext) => source.Subscribe(onNext);

    /// <summary>Subscribes value and error callbacks to an observable sequence.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The observable sequence.</param>
    /// <param name="onNext">The value callback.</param>
    /// <param name="onError">The error callback.</param>
    /// <returns>The subscription.</returns>
    internal static IDisposable Subscribe<T>(IObservable<T> source, Action<T> onNext, Action<Exception> onError) =>
        source.Subscribe(onNext, onError);

    /// <summary>Subscribes value, error, and completion callbacks to an observable sequence.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The observable sequence.</param>
    /// <param name="onNext">The value callback.</param>
    /// <param name="onError">The error callback.</param>
    /// <param name="onCompleted">The completion callback.</param>
    /// <returns>The subscription.</returns>
    internal static IDisposable Subscribe<T>(
        IObservable<T> source,
        Action<T> onNext,
        Action<Exception> onError,
        Action onCompleted) =>
        source.Subscribe(onNext, onError, onCompleted);
}
