# GitHub Copilot Instructions for Punchclock

This file provides guidance to GitHub Copilot when working with code in the Punchclock repository.

## Project Overview

Punchclock is a library for managing concurrent operations with priorities and key-based serialization. It provides a scheduler for deferred actions (like network requests) with bounded concurrency, priority scheduling, and operation serialization by key.

**Key Features:**
- Bounded concurrency with priority-aware semaphore
- Priority scheduling (higher numbers run first)
- Key-based serialization (only one operation per key at a time)
- Pause/resume with reference counting
- Cancellation via CancellationToken or IObservable
- Task and IObservable friendly API

## Development Environment

### Required Tools
- .NET 8.0, 9.0, and 10.0 SDKs
- Optional: .NET Framework 4.6.2, 4.7.2, 4.8.1 SDKs (Windows only for legacy targets)
- Visual Studio 2022 17.10+ or JetBrains Rider 2024.1+

### Project Structure
```
src/
├── Punchclock/                    # Main library
│   ├── OperationQueue.cs          # Core scheduler
│   ├── OperationQueueExtensions.cs # Task-based extensions
│   ├── KeyedOperation.cs          # Operation representation
│   ├── PrioritySemaphoreSubject.cs # Priority semaphore
│   ├── ScheduledSubject.cs        # Observable wrapper
│   └── PriorityQueue.cs           # Priority queue
├── Punchclock.Tests/              # Unit tests
└── Punchclock.sln                 # Solution file
```

### Build and Test

**Working Directory:** All commands must be run from `./src` folder

**CRITICAL:** This repository uses Microsoft Testing Platform (MTP). Use `--project` or `--solution` flags.

```bash
# Restore packages
dotnet restore Punchclock.sln

# Build
dotnet build Punchclock.sln -c Release

# Run all tests (using --solution flag)
dotnet test --solution Punchclock.sln

# Run all tests in test project (using --project flag)
dotnet test --project Punchclock.Tests/Punchclock.Tests.csproj

# Run tests with coverage
dotnet test --solution Punchclock.sln --coverage --coverage-output-format cobertura

# Run specific test
dotnet test --project Punchclock.Tests/Punchclock.Tests.csproj -- --treenode-filter "/*/*/*/TestName"
```

## Code Style Guidelines

### C# Style Rules

**File Organization:**
- File-scoped namespaces preferred
- Using directives outside namespace, sorted (System first, then others)
- One type per file (nested types inside parent allowed)

**Naming Conventions:**
- Private/internal fields: `_camelCase`
- Private/internal static fields: `s_camelCase`
- Constants: `PascalCase`
- Methods, properties, classes: `PascalCase`
- Parameters, local variables: `camelCase`

**Code Formatting:**
- **Braces:** Allman style (each brace on new line)
- **Indentation:** 4 spaces, no tabs
- **Visibility:** Always explicit (e.g., `private string _foo`)
- **Modifier order:** public, private, protected, internal, static, readonly, async
- **Fields:** `readonly` where possible, `static readonly` (not `readonly static`)

**Modern C# Features:**
- Use nullable reference types
- Use `var` when it improves readability
- Use pattern matching where appropriate
- Use target-typed `new()`
- Use collection expressions `[...]`
- Avoid `this.` unless necessary
- Use `nameof()` instead of string literals
- Use `ArgumentNullException.ThrowIfNull()` for parameter validation

**Example:**
```csharp
// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Linq;

namespace Punchclock;

/// <summary>
/// Summary of what this class does.
/// </summary>
public class ExampleClass
{
    private readonly string _name;
    private readonly int _priority;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExampleClass"/> class.
    /// </summary>
    /// <param name="name">The name parameter.</param>
    /// <param name="priority">The priority value.</param>
    public ExampleClass(string name, int priority)
    {
        ArgumentNullException.ThrowIfNull(name);

        _name = name;
        _priority = priority;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name => _name;
}
```

### Documentation Requirements

**All public APIs must have XML documentation:**
- Summary tag for all public/protected types and members
- Param tags for all parameters
- Returns tag for methods with return values
- Exception tags for documented exceptions
- Remarks tag for additional important information

**Example:**
```csharp
/// <summary>
/// Enqueues an operation with the specified priority and key.
/// </summary>
/// <typeparam name="T">The type of the operation result.</typeparam>
/// <param name="priority">The priority (higher numbers run first).</param>
/// <param name="key">The key for serialization (optional).</param>
/// <param name="operation">The operation to execute.</param>
/// <param name="cancellationToken">Cancellation token (optional).</param>
/// <returns>A task that completes when the operation finishes.</returns>
/// <exception cref="ArgumentNullException">Thrown when operation is null.</exception>
/// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
public Task<T> Enqueue<T>(
    int priority,
    string? key,
    Func<Task<T>> operation,
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

## Testing Guidelines

### Test Framework: TUnit with Microsoft Testing Platform

**Test Structure:**
```csharp
namespace Punchclock.Tests;

public class OperationQueueTests
{
    [Test]
    public async Task Enqueue_WithPriority_ExecutesInOrder()
    {
        // Arrange
        var queue = new OperationQueue(1);
        var results = new List<int>();

        // Act
        var t1 = queue.Enqueue(1, () => Task.Run(() => results.Add(1)));
        var t2 = queue.Enqueue(5, () => Task.Run(() => results.Add(2))); // Higher priority
        await Task.WhenAll(t1, t2);

        // Assert
        await Assert.That(results).IsEquivalentTo([1, 2]);
    }
}
```

**Test Best Practices:**
- Use descriptive test names: `MethodName_Scenario_ExpectedOutcome`
- Arrange-Act-Assert pattern
- One assertion concept per test
- Use `async Task` for async tests
- Clean up resources with `using` or explicit disposal
- Tests run non-parallel (`"parallel": false` in testconfig.json)

**Running Tests:**
```bash
# All tests
dotnet test --solution Punchclock.sln

# Specific test
dotnet test --project Punchclock.Tests/Punchclock.Tests.csproj -- --treenode-filter "/*/*/*/TestName"

# With detailed output
dotnet test --solution Punchclock.sln -- --output Detailed

# With coverage
dotnet test --solution Punchclock.sln --coverage --coverage-output-format cobertura
```

## AOT Compatibility (net8.0+)

All code targeting net8.0+ must be AOT-compatible (`IsAotCompatible=true`).

**Key Guidelines:**
- Avoid reflection where possible
- Use `DynamicallyAccessedMembersAttribute` when reflection is required
- Prefer strongly-typed expressions over string-based property names
- Use `nameof()` for compile-time checking

**Example:**
```csharp
#if NET6_0_OR_GREATER
private static PropertyInfo? GetPropertyInfo(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    Type type,
    string propertyName)
#else
private static PropertyInfo? GetPropertyInfo(Type type, string propertyName)
#endif
{
    return type.GetProperty(propertyName);
}
```

## Warning Suppression Policy

**CRITICAL: Zero Pragma Policy**

- **NO `#pragma warning disable` statements allowed**
- **StyleCop warnings (SA****) must always be fixed, never suppressed**
- **Code analyzer warnings (CA****) may be suppressed only as a last resort**

**When suppression is acceptable (CA**** only):**
1. You attempted to fix the warning first
2. The fix would make the code worse or is not feasible
3. You use `[SuppressMessage]` attribute (not pragma)
4. You provide a clear, valid justification

**Example:**
```csharp
// WRONG - Never use pragma
#pragma warning disable CA1062
public void MyMethod(object parameter)
{
    parameter.ToString();
}
#pragma warning restore CA1062

// CORRECT - Fix the issue
public void MyMethod(object parameter)
{
    ArgumentNullException.ThrowIfNull(parameter);
    parameter.ToString();
}

// ACCEPTABLE - SuppressMessage with justification (last resort)
[SuppressMessage("Security", "CA5394:Do not use insecure randomness",
    Justification = "Random is used for priority queue tiebreaking, not security")]
public class OperationQueue
{
    private readonly Random? _random;
}
```

## Common Patterns

### Using OperationQueue

**Basic usage:**
```csharp
var queue = new OperationQueue(maximumConcurrent: 2);

// Simple operation
await queue.Enqueue(1, async () =>
{
    await DoSomethingAsync();
});

// With result
var result = await queue.Enqueue(1, async () =>
{
    return await FetchDataAsync();
});
```

**Priority scheduling:**
```csharp
// Higher priority runs first
var urgent = queue.Enqueue(10, () => FetchUrgentDataAsync());
var normal = queue.Enqueue(1, () => FetchNormalDataAsync());
```

**Key-based serialization:**
```csharp
// These run sequentially (same key)
var user1 = queue.Enqueue(1, key: "user:42", () => LoadUserAsync(42));
var user2 = queue.Enqueue(1, key: "user:42", () => UpdateUserAsync(42));

// These can run concurrently (different keys)
var userA = queue.Enqueue(1, key: "user:1", () => LoadUserAsync(1));
var userB = queue.Enqueue(1, key: "user:2", () => LoadUserAsync(2));
```

**Cancellation:**
```csharp
// Using CancellationToken
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await queue.Enqueue(1, () => LongOperationAsync(), cts.Token);

// Using IObservable
var cancel = new Subject<Unit>();
var obs = queue.EnqueueObservableOperation(
    1,
    "operation-key",
    cancel,
    () => LongOperation().ToObservable());
cancel.OnNext(Unit.Default); // Cancel
```

**Pause and resume:**
```csharp
// Pause the queue
var gate = queue.PauseQueue();

// Queue operations (won't execute while paused)
var task = queue.Enqueue(1, () => DoWorkAsync());

// Resume
gate.Dispose();
```

## What to Avoid

- **Reflection** without proper AOT suppression
- **Breaking changes** to public APIs without versioning
- **Blocking operations** in async code (use `await` not `.Result` or `.Wait()`)
- **Excessive allocations** in hot paths
- **Thread-unsafe code** without synchronization
- **Long-running operations** that block queue slots
- **Ignoring cancellation tokens**

## Common Mistakes to Avoid

1. **Using `.Result` or `.Wait()`** - Always use `await`
2. **Not disposing resources** - Use `using` statements or explicit disposal
3. **Ignoring priorities** - Use meaningful priority values
4. **Forgetting to resume** - Always dispose pause gates
5. **Not handling cancellation** - Check cancellation tokens
6. **Blocking the thread pool** - Keep operations async
7. **Mutating shared state** - Ensure thread safety

## Architecture Considerations

**Punchclock is designed for:**
- Managing concurrent network requests
- Throttling API calls
- Prioritizing user-initiated actions over background work
- Serializing access to shared resources

**Key design principles:**
- Small, focused API surface
- Composable with Rx.NET and Task-based async
- Proper synchronization and disposal
- Efficient scheduling with minimal allocations
- Thread-safe by design

## Target Frameworks

- **Modern:** net8.0, net9.0, net10.0 (cross-platform, AOT-compatible)
- **Legacy:** net462, net472, net481 (Windows only)

When writing code:
- Target modern .NET APIs where possible
- Use `#if NET6_0_OR_GREATER` for modern-only code
- Consider legacy framework limitations
- Ensure AOT compatibility for net8.0+

## Package Dependencies

- **System.Reactive** - Core dependency for Rx.NET
- **Test dependencies:** TUnit, Verify.TUnit, PublicApiGenerator, DynamicData

## Version Management

- Uses **Nerdbank.GitVersioning** for semantic versioning
- Version is derived from git tags and history
- No manual version files to update

## Additional Resources

- **TUnit Documentation:** https://tunit.dev/
- **Microsoft Testing Platform:** https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test
- **Rx.NET:** https://github.com/dotnet/reactive
- **Project README:** See `/README.md` for API overview and examples
- **CLAUDE.md:** See `/CLAUDE.md` for detailed build/test/style guide

## Quick Reference

```bash
# From ./src directory

# Build
dotnet build Punchclock.sln -c Release

# Test (using --solution flag for MTP)
dotnet test --solution Punchclock.sln

# Test with coverage
dotnet test --solution Punchclock.sln --coverage --coverage-output-format cobertura

# Run specific test (using --project flag for MTP)
dotnet test --project Punchclock.Tests/Punchclock.Tests.csproj -- --treenode-filter "/*/*/*/TestName"

# Clean
dotnet clean Punchclock.sln
```

## Summary

When working with Punchclock:
1. Follow Allman brace style and code formatting rules
2. Document all public APIs with XML comments
3. Write tests for all new features and bug fixes
4. Ensure AOT compatibility for net8.0+
5. Never use pragma for warning suppression
6. Keep the API surface small and focused
7. Prioritize thread safety and proper disposal
8. Use async/await (never block with `.Result` or `.Wait()`)
