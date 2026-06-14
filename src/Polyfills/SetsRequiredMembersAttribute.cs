// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Polyfill implementation adapted from SimonCropp/Polyfill (https://github.com/SimonCropp/Polyfill).
#if !NET7_0_OR_GREATER
using System.Diagnostics;

namespace System.Diagnostics.CodeAnalysis;

/// <summary>Specifies that a constructor sets all required members, satisfying the compiler's required-member checks.</summary>
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
internal sealed class SetsRequiredMembersAttribute : Attribute;
#endif
