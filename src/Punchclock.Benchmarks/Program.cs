// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Running;

namespace Punchclock.Benchmarks;

/// <summary>Entry point for BenchmarkDotNet runner.</summary>
public static class Program
{
    /// <summary>Runs the benchmarks selected by the supplied command-line arguments.</summary>
    /// <param name="args">The command-line arguments supplied to BenchmarkDotNet.</param>
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
