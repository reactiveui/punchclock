// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ReactiveUI.Primitives.Internal;

/// <summary>
/// Polyfill for <c>ArgumentNullException.ThrowIfNull</c> on target frameworks (net462-net481) that predate it.
/// On net8.0 and later this type is not compiled; consuming projects alias the <c>ArgumentExceptionHelper</c>
/// identifier directly to <see cref="ArgumentNullException"/> so the call sites bind to the BCL method.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class ArgumentExceptionHelper
{
    /// <summary>Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is <see langword="null"/>.</summary>
    /// <param name="argument">The reference type argument to validate as non-null.</param>
    /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
    public static void ThrowIfNull(
        [NotNull] object? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is not null)
        {
            return;
        }

        throw new ArgumentNullException(paramName);
    }
}
