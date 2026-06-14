// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NET8_0_OR_GREATER
namespace System.Threading;

/// <summary>Polyfill extensions for <see cref="CancellationTokenSource"/> on frameworks without the .NET 8 async cancellation API.</summary>
internal static class CancellationTokenSourcePolyfillExtensions
{
    /// <summary>Async cancellation members for cancellation token sources.</summary>
    /// <param name="source">The cancellation token source.</param>
    extension(CancellationTokenSource source)
    {
        /// <summary>Communicates a request for cancellation, completing synchronously.</summary>
        /// <returns>A completed task representing the cancellation request.</returns>
        public Task CancelAsync()
        {
            source.Cancel();
            return Task.CompletedTask;
        }
    }
}
#endif
