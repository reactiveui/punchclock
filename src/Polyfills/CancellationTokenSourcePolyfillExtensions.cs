// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NET8_0_OR_GREATER
namespace System.Threading;

/// <summary>Polyfill extensions for <see cref="CancellationTokenSource"/> on frameworks without the .NET 8 async cancellation API.</summary>
internal static class CancellationTokenSourcePolyfillExtensions
{
    /// <summary>Communicates a request for cancellation, completing synchronously.</summary>
    /// <param name="source">The cancellation token source.</param>
    /// <returns>A completed task representing the cancellation request.</returns>
    public static Task CancelAsync(this CancellationTokenSource source)
    {
        source.Cancel();
        return Task.CompletedTask;
    }
}
#endif
