// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Punchclock.Benchmarks;

/// <summary>
/// Benchmarks for CancellationToken fast path optimizations.
/// Compares old vs new behavior when enqueueing with various token types.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net462, warmupCount: 3, iterationCount: 10)]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 3, iterationCount: 10)]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 10)]
[SimpleJob(RuntimeMoniker.NativeAot10_0, id: nameof(RuntimeMoniker.NativeAot10_0), warmupCount: 3, iterationCount: 10)]
[MarkdownExporterAttribute.GitHub]
public class CancellationTokenBenchmarks
{
    private OperationQueue? _queue;

    /// <summary>
    /// Setup method called before each benchmark.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _queue = new OperationQueue(maximumConcurrent: 4);
    }

    /// <summary>
    /// Cleanup method called after each benchmark.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _queue?.Dispose();
    }

    /// <summary>
    /// Benchmark: Enqueue with CancellationToken.None (fast path).
    /// New version should have zero allocations, old version allocates Observable.Create + delegate.
    /// </summary>
    /// <returns>Task for async operation.</returns>
    [Benchmark(Description = "Enqueue with CancellationToken.None")]
    public async Task EnqueueWithTokenNone()
    {
        var result = await _queue!.Enqueue(
            priority: 1,
            key: "bench",
            asyncOperation: () => Task.FromResult(42),
            token: CancellationToken.None);
    }

    /// <summary>
    /// Benchmark: Enqueue with a cancellable token (slow path - unavoidable).
    /// Both versions should perform similarly.
    /// </summary>
    /// <returns>Task for async operation.</returns>
    [Benchmark(Description = "Enqueue with cancellable token")]
    public async Task EnqueueWithCancellableToken()
    {
        using var cts = new CancellationTokenSource();
        var result = await _queue!.Enqueue(
            priority: 1,
            key: "bench",
            asyncOperation: () => Task.FromResult(42),
            token: cts.Token);
    }

    /// <summary>
    /// Benchmark: Enqueue without any token (no cancellation support).
    /// Baseline for comparison.
    /// </summary>
    /// <returns>Task for async operation.</returns>
    [Benchmark(Baseline = true, Description = "Enqueue without token (baseline)")]
    public async Task EnqueueWithoutToken()
    {
        var result = await _queue!.Enqueue(
            priority: 1,
            key: "bench",
            asyncOperation: () => Task.FromResult(42));
    }

    /// <summary>
    /// Benchmark: Batch enqueue 100 operations with CancellationToken.None.
    /// Tests fast path scaling.
    /// </summary>
    /// <returns>Task for async operation.</returns>
    [Benchmark(Description = "Batch 100 operations with CancellationToken.None")]
    public async Task BatchEnqueueWithTokenNone()
    {
        var tasks = new Task<int>[100];
        for (var i = 0; i < 100; i++)
        {
            var capturedI = i;
            tasks[i] = _queue!.Enqueue(
                priority: 1,
                key: $"bench{capturedI}",
                asyncOperation: () => Task.FromResult(capturedI),
                token: CancellationToken.None);
        }

        await Task.WhenAll(tasks);
    }
}
