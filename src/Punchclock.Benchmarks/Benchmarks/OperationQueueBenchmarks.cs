// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Punchclock.Benchmarks;

/// <summary>
/// Benchmarks for core OperationQueue scenarios.
/// Tests priority ordering, key-based serialization, and concurrent execution.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net462, warmupCount: 3, iterationCount: 10)]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 3, iterationCount: 10)]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 10)]
[SimpleJob(RuntimeMoniker.NativeAot10_0, id: nameof(RuntimeMoniker.NativeAot10_0), warmupCount: 3, iterationCount: 10)]
[MarkdownExporterAttribute.GitHub]
public class OperationQueueBenchmarks
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
    /// Benchmark: Enqueue and execute 100 operations with varying priorities.
    /// Tests priority queue ordering performance.
    /// </summary>
    /// <returns>Task for async operation.</returns>
    [Benchmark(Description = "100 operations with mixed priorities")]
    public async Task MixedPriorities()
    {
        var tasks = new Task<int>[100];
        for (var i = 0; i < 100; i++)
        {
            var capturedI = i;
            var priority = capturedI % 10; // Priorities 0-9
            tasks[i] = _queue!.Enqueue(
                priority: priority,
                asyncOperation: () => Task.FromResult(capturedI));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Benchmark: Enqueue 50 operations with the same key (serialized execution).
    /// Tests key-based serialization performance.
    /// </summary>
    /// <returns>Task for async operation.</returns>
    [Benchmark(Description = "50 serialized operations (same key)")]
    public async Task SerializedOperations()
    {
        var tasks = new Task<int>[50];
        for (var i = 0; i < 50; i++)
        {
            var capturedI = i;
            tasks[i] = _queue!.Enqueue(
                priority: 1,
                key: "serial",
                asyncOperation: () => Task.FromResult(capturedI));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Benchmark: Enqueue 100 operations with unique keys (parallel execution).
    /// Tests concurrent execution performance.
    /// </summary>
    /// <returns>Task for async operation.</returns>
    [Benchmark(Baseline = true, Description = "100 parallel operations (unique keys)")]
    public async Task ParallelOperations()
    {
        var tasks = new Task<int>[100];
        for (var i = 0; i < 100; i++)
        {
            var capturedI = i;
            tasks[i] = _queue!.Enqueue(
                priority: 1,
                key: $"key{capturedI}",
                asyncOperation: () => Task.FromResult(capturedI));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Benchmark: Observable-based enqueue (10 operations).
    /// Tests the raw observable API performance.
    /// </summary>
    /// <returns>Task for async operation.</returns>
    [Benchmark(Description = "10 observable operations")]
    public async Task ObservableOperations()
    {
        var tasks = new Task<int>[10];
        for (var i = 0; i < 10; i++)
        {
            var capturedI = i;
            var obs = _queue!.EnqueueObservableOperation(
                priority: 1,
                asyncCalculationFunc: () => Observable.Return(capturedI));
            tasks[i] = obs.ToTask();
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Benchmark: Pause and resume queue with pending operations.
    /// Tests pause/resume overhead and ref-counting.
    /// </summary>
    /// <returns>Task for async operation.</returns>
    [Benchmark(Description = "Pause/resume with 20 operations")]
    public async Task PauseResumeOperations()
    {
        using var pause = _queue!.PauseQueue();

        var tasks = new Task<int>[20];
        for (var i = 0; i < 20; i++)
        {
            var capturedI = i;
            tasks[i] = _queue.Enqueue(
                priority: 1,
                asyncOperation: () => Task.FromResult(capturedI));
        }

        // Resume by disposing pause
        pause.Dispose();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Benchmark: Random priority with tie-breaking (deterministic seed).
    /// Tests randomization overhead.
    /// </summary>
    /// <returns>Task for async operation.</returns>
    [Benchmark(Description = "50 operations with random tie-breaking")]
    public async Task RandomPriorityTieBreaking()
    {
        using var randomQueue = new OperationQueue(
            maximumConcurrent: 4,
            randomizeEqualPriority: true,
            seed: 42);

        var tasks = new Task<int>[50];
        for (var i = 0; i < 50; i++)
        {
            var capturedI = i;
            tasks[i] = randomQueue.Enqueue(
                priority: 1, // Same priority for all - triggers randomization
                asyncOperation: () => Task.FromResult(capturedI));
        }

        await Task.WhenAll(tasks);
    }
}
