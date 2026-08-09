// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

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
    private const int MixedPriorityOperationCount = 100;

    private const int PriorityLevelCount = 10;

    private const int SerializedOperationCount = 50;

    private const int ParallelOperationCount = 100;

    private const int ObservableOperationCount = 10;

    private const int PausedOperationCount = 20;

    private const int RandomizedOperationCount = 50;

    private OperationQueue? _queue;

    /// <summary>Setup method called before each benchmark.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _queue = new(maximumConcurrent: 4);
    }

    /// <summary>Cleanup method called after each benchmark.</summary>
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
    public async Task MixedPrioritiesAsync()
    {
        var tasks = new Task<int>[MixedPriorityOperationCount];
        for (var i = 0; i < MixedPriorityOperationCount; i++)
        {
            var capturedI = i;
            var priority = capturedI % PriorityLevelCount;
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
    public async Task SerializedOperationsAsync()
    {
        var tasks = new Task<int>[SerializedOperationCount];
        for (var i = 0; i < SerializedOperationCount; i++)
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
    public async Task ParallelOperationsAsync()
    {
        var tasks = new Task<int>[ParallelOperationCount];
        for (var i = 0; i < ParallelOperationCount; i++)
        {
            var capturedI = i;
            tasks[i] = _queue!.Enqueue(
                priority: 1,
                key: $"key{capturedI}",
                asyncOperation: () => Task.FromResult(capturedI));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>Benchmarks 10 observable-based enqueue operations through the raw observable API.</summary>
    /// <returns>Task for async operation.</returns>
    [Benchmark(Description = "10 observable operations")]
    public async Task ObservableOperationsAsync()
    {
        var tasks = new Task<int>[ObservableOperationCount];
        for (var i = 0; i < ObservableOperationCount; i++)
        {
            var capturedI = i;
            var obs = _queue!.EnqueueObservableOperation(
                priority: 1,
                asyncCalculationFunc: () => Signal.Emit(capturedI));
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
    public async Task PauseResumeOperationsAsync()
    {
        using var pause = _queue!.PauseQueue();

        var tasks = new Task<int>[PausedOperationCount];
        for (var i = 0; i < PausedOperationCount; i++)
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

    /// <summary>Benchmarks random priority tie-breaking overhead with a deterministic seed.</summary>
    /// <returns>Task for async operation.</returns>
    [Benchmark(Description = "50 operations with random tie-breaking")]
    public async Task RandomPriorityTieBreakingAsync()
    {
        using var randomQueue = new OperationQueue(
            maximumConcurrent: 4,
            randomizeEqualPriority: true,
            seed: 42);

        var tasks = new Task<int>[RandomizedOperationCount];
        for (var i = 0; i < RandomizedOperationCount; i++)
        {
            var capturedI = i;
            tasks[i] = randomQueue.Enqueue(
                priority: 1, // Same priority for all - triggers randomization
                asyncOperation: () => Task.FromResult(capturedI));
        }

        await Task.WhenAll(tasks);
    }
}
