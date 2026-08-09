// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NET5_0_OR_GREATER
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

/// <summary>Reserved for the compiler to track metadata for <see langword="init"/>-only members; not for use in source.</summary>
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
internal static class IsExternalInit
{
    /// <summary>Gets a value indicating that this is the compiler-recognized init-only marker type.</summary>
    internal static bool IsCompilerMarker => true;
}
#endif
