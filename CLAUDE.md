# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

This project uses **Microsoft Testing Platform (MTP)** with the **TUnit** testing framework. Test commands differ significantly from traditional VSTest.

See: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test?tabs=dotnet-test-with-mtp

### Solution Format

This repository uses the traditional **SLN** (solution) format.

**File Location:** `src/Punchclock.sln`

All build and test commands in this document reference `Punchclock.sln`.

### Prerequisites

```powershell
# Check .NET installation
dotnet --info

# Restore NuGet packages
cd src
dotnet restore Punchclock.sln
```

### Build Commands

**Working Directory:** All build commands must be run from the `./src` folder.

```powershell
# Build the solution
dotnet build Punchclock.sln -c Release

# Build with warnings as errors (includes StyleCop violations)
dotnet build Punchclock.sln -c Release -warnaserror

# Clean the solution
dotnet clean Punchclock.sln
```

### Test Commands (Microsoft Testing Platform)

**CRITICAL:** This repository uses MTP configured in `global.json`. MTP syntax requires `--project` or `--solution` flags.

**MTP Command Syntax:**
```
dotnet test
    [--project <PROJECT_PATH>]
    [--solution <SOLUTION_PATH>]
    [--test-modules <EXPRESSION>]
    [-c|--configuration <CONFIGURATION>]
    [-f|--framework <FRAMEWORK>]
    [--coverage]
    [--coverage-output-format <FORMAT>]
    [--no-build]
    [--no-restore]
    [<args>...]
```

**What is Microsoft Testing Platform (MTP)?**

MTP is the modern test execution platform for .NET, replacing the legacy VSTest platform. It provides:
- **Native integration** with `dotnet test` command
- **Better performance** through optimized test discovery and execution
- **Modern architecture** designed for current .NET versions (6.0+)
- **Enhanced control** over test execution with detailed filtering and reporting

**Why Punchclock uses MTP:**
- Required for TUnit testing framework (modern alternative to xUnit/NUnit)
- Better integration with build systems and CI/CD pipelines
- Improved test isolation and parallel execution control
- Native support for modern .NET features

**Key Differences from VSTest:**
- **Project/Solution specification:** Use `--project <PATH>` or `--solution <PATH>` (not just the path directly)
- **Test framework arguments:** Passed AFTER `--` separator: `dotnet test --project <PATH> -- --tunit-args`
- **Test module filtering:** Use `--test-modules` with globbing patterns to filter test assemblies
- **Configuration:** MTP is configured via `global.json` (see "Key Configuration Files" section)

**Important Constraints:**
- ⚠️ **Cannot use `--project`, `--solution`, and `--test-modules` together** - only one at a time
- ⚠️ **When using `--test-modules`**, cannot specify `--arch`, `--configuration`, `--framework`, `--os`, or `--runtime` (module is already built)
- ⚠️ **All test projects must use MTP** when opted in via `global.json` - mixing VSTest and MTP is an error

**Configuration:**
- `global.json`: Specifies MTP as the test runner (`"Microsoft.Testing.Platform"`)
- `testconfig.json`: Test execution settings (parallel: false, coverage format)
- `Directory.Build.props`: Common build properties and analyzer configuration

**IMPORTANT Testing Best Practices:**
- **Do NOT use `--no-build` flag** when running tests. Always build before testing to ensure all code changes (including test changes) are compiled. Using `--no-build` can cause tests to run against stale binaries and produce misleading results.
- Use `--output Detailed` to see Console.WriteLine output from tests. This must be placed AFTER the `--` separator
- TUnit runs tests non-parallel by default in this repository (`"parallel": false` in testconfig.json) to avoid test interference

**Working Directory:** All test commands must be run from the `./src` folder.

```powershell
# Run all tests in the solution
dotnet test --solution Punchclock.sln -c Release

# Run all tests in the test project
dotnet test --project Punchclock.Tests/Punchclock.Tests.csproj

# Run a single test method using treenode-filter
# Syntax: /{AssemblyName}/{Namespace}/{ClassName}/{TestMethodName}
dotnet test --project Punchclock.Tests/Punchclock.Tests.csproj -- --treenode-filter "/*/*/*/MyTestMethod"

# Run all tests in a specific class
dotnet test --project Punchclock.Tests/Punchclock.Tests.csproj -- --treenode-filter "/*/*/MyClassName/*"

# Run tests in a specific namespace
dotnet test --project Punchclock.Tests/Punchclock.Tests.csproj -- --treenode-filter "/*/MyNamespace/*/*"

# Filter by test property (e.g., Category)
dotnet test --solution Punchclock.sln -- --treenode-filter "/*/*/*/*[Category=Integration]"

# Run tests with code coverage (Microsoft Code Coverage)
dotnet test --solution Punchclock.sln --coverage --coverage-output-format cobertura

# Run tests with detailed output
dotnet test --solution Punchclock.sln -- --output Detailed

# List all available tests without running them
dotnet test --project Punchclock.Tests/Punchclock.Tests.csproj -- --list-tests

# Fail fast (stop on first failure)
dotnet test --solution Punchclock.sln -- --fail-fast

# Control parallel test execution
dotnet test --solution Punchclock.sln -- --maximum-parallel-tests 4

# Generate TRX report
dotnet test --solution Punchclock.sln -- --report-trx

# Disable logo for cleaner output
dotnet test --project Punchclock.Tests/Punchclock.Tests.csproj -- --disable-logo

# Combine options: coverage + TRX report + detailed output
dotnet test --solution Punchclock.sln --coverage --coverage-output-format cobertura -- --report-trx --output Detailed

# Run tests using test modules (for already-built assemblies)
dotnet test --test-modules "**/bin/**/Release/net9.0/Punchclock.Tests.dll"

# Run tests using test modules with root directory
dotnet test --test-modules "**/Punchclock.Tests.dll" --root-directory "C:\source\punchclock\src"
```

**Alternative: Using `dotnet run` for single project**
```powershell
# Run tests using dotnet run (easier for passing flags)
dotnet run --project Punchclock.Tests/Punchclock.Tests.csproj -c Release -- --treenode-filter "/*/*/*/MyTest"

# Disable logo for cleaner output
dotnet run --project Punchclock.Tests/Punchclock.Tests.csproj -- --disable-logo --treenode-filter "/*/*/*/Test1"
```

### TUnit Treenode-Filter Syntax

The `--treenode-filter` follows the pattern: `/{AssemblyName}/{Namespace}/{ClassName}/{TestMethodName}`

**Examples:**
- Single test: `--treenode-filter "/*/*/*/MyTestMethod"`
- All tests in class: `--treenode-filter "/*/*/MyClassName/*"`
- All tests in namespace: `--treenode-filter "/*/MyNamespace/*/*"`
- Filter by property: `--treenode-filter "/*/*/*/*[Category=Integration]"`
- Multiple wildcards: `--treenode-filter "/*/*/MyTests*/*"`

**Note:** Use single asterisks (`*`) to match segments. Double asterisks (`/**`) are not supported in treenode-filter.

### Key Command-Line Flags

**dotnet test flags (BEFORE `--`):**
- `--coverage` - Enable Microsoft Code Coverage
- `--coverage-output-format` - Set coverage format (cobertura, xml, coverage)

**TUnit flags (AFTER `--`):**
- `--treenode-filter` - Filter tests by path pattern or properties (syntax: `/{Assembly}/{Namespace}/{Class}/{Method}`)
- `--list-tests` - Display available tests without running
- `--fail-fast` - Stop after first failure
- `--maximum-parallel-tests` - Limit concurrent execution (default: processor count)
- `--report-trx` - Generate TRX format reports
- `--output` - Control verbosity (Normal or Detailed)
- `--no-progress` - Suppress progress reporting
- `--disable-logo` - Remove TUnit logo display
- `--diagnostic` - Enable diagnostic logging (Trace level)
- `--timeout` - Set global test timeout
- `--reflection` - Enable reflection mode instead of source generation

See https://tunit.dev/docs/reference/command-line-flags for complete TUnit flag reference.

### Key Configuration Files

- `global.json` - Specifies `"Microsoft.Testing.Platform"` as the test runner
- `testconfig.json` - Configures test execution (`"parallel": false`) and code coverage (Cobertura format)
- `Directory.Build.props` - Common build properties, target frameworks, and analyzer configuration
- `Directory.Packages.props` - Central package management (CPM) for all dependencies
- `.editorconfig` - C# code style and formatting rules

## Architecture Overview

### Core Library Structure

Punchclock is a library for managing concurrent operations with priorities and key-based serialization. It's built on Reactive Extensions (Rx.NET).

**Core Classes (`Punchclock/`)**
- `OperationQueue.cs` - The main scheduler for deferred operations with bounded concurrency
- `OperationQueueExtensions.cs` - Task-based extension methods for easier consumption
- `KeyedOperation.cs` - Internal representation of queued operations with keys and priorities
- `PrioritySemaphoreSubject.cs` - Priority-aware semaphore for controlling concurrent execution
- `ScheduledSubject.cs` - Subject wrapper for scheduled observable operations
- `PriorityQueue.cs` - Internal priority queue implementation

### Key Architectural Patterns

**Operation Scheduling**
- Bounded concurrency via semaphore (default: 4 concurrent operations)
- Priority-based scheduling (higher numbers run first)
- Key-based serialization (operations with the same key run sequentially)
- Pause/resume functionality with reference counting
- Cancellation support via CancellationToken or IObservable

**API Design**
- Observable-based core API (`EnqueueObservableOperation`)
- Task-based extension methods for easier consumption (`Enqueue`)
- Functional reactive programming patterns throughout
- Proper disposal and cleanup support

### Target Frameworks

The project uses target framework definitions in `Directory.Build.props`:

- `PunchclockModernTargets` - net8.0, net9.0, net10.0 (cross-platform, AOT-compatible)
- `PunchclockLegacyTargets` - net462, net472, net481 (Windows-only, built only on Windows)
- `PunchclockCoreTargets` - Combines modern + legacy (when on Windows)

**Platform Requirements:**
- Modern targets (net8.0+) can be built on any platform
- Legacy .NET Framework targets require Windows
- Building on non-Windows will only produce modern targets (this is expected)

### AOT (Ahead-of-Time) Compilation Support

All code targeting net8.0+ must be AOT-compatible (`IsAotCompatible=true`).

**Key AOT Patterns:**
- Prefer `DynamicallyAccessedMembersAttribute` over `UnconditionalSuppressMessage`
- Use specific `DynamicallyAccessedMemberTypes` values rather than `All` when possible
- Avoid reflection where possible
- Use `nameof()` for compile-time property name checking

**Example AOT-Safe Code:**
```csharp
// For methods that access properties via reflection
#if NET6_0_OR_GREATER
private static PropertyInfo? GetPropertyInfo(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    Type type, string propertyName)
#else
private static PropertyInfo? GetPropertyInfo(Type type, string propertyName)
#endif
{
    return type.GetProperty(propertyName);
}
```

## Code Style & Quality Requirements

### Style Enforcement

- EditorConfig rules (`.editorconfig`) - comprehensive C# formatting and naming conventions
- StyleCop Analyzers - builds fail on violations
- Roslynator Analyzers - additional code quality rules
- Analysis level: latest with enhanced .NET analyzers
- `TreatWarningsAsErrors`: true
- `WarningsAsErrors`: nullable
- **All public APIs require XML documentation comments** (including protected methods of public classes)

### C# Style Rules

- **Braces:** Allman style (each brace on new line)
- **Indentation:** 4 spaces, no tabs
- **Fields:** `_camelCase` for private/internal, `s_camelCase` for private/internal static, `readonly` where possible, `static readonly` (not `readonly static`)
- **Visibility:** Always explicit (e.g., `private string _foo` not `string _foo`), visibility first modifier
- **Namespaces:** File-scoped preferred, imports outside namespace, sorted (system then third-party)
- **Types:** Use keywords (`int`, `string`) not BCL types (`Int32`, `String`)
- **Modern C#:** Use nullable reference types, pattern matching, switch expressions, records, init setters, target-typed new, collection expressions, file-scoped namespaces, primary constructors where appropriate
- **Avoid `this.`** unless necessary
- **Use `nameof()`** instead of string literals
- **Use `var`** when it improves readability or aids refactoring

## Testing Guidelines

- Unit tests use **TUnit** framework with **Microsoft Testing Platform**
- Test project: `Punchclock.Tests`
- Test projects detected via naming convention (`.Tests` in project name)
- Coverage configured in `testconfig.json` (Cobertura format, skip auto-properties)
- Non-parallel test execution (`"parallel": false` in testconfig.json)
- Always write unit tests for new features or bug fixes
- Use Verify.TUnit for snapshot testing (API approval tests)
- Follow existing test patterns in `Punchclock.Tests/`

## Common Development Patterns

### Using OperationQueue

```csharp
using Punchclock;
using System.Net.Http;

// Create a queue with maximum 2 concurrent operations
var queue = new OperationQueue(maximumConcurrent: 2);
var http = new HttpClient();

// Enqueue operations with priority (higher number = higher priority)
var t1 = queue.Enqueue(1, () => http.GetStringAsync("https://example.com/a"));
var t2 = queue.Enqueue(1, () => http.GetStringAsync("https://example.com/b"));
var t3 = queue.Enqueue(5, () => http.GetStringAsync("https://example.com/urgent")); // Higher priority

await Task.WhenAll(t1, t2, t3);
```

### Using Keys for Serialization

```csharp
// Operations with the same key run sequentially
var k1 = queue.Enqueue(1, key: "user:42", () => LoadUserAsync(42));
var k2 = queue.Enqueue(1, key: "user:42", () => LoadUserPostsAsync(42));

// These will run one after another, never concurrently
await Task.WhenAll(k1, k2);
```

### Cancellation

```csharp
// Using CancellationToken
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await queue.Enqueue(1, key: "img:1", () => DownloadImageAsync("/1"), cts.Token);

// Using IObservable for cancellation
var cancel = new Subject<Unit>();
var obs = queue.EnqueueObservableOperation(1, "slow", cancel, () => ExpensiveOperation().ToObservable());
cancel.OnNext(Unit.Default); // Cancels the operation
```

### Pause and Resume

```csharp
// Pause the queue (ref-counted)
var gate = queue.PauseQueue();

// Enqueue work while paused - nothing executes yet
var task = queue.Enqueue(1, () => DoWorkAsync());

// Resume by disposing the gate
gate.Dispose(); // Queue resumes and drains according to priority/keys
```

## Common Tasks

### Adding a New Feature

1. Create failing tests first
2. Implement minimal functionality
3. Ensure AOT compatibility for net8.0+
4. Update documentation if needed
5. Add XML documentation to public APIs
6. Verify all tests pass
7. Check that code builds without warnings

### Fixing Bugs

1. Create reproduction test
2. Fix with minimal changes
3. Verify AOT compatibility
4. Ensure no regression in existing tests
5. Update API approval tests if public API changed

## Code Quality and Warning Suppression Policy

### Zero Pragma Policy

**CRITICAL: This project enforces a zero pragma policy for warning suppression.**

- **NO `#pragma warning disable` statements allowed** in any code files
- **All StyleCop analyzer warnings (SA****) MUST be fixed**, not suppressed
- **Code analyzer warnings (CA****) may be suppressed ONLY as a last resort** with the following conditions:
  1. You have attempted to fix the warning first
  2. The fix would make the code worse or is not feasible
  3. You use `[SuppressMessage]` attribute instead of pragma
  4. You provide a clear, valid justification in the `Justification` parameter

### When Suppression Is Acceptable

```csharp
// WRONG - Never use pragma
#pragma warning disable CA1062
public void MyMethod(object parameter)
{
    parameter.ToString();  // CA1062: Validate parameter is non-null
}
#pragma warning restore CA1062

// CORRECT - Fix the issue
public void MyMethod(object parameter)
{
    ArgumentNullException.ThrowIfNull(parameter);
    parameter.ToString();
}

// ACCEPTABLE - SuppressMessage with valid justification (last resort only)
[SuppressMessage("Security", "CA5394:Do not use insecure randomness",
    Justification = "Random is used for non-security purposes (priority queue tiebreaking) and supports deterministic seeding for tests")]
public class OperationQueue
{
    private readonly Random? _random;
    // ...
}
```

### Fixing StyleCop Violations

**StyleCop warnings MUST always be fixed, never suppressed.** Common fixes:

- **SA1202** (Static members before instance): Reorder members
- **SA1204** (Static before non-static): Move static members to top of class
- **SA1402** (One type per file): Move nested types inside parent class or split files
- **SA1611/SA1615** (Missing documentation): Add XML documentation tags
- **SA1600** (Elements should be documented): Add XML documentation to public members

### Enforcement

- Build will fail with `-warnaserror` flag if any analyzer warnings exist
- All pragma directives will be flagged during code review
- Use of `SuppressMessage` requires justification and review approval

## What to Avoid

- **Reflection** without proper AOT suppression (for net8.0+ targets)
- **Breaking changes** to public APIs without proper versioning
- **Blocking operations** in async code paths
- **Excessive allocations** in hot paths (this is a performance-critical library)
- **Thread-unsafe code** without proper synchronization

## Important Notes

- **Repository Location:** Working directory is `C:\source\punchclock`
- **Source Location:** `src/` folder contains the solution and all projects
- **Main Solution:** `Punchclock.sln`
- **Required .NET SDKs:** .NET 8.0, 9.0, and 10.0 (all three required for modern targets)
- **Optional:** .NET Framework 4.6.2, 4.7.2, 4.8.1 SDKs (only needed on Windows for legacy targets)
- **Version Management:** Uses Nerdbank.GitVersioning for semantic versioning based on git history
- **Package Management:** Central Package Management (CPM) via `Directory.Packages.props`

## Project Philosophy

Punchclock emphasizes:
- **Simplicity:** Small, focused API surface
- **Correctness:** Proper synchronization and disposal
- **Performance:** Efficient scheduling and minimal allocations
- **Composability:** Works naturally with Rx.NET and Task-based async
- **Reliability:** Comprehensive test coverage and AOT compatibility

When in doubt:
- Prefer reactive streams over imperative code
- Consider the AOT implications of your changes
- Keep the API surface small and focused
- Write tests first
- Ensure thread safety
