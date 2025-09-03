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

- Bounded concurrency with a priority-aware semaphore
- Priority scheduling (higher number runs first)
- Key-based serialization (only one operation per key runs at a time)
- Pause/resume with reference counting
- Cancellation via CancellationToken or IObservable
- Task and IObservable friendly API

## Install

- NuGet: `dotnet add package Punchclock`

## Quick start

```csharp
using Punchclock;
using System.Net.Http;

var queue = new OperationQueue(maximumConcurrent: 2);
var http = new HttpClient();

// Fire a bunch of downloads – only two will run at a time
var t1 = queue.Enqueue(1, () => http.GetStringAsync("https://example.com/a"));
var t2 = queue.Enqueue(1, () => http.GetStringAsync("https://example.com/b"));
var t3 = queue.Enqueue(1, () => http.GetStringAsync("https://example.com/c"));
await Task.WhenAll(t1, t2, t3);
```

## Priorities

- Higher numbers win. A priority 10 operation will preempt priority 1 when a slot opens.

```csharp
await queue.Enqueue(10, () => http.GetStringAsync("https://example.com/urgent"));
```

## Keys: serialize related work

- Use a key to ensure only one operation for that key runs at a time.
- Useful to avoid thundering herds against the same resource.

```csharp
// These will run one-after-another because they share the same key
var k1 = queue.Enqueue(1, key: "user:42", () => LoadUserAsync(42));
var k2 = queue.Enqueue(1, key: "user:42", () => LoadUserPostsAsync(42));
await Task.WhenAll(k1, k2);
```

## Cancellation

- Via CancellationToken:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
await queue.Enqueue(1, key: "img:1", () => DownloadImageAsync("/1"), cts.Token);
```

- Via IObservable cancellation signal:

```csharp
var cancel = new Subject<Unit>();
var obs = queue.EnqueueObservableOperation(1, "slow", cancel, () => Expensive().ToObservable());
cancel.OnNext(Unit.Default); // cancels if not yet running or in-flight
```

## Pause and resume

```csharp
var gate = queue.PauseQueue();
// enqueue work while paused; nothing executes yet
// ...
gate.Dispose(); // resumes and drains respecting priority/keys
```

## Adjust concurrency at runtime

```csharp
queue.SetMaximumConcurrent(8); // increases throughput
```

## Shutting down

```csharp
await queue.ShutdownQueue(); // completes when outstanding work finishes
```

## API overview

- OperationQueue
  - ctor(int maximumConcurrent = 4)
  - IObservable<T> EnqueueObservableOperation<T>(int priority, Func<IObservable<T>>)
  - IObservable<T> EnqueueObservableOperation<T>(int priority, string key, Func<IObservable<T>>)
  - IObservable<T> EnqueueObservableOperation<T, TDontCare>(int priority, string key, IObservable<TDontCare> cancel, Func<IObservable<T>>)
  - IDisposable PauseQueue()
  - void SetMaximumConcurrent(int maximumConcurrent)
  - IObservable<Unit> ShutdownQueue()

- OperationQueueExtensions
  - Task Enqueue(int priority, Func<Task>)
  - Task<T> Enqueue<T>(int priority, Func<Task<T>>)
  - Task Enqueue(int priority, string key, Func<Task>)
  - Task<T> Enqueue<T>(int priority, string key, Func<Task<T>>)
  - Overloads with CancellationToken for all of the above

## Best practices

- Prefer Task-based Enqueue APIs in application code; use observable APIs when composing with Rx.
- Use descriptive keys for shared resources (e.g., "user:{id}", "file:{path}").
- Keep operations idempotent and short; long operations block concurrency slots.
- Use higher priorities sparingly; they jump the queue when a slot opens.
- PauseQueue is ref-counted; always dispose the returned handle exactly once.
- For cancellation via token, reuse CTS per user action to cancel pending work quickly.

## Advanced notes

- Unkeyed work is prioritized ahead of keyed work internally to keep the pipeline flowing; keys are serialized per group.
- The semaphore releases when an operation completes, errors, or is canceled.
- Cancellation before evaluation prevents invoking the supplied function.

## Full examples

- Image downloader with keys and priorities

```csharp
var queue = new OperationQueue(3);

Task Download(string url, string dest, int pri, string key) =>
    queue.Enqueue(pri, key, async () =>
    {
        using var http = new HttpClient();
        var bytes = await http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(dest, bytes);
    });

var tasks = new[]
{
    Download("https://example.com/a.jpg", "a.jpg", 1, "img"),
    Download("https://example.com/b.jpg", "b.jpg", 1, "img"),
    queue.Enqueue(5, () => Task.Delay(100)), // higher priority misc work
};
await Task.WhenAll(tasks);
```

## Troubleshooting

- Nothing runs? Ensure you didn't leave the queue paused. Dispose the token from PauseQueue.
- Starvation? Check if you assigned very high priorities to long-running tasks.
- Deadlock-like behavior with keys? Remember keyed operations are strictly serialized; avoid long critical sections.

## Contribute

Punchclock is developed under an OSI-approved open source license, making it freely usable and distributable, even for commercial use. Because of our Open Collective model for funding and transparency, we are able to funnel support and funds through to our contributors and community. We ❤ the people who are involved in this project, and we’d love to have you on board, especially if you are just getting started or have never contributed to open-source before.

So here's to you, lovely person who wants to join us — this is how you can support us:

- [Responding to questions on StackOverflow](https://stackoverflow.com/questions/tagged/punchclock)
- [Passing on knowledge and teaching the next generation of developers](https://ericsink.com/entries/dont_use_rxui.html)
- Submitting documentation updates where you see fit or lacking.
- Making contributions to the code base.
