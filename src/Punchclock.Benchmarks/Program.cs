// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Running;

namespace Punchclock.Benchmarks;

/// <summary>
/// Entry point for BenchmarkDotNet runner.
/// </summary>
internal class Program
{
    private static void Main(string[] args)
    {
        // Run all benchmarks in this assembly
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
