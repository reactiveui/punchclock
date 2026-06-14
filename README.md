[![NuGet Stats](https://img.shields.io/nuget/v/punchclock.svg)](https://www.nuget.org/packages/punchclock) ![Build](https://github.com/reactiveui/punchclock/workflows/Build/badge.svg)
 [![Code Coverage](https://codecov.io/gh/reactiveui/punchclock/branch/main/graph/badge.svg)](https://codecov.io/gh/reactiveui/punchclock) [![#yourfirstpr](https://img.shields.io/badge/first--timers--only-friendly-blue.svg)](https://reactiveui.net/contribute)
<br>

<br />
<a href="https://github.com/reactiveui/punchclock">
  <img width="120" heigth="120" src="https://raw.githubusercontent.com/reactiveui/styleguide/master/logo_punchclock/main.png">
</a>

## Punchclock: A library for managing concurrent operations

Punchclock is the low-level scheduling and prioritization library used by
[Fusillade](https://github.com/reactiveui/Fusillade) to orchestrate pending
concurrent operations.

### What even does that mean?

Ok, so you've got a shiny mobile phone app and you've got async/await.
Awesome! It's so easy to issue network requests, why not do it all the time?
After your users one-:star2: you for your app being slow, you discover that
you're issuing *way* too many requests at the same time.

Then, you try to manage issuing less requests by hand, and it becomes a
spaghetti mess as different parts of your app reach into each other to try to
figure out who's doing what. Let's figure out a better way.

## Key features

- Bounded concurrency so only a fixed number of operations run at once
- Priority scheduling where higher numbers run first when a slot opens
- Key-based serialization so related work runs one-at-a-time
- Task and `IObservable<T>` APIs over the same queueing engine
- Cancellation via `CancellationToken` or an observable signal
- Pause/resume with reference counting
- Runtime concurrency changes with `SetMaximumConcurrent`
- Shutdown that waits for queued and in-flight work to finish
- V7.0.0+ Built on `ReactiveUI.Primitives` for signals, disposables, `RxVoid`, and sequencing
- Public API tracking for every target framework

## Install

- NuGet: `dotnet add package Punchclock`

Punchclock currently targets modern .NET (`net8.0`, `net9.0`, `net10.0`,
`net11.0`) and .NET Framework (`net462`, `net472`, `net48`, `net481`).

Punchclock v7.0.0 moves the queue internals and observable examples onto
`ReactiveUI.Primitives`. If you are migrating from Punchclock v6 code that
still uses System.Reactive, or you want to bridge to R3 at the edge of your
application, reference `ReactiveUI.Primitives` directly so its source-generator
bridge analyzers are available to your project. The generators do not add a
runtime Rx or R3 dependency to Punchclock; they emit adapters only when your
app already references `System.Reactive`, `System.Reactive.Async`, `R3`, or
`R3Async`.

For a v6-style System.Reactive migration, keep the Rx package while you move
code across the boundary:

```powershell
dotnet add package Punchclock --version 7.0.0
dotnet add package ReactiveUI.Primitives
dotnet add package System.Reactive
```

Then import the generated System.Reactive bridge namespace:

```csharp
using Punchclock;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.SystemReactiveBridge;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using var queue = new OperationQueue(maximumConcurrent: 2);
using var http = new HttpClient();

var cancelFromLegacyRx = new Subject<RxVoid>();

IObservable<string> rxFriendlyResult =
    queue.EnqueueObservableOperation(
        priority: 5,
        key: "legacy:refresh",
        cancel: cancelFromLegacyRx.AsPrimitivesSignal(),
        asyncCalculationFunc: () =>
            Observable
                .FromAsync(() => http.GetStringAsync("https://example.com/legacy"))
                .AsPrimitivesSignal())
    .AsSystemObservable();

using var subscription = System.ObservableExtensions.Subscribe(
    Observable.Timeout(rxFriendlyResult, TimeSpan.FromSeconds(10)),
    value => Console.WriteLine(value),
    error => Console.Error.WriteLine(error));

cancelFromLegacyRx.OnNext(RxVoid.Default);
```

The R3 bridge works the same way from the generated
`ReactiveUI.Primitives.R3Bridge` namespace when the consuming project references
R3.

Most application code only needs this:

```csharp
using Punchclock;
```

Observable examples also use the ReactiveUI.Primitives signal helpers:

```csharp
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;
```

## Quick start

```csharp
using Punchclock;
using System.Net.Http;

using var queue = new OperationQueue(maximumConcurrent: 2);
using var http = new HttpClient();

// Fire a bunch of downloads. Only two will run at a time.
var t1 = queue.Enqueue(1, () => http.GetStringAsync("https://example.com/a"));
var t2 = queue.Enqueue(1, () => http.GetStringAsync("https://example.com/b"));
var t3 = queue.Enqueue(10, () => http.GetStringAsync("https://example.com/urgent"));
await Task.WhenAll(t1, t2, t3);
```

## In 60 seconds

Create one queue near the part of your app that owns the work, then send work
through it instead of letting every caller start its own request immediately.

```csharp
using Punchclock;

using var queue = new OperationQueue(maximumConcurrent: 4);

Task<string> LoadProfile(int userId) =>
    queue.Enqueue(
        priority: 5,
        key: $"user:{userId}",
        asyncOperation: () => api.GetProfileAsync(userId));

Task<string> LoadTimeline(int userId) =>
    queue.Enqueue(
        priority: 1,
        key: $"user:{userId}",
        asyncOperation: () => api.GetTimelineAsync(userId));

var profile = LoadProfile(42);
var timeline = LoadTimeline(42);

await Task.WhenAll(profile, timeline);
```

Those two operations share the same key, so they will not run at the same time.
Other keys can still use the remaining concurrency slots.

## Priorities

Higher numbers win. A priority `10` operation is chosen ahead of priority `1`
when a slot opens.

```csharp
await queue.Enqueue(10, () => http.GetStringAsync("https://example.com/urgent"));
```

Priorities do not cancel work that is already running. They decide which pending
operation gets the next available slot.

Equal-priority operations are FIFO by default. If you want to avoid one caller
always winning equal-priority tie-breaks across different keys, enable
randomization:

```csharp
using var queue = new OperationQueue(
    maximumConcurrent: 4,
    randomizeEqualPriority: true,
    seed: null);
```

Use a `seed` when you want deterministic randomized ordering in tests:

```csharp
using var queue = new OperationQueue(4, randomizeEqualPriority: true, seed: 1234);
```

## Keys: serialize related work

- Use a key to ensure only one operation for that key runs at a time.
- Useful to avoid thundering herds against the same resource.
- Different keys can run together up to the queue's concurrency limit.
- `null`, `string.Empty`, and the internal default key are treated as non-keyed work.

```csharp
// These will run one-after-another because they share the same key.
var k1 = queue.Enqueue(
    priority: 1,
    key: "user:42",
    asyncOperation: () => LoadUserAsync(42));

var k2 = queue.Enqueue(
    priority: 1,
    key: "user:42",
    asyncOperation: () => LoadUserPostsAsync(42));
await Task.WhenAll(k1, k2);
```

Use keys for the resource you are protecting, not for the operation type:

```csharp
await Task.WhenAll(
    queue.Enqueue(
        priority: 1,
        key: "file:avatar.png",
        asyncOperation: () => ResizeAsync("avatar.png")),
    queue.Enqueue(
        priority: 1,
        key: "file:avatar.png",
        asyncOperation: () => UploadAsync("avatar.png")),
    queue.Enqueue(
        priority: 1,
        key: "file:banner.png",
        asyncOperation: () => UploadAsync("banner.png")));
```

The two `avatar.png` operations serialize. The `banner.png` operation can run
beside them if a slot is available.

## Cancellation

Via `CancellationToken`:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
await queue.Enqueue(
    priority: 1,
    key: "img:1",
    asyncOperation: () => DownloadImageAsync("/1"),
    token: cts.Token);
```

Via an observable cancellation signal:

```csharp
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

var cancel = new Signal<RxVoid>();

var obs = queue.EnqueueObservableOperation(
    priority: 1,
    key: "slow",
    cancel: cancel,
    asyncCalculationFunc: () => Signal.FromTask(ExpensiveAsync()));

using var subscription = obs.Subscribe(
    value => Console.WriteLine(value),
    error => Console.Error.WriteLine(error));

cancel.OnNext(RxVoid.Default); // Cancels if pending or while observed in-flight.
```

An already-canceled `CancellationToken` returns a canceled task without queueing
anything. A non-cancelable token uses a fast path with no cancellation
registration.

When an observable cancellation signal fires before the operation is evaluated,
the operation factory is not invoked.

## Pause and resume

```csharp
using var gate = queue.PauseQueue();

// Enqueue work while paused; nothing new executes yet.
// ...

gate.Dispose(); // Resumes and drains respecting priority/keys.
```

Pause is reference counted. If two callers pause the queue, the queue resumes
only after both returned handles have been disposed.

In-flight operations are not canceled by pausing. Pause only stops dispatching
new work.

## Adjust concurrency at runtime

```csharp
queue.SetMaximumConcurrent(8); // increases throughput
```

You can increase or decrease the concurrency limit while the queue is alive.
The value must be positive; constructors and `SetMaximumConcurrent` throw
`ArgumentOutOfRangeException` for zero or negative values.

Lowering the value does not cancel already-running operations. It limits future
dispatch until the active count drops below the new limit.

## Shutting down

```csharp
using ReactiveUI.Primitives.Concurrency;

await queue.ShutdownQueue().ToTask();
```

`ShutdownQueue` starts shutdown and returns an `IObservable<RxVoid>` that
signals when queued and in-flight operations have finished. After shutdown has
started, new enqueue attempts throw `InvalidOperationException`.

Calling `ShutdownQueue` more than once is safe. Repeated calls return the same
shutdown observable.

## ReactiveUI.Primitives base

Punchclock now uses `ReactiveUI.Primitives` as its reactive foundation. The
public API still feels small:

- Task callers use `queue.Enqueue(...)`.
- Observable callers use `queue.EnqueueObservableOperation(...)`.
- Void observable signals use `RxVoid` instead of `Unit`.
- Cancellation and examples use `Signal<T>`.
- Advanced scheduling can be controlled with `ISequencer`.

This keeps the queue independent from any UI framework while still giving
ReactiveUI-style applications a natural observable API.

```csharp
using Punchclock;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

using var queue = new OperationQueue(maximumConcurrent: 1);

IObservable<string> pending = queue.EnqueueObservableOperation(
    priority: 3,
    key: "refresh",
    asyncCalculationFunc: () => Signal.FromTask(RefreshAsync()));

string result = await pending.ToTask();
```

## Task API

Use the Task API when your application is already written with `async`/`await`.
It is the most direct API for app code.

```csharp
Task SaveAsync(Document document, CancellationToken token) =>
    queue.Enqueue(
        priority: 5,
        key: $"document:{document.Id}",
        asyncOperation: () => repository.SaveAsync(document, token),
        token: token);
```

Non-generic operations return `Task`:

```csharp
await queue.Enqueue(
    priority: 1,
    key: "cache:trim",
    asyncOperation: () => cache.TrimAsync());
```

Generic operations return `Task<T>`:

```csharp
User user = await queue.Enqueue(
    priority: 3,
    key: "user:42",
    asyncOperation: () => api.GetUserAsync(42));
```

Leave the key out when the operation does not need serialization:

```csharp
var response = await queue.Enqueue(
    priority: 1,
    asyncOperation: () => http.GetStringAsync("https://example.com/status"));
```

## Observable API

Use the observable API when you want to compose the queued operation with other
observable streams.

```csharp
IObservable<byte[]> image = queue.EnqueueObservableOperation(
    priority: 2,
    key: "image:42",
    asyncCalculationFunc: () => Signal.FromTask(DownloadImageAsync(42)));

using var subscription = image.Subscribe(bytes =>
{
    Console.WriteLine($"Downloaded {bytes.Length} bytes");
});
```

With an observable cancellation signal:

```csharp
var cancel = new Signal<RxVoid>();

IObservable<SearchResult> search = queue.EnqueueObservableOperation(
    priority: 10,
    key: "search",
    cancel: cancel,
    asyncCalculationFunc: () => Signal.FromTask(SearchAsync("punchclock")));

using var subscription = search.Subscribe(result => Render(result));

cancel.OnNext(RxVoid.Default);
```

Operation factory exceptions and operation observable errors flow to that
operation's result. The queue still releases capacity and continues processing
later work.

## Custom sequencing

Most apps can use the default `Sequencer.Immediate`. Tests and hosts with their
own execution model can pass an `ISequencer`.

```csharp
using ReactiveUI.Primitives.Concurrency;

ISequencer sequencer = Sequencer.Immediate;
using var queue = new OperationQueue(maximumConcurrent: 2, scheduler: sequencer);
```

The sequencer controls when scheduled operations are started after they have
been selected by the queue.

## API overview

### `OperationQueue`

Constructors:

```csharp
new OperationQueue();
new OperationQueue(int maximumConcurrent);
new OperationQueue(int maximumConcurrent, ISequencer scheduler);
new OperationQueue(int maximumConcurrent, bool randomizeEqualPriority, int? seed);
new OperationQueue(
    int maximumConcurrent,
    bool randomizeEqualPriority,
    int? seed,
    ISequencer? scheduler);
```

The default constructor uses `maximumConcurrent: 4`. Any constructor that takes
`maximumConcurrent` requires a positive value.

Observable enqueue methods:

```csharp
IObservable<T> EnqueueObservableOperation<T>(
    int priority,
    Func<IObservable<T>> asyncCalculationFunc);

IObservable<T> EnqueueObservableOperation<T>(
    int priority,
    string key,
    Func<IObservable<T>> asyncCalculationFunc);

IObservable<T> EnqueueObservableOperation<T, TDontCare>(
    int priority,
    string key,
    IObservable<TDontCare> cancel,
    Func<IObservable<T>> asyncCalculationFunc);
```

Queue control:

```csharp
IDisposable PauseQueue();
void SetMaximumConcurrent(int maximumConcurrent);
IObservable<RxVoid> ShutdownQueue();
void Dispose();
```

`OperationQueue` also has a protected virtual `Dispose(bool isDisposing)` for
derived types.

### `OperationQueueExtensions`

Task helpers are exposed as extension methods on `OperationQueue`:

```csharp
Task Enqueue(int priority, Func<Task> asyncOperation);
Task<T> Enqueue<T>(int priority, Func<Task<T>> asyncOperation);

Task Enqueue(int priority, string key, Func<Task> asyncOperation);
Task<T> Enqueue<T>(int priority, string key, Func<Task<T>> asyncOperation);

Task Enqueue(
    int priority,
    string key,
    Func<Task> asyncOperation,
    CancellationToken token);

Task<T> Enqueue<T>(
    int priority,
    string key,
    Func<Task<T>> asyncOperation,
    CancellationToken token);
```

The public API baseline also records the compiler-generated static extension
method entries. Consumers should call them as normal extension methods:

```csharp
await queue.Enqueue(1, () => DoWorkAsync());
```

## Behavior details

- `maximumConcurrent` is the upper bound for active operations.
- Higher priority pending operations are selected first.
- Equal priorities are FIFO unless randomized tie-breaking is enabled.
- Operations with the same non-empty key are serialized.
- Operations with different keys may run concurrently.
- Non-keyed work can run concurrently with other non-keyed work.
- Non-keyed pending work is considered ahead of keyed pending work internally so one serialized key does not unnecessarily hold the whole pipeline back.
- Pausing stops new dispatch only; active operations keep running.
- Shutdown drains pending and active work, then signals `RxVoid.Default` and completes.
- Enqueueing after shutdown starts throws `InvalidOperationException`.
- Cancellation before evaluation prevents the factory from being invoked.
- Factory exceptions and operation errors are delivered to that operation and do not permanently break the queue.
- `Dispose` is safe to call repeatedly and cleans up pending cancellation subscriptions.

## Best practices

- Prefer Task-based Enqueue APIs in application code; use observable APIs when composing with Rx.
- Use descriptive keys for shared resources (e.g., "user:{id}", "file:{path}").
- Keep operations idempotent and short; long operations block concurrency slots.
- Use higher priorities sparingly; they jump the queue when a slot opens.
- PauseQueue is ref-counted; always dispose the returned handle exactly once.
- For cancellation via token, reuse CTS per user action to cancel pending work quickly.
- Treat the queue as infrastructure owned by a feature or service. Avoid creating a new queue for every operation.
- Pick a priority scale and keep it boring: for example, `1` background, `5` user-visible, `10` urgent.
- Include the resource identity in keys. `user:42` is more useful than `user`.
- Prefer `ShutdownQueue` for graceful application teardown and `Dispose` for cleanup.

## Advanced notes

- Unkeyed work is prioritized ahead of keyed work internally to keep the pipeline flowing; keys are serialized per group.
- The semaphore releases when an operation completes, errors, or is canceled.
- Cancellation before evaluation prevents invoking the supplied function.
- A pause handle created after shutdown starts will not resume dispatch.
- `ShutdownQueue` is idempotent; all callers observe the same shutdown signal.
- Randomized equal-priority scheduling only affects tie-breaks. Priority still wins.
- A seeded random queue is useful for deterministic tests.
- `ISequencer` exists for advanced scheduling and test control. The default is immediate scheduling.

## Real-world patterns

### Request throttling

```csharp
using var queue = new OperationQueue(maximumConcurrent: 3);

Task<string> GetJsonAsync(string url, CancellationToken token) =>
    queue.Enqueue(
        priority: 1,
        key: url,
        asyncOperation: () => http.GetStringAsync(url, token),
        token: token);
```

This limits total HTTP pressure while serializing repeated calls to the same
URL.

### User-visible work beats background work

```csharp
Task RefreshVisibleItemAsync(int id) =>
    queue.Enqueue(
        priority: 10,
        key: $"item:{id}",
        asyncOperation: () => api.RefreshItemAsync(id));

Task WarmCacheAsync(int id) =>
    queue.Enqueue(
        priority: 1,
        key: $"item:{id}",
        asyncOperation: () => cache.WarmAsync(id));
```

The visible refresh gets the next slot before lower-priority cache warming.
The shared key still prevents both operations from touching the same item at
the same time.

### One queue, many keys

```csharp
var tasks = documents.Select(document =>
    queue.Enqueue(
        priority: 2,
        key: $"document:{document.Id}",
        asyncOperation: () => SyncDocumentAsync(document)));

await Task.WhenAll(tasks);
```

Every document sync is isolated by key, but unrelated documents can still run
in parallel.

### Observable refresh

```csharp
var refresh = queue.EnqueueObservableOperation(
    priority: 5,
    key: "dashboard",
    asyncCalculationFunc: () => Signal.FromTask(LoadDashboardAsync()));

using var subscription = refresh.Subscribe(model => Render(model));
```

## Full examples

### Image downloader with keys and priorities

```csharp
using Punchclock;

using var queue = new OperationQueue(3);
using var http = new HttpClient();

Task Download(string url, string dest, int pri, string key) =>
    queue.Enqueue(
        priority: pri,
        key: key,
        asyncOperation: async () =>
        {
            var bytes = await http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(dest, bytes);
        });

var tasks = new[]
{
    Download("https://example.com/a.jpg", "a.jpg", 1, "img"),
    Download("https://example.com/b.jpg", "b.jpg", 1, "img"),
    queue.Enqueue(priority: 5, asyncOperation: () => Task.Delay(100)), // higher priority misc work
};
await Task.WhenAll(tasks);
```

### Graceful shutdown

```csharp
using ReactiveUI.Primitives.Concurrency;

using var queue = new OperationQueue(2);

var upload = queue.Enqueue(
    priority: 5,
    key: "upload:1",
    asyncOperation: () => UploadAsync("1"));

var cache = queue.Enqueue(
    priority: 1,
    asyncOperation: () => WarmCacheAsync());

await queue.ShutdownQueue().ToTask();
await Task.WhenAll(upload, cache);
```

### Pause while batching

```csharp
using var queue = new OperationQueue(4);

using (queue.PauseQueue())
{
    foreach (var id in ids)
    {
        _ = queue.Enqueue(
            priority: 1,
            key: $"item:{id}",
            asyncOperation: () => RefreshAsync(id));
    }
}

// The queue resumes here and drains according to priority and key.
```

## Performance and behavior

Punchclock is intended for application-level operation scheduling: network
requests, disk work, cache refreshes, API calls, and other async operations
where too much parallelism hurts more than it helps.

It is not a CPU work-stealing scheduler, a job runner, or a durable background
queue. If you need persistence, retries across process restarts, distributed
workers, or cron-style scheduling, pair Punchclock with a tool designed for
that job.

The queue is thread-safe for normal enqueue/control usage. Keep the operation
body itself thread-safe too, especially when multiple keys can run together.

## FAQ

### Does priority interrupt running work?

No. Priority decides the next pending operation when a concurrency slot opens.

### Do keys limit global concurrency?

No. Keys serialize work for the same key. The queue still uses the global
`maximumConcurrent` limit across all active work.

### What should I use for a "void" observable?

Use `ReactiveUI.Primitives.RxVoid`. Emit `RxVoid.Default`.

### Should I create one queue or many queues?

Usually one queue per feature, subsystem, or external resource is enough. Too
many queues make global concurrency harder to reason about.

### What happens when an operation throws?

The error is delivered to that operation's task or observable. The queue
releases capacity and continues processing later operations.

## Troubleshooting

- Nothing runs? Ensure you didn't leave the queue paused. Dispose the token from PauseQueue.
- Starvation? Check if you assigned very high priorities to long-running tasks.
- Deadlock-like behavior with keys? Remember keyed operations are strictly serialized; avoid long critical sections.
- `InvalidOperationException` when enqueueing? Shutdown has already started.
- `ArgumentOutOfRangeException` from the constructor or `SetMaximumConcurrent`? Use a value greater than zero.
- Observable shutdown does not appear to finish? Make sure the returned `IObservable<RxVoid>` is subscribed, or convert it with `ToTask()`.

## Contribute

Punchclock is developed under an OSI-approved open source license, making it freely usable and distributable, even for commercial use. Because of our Open Collective model for funding and transparency, we are able to funnel support and funds through to our contributors and community. We ❤ the people who are involved in this project, and we’d love to have you on board, especially if you are just getting started or have never contributed to open-source before.

So here's to you, lovely person who wants to join us — this is how you can support us:

- [Responding to questions on StackOverflow](https://stackoverflow.com/questions/tagged/punchclock)
- [Passing on knowledge and teaching the next generation of developers](https://ericsink.com/entries/dont_use_rxui.html)
- Submitting documentation updates where you see fit or lacking.
- Making contributions to the code base.
