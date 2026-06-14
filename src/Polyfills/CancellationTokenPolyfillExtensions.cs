// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NETCOREAPP3_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER
namespace System.Threading;

/// <summary>Polyfill extensions for <see cref="CancellationToken"/> on frameworks without <c>UnsafeRegister</c>.</summary>
internal static class CancellationTokenPolyfillExtensions
{
    /// <summary>Registers a delegate that is invoked when the token is cancelled, without capturing the execution context.</summary>
    /// <param name="token">The cancellation token.</param>
    /// <param name="callback">The delegate to invoke on cancellation.</param>
    /// <param name="state">The state passed to <paramref name="callback"/>.</param>
    /// <returns>A registration that can be disposed to remove the callback.</returns>
    public static CancellationTokenRegistration UnsafeRegister(this CancellationToken token, Action<object?> callback, object? state) =>
        token.Register(callback, state, useSynchronizationContext: false);

    /// <summary>Registers a delegate that is invoked with the triggering token when the token is cancelled, without capturing the execution context.</summary>
    /// <param name="token">The cancellation token.</param>
    /// <param name="callback">The delegate to invoke on cancellation, receiving the state and the triggering token.</param>
    /// <param name="state">The state passed to <paramref name="callback"/>.</param>
    /// <returns>A registration that can be disposed to remove the callback.</returns>
    public static CancellationTokenRegistration UnsafeRegister(this CancellationToken token, Action<object?, CancellationToken> callback, object? state) =>
        token.Register(
            static boxed =>
            {
                var (inner, innerState, innerToken) = ((Action<object?, CancellationToken> Callback, object? State, CancellationToken Token))boxed!;
                inner(innerState, innerToken);
            },
            (callback, state, token),
            useSynchronizationContext: false);
}
#endif
