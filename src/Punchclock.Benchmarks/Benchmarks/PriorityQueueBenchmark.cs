// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Primitives.Core;

namespace Punchclock.Benchmarks;

/// <summary>
/// Benchmarks for PriorityQueue performance across different workload sizes.
/// Tests small (16), medium (100), and large (1000) workloads to measure
/// quaternary heap (4-ary) performance characteristics.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net462, warmupCount: 3, iterationCount: 10)]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 3, iterationCount: 10)]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 10)]
[SimpleJob(RuntimeMoniker.NativeAot10_0, id: nameof(RuntimeMoniker.NativeAot10_0), warmupCount: 3, iterationCount: 10)]
[MarkdownExporterAttribute.GitHub]
public class PriorityQueueBenchmark
{
    private const int SmallItemCount = 16;

    private const int MediumItemCount = 100;

    private const int LargeItemCount = 1000;

    private const int PriorityMultiplier = 31;

    private const int PriorityOffset = 42;

    private const int PriorityRange = 100;

    private const int SmallDequeueInterval = 4;

    private const int MediumDequeueInterval = 10;

    private const int LargeDequeueInterval = 100;

    private TestItem[] _smallData = null!;

    private TestItem[] _mediumData = null!;

    private TestItem[] _largeData = null!;

    /// <summary>Setup method to generate test data before benchmarks run.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _smallData = CreateData(SmallItemCount);
        _mediumData = CreateData(MediumItemCount);
        _largeData = CreateData(LargeItemCount);
    }

    // === Small Queue (typical Punchclock workload: 4-16 items) ===
    /// <summary>Enqueue 16 items.</summary>
    [Benchmark(Description = "Enqueue 16 items")]
    public void Enqueue_Small()
    {
        var queue = new PriorityQueue<TestItem>();
        foreach (var item in _smallData)
        {
            queue.Enqueue(item);
        }
    }

    /// <summary>Mixed operations (enqueue + dequeue) with 16 items.</summary>
    [Benchmark(Description = "Mixed ops 16 items")]
    public void Mixed_Small()
    {
        var queue = new PriorityQueue<TestItem>();
        for (var i = 0; i < SmallItemCount; i++)
        {
            queue.Enqueue(_smallData[i]);
            if (i % SmallDequeueInterval == 0 && queue.Count > 0)
            {
                queue.Dequeue();
            }
        }
    }

    // === Medium Queue (100 items) ===
    /// <summary>Enqueue 100 items.</summary>
    [Benchmark(Description = "Enqueue 100 items")]
    public void Enqueue_Medium()
    {
        var queue = new PriorityQueue<TestItem>();
        foreach (var item in _mediumData)
        {
            queue.Enqueue(item);
        }
    }

    /// <summary>Mixed operations with 100 items.</summary>
    [Benchmark(Description = "Mixed ops 100 items")]
    public void Mixed_Medium()
    {
        var queue = new PriorityQueue<TestItem>();
        for (var i = 0; i < MediumItemCount; i++)
        {
            queue.Enqueue(_mediumData[i]);
            if (i % MediumDequeueInterval == 0 && queue.Count > 0)
            {
                queue.Dequeue();
            }
        }
    }

    // === Large Queue (1000 items) ===
    /// <summary>Enqueue 1000 items.</summary>
    [Benchmark(Description = "Enqueue 1000 items")]
    public void Enqueue_Large()
    {
        var queue = new PriorityQueue<TestItem>();
        foreach (var item in _largeData)
        {
            queue.Enqueue(item);
        }
    }

    /// <summary>Mixed operations with 1000 items.</summary>
    [Benchmark(Description = "Mixed ops 1000 items")]
    public void Mixed_Large()
    {
        var queue = new PriorityQueue<TestItem>();
        for (var i = 0; i < LargeItemCount; i++)
        {
            queue.Enqueue(_largeData[i]);
            if (i % LargeDequeueInterval == 0 && queue.Count > 0)
            {
                queue.Dequeue();
            }
        }
    }

    private static TestItem[] CreateData(int count) =>
        Enumerable.Range(0, count)
            .Select(static index => new TestItem(((index * PriorityMultiplier) + PriorityOffset) % PriorityRange))
            .ToArray();

    /// <summary>Test item for benchmarking.</summary>
    /// <param name="Priority">The priority value.</param>
    private sealed record TestItem(int Priority) : IComparable<TestItem>
    {
        public int CompareTo(TestItem? other) => Priority.CompareTo(other?.Priority ?? int.MaxValue);
    }
}
