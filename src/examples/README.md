# Schedule profile work

Run either sample from `src`. The examples use local tasks and signals so the order of events can be checked directly.

```console
dotnet run --project examples/Queue/Queue.csproj --framework net11.0
dotnet run --project examples/ReactiveQueue/ReactiveQueue.csproj --framework net11.0
```

Use `net10.0` to run on .NET 10. Both packages run the same [scheduling scenario](Queue/Program.cs).
`Queue` uses Punchclock with ReactiveUI.Primitives. `ReactiveQueue` uses Punchclock.Reactive with System.Reactive.

| Method | What it checks |
| --- | --- |
| `SaveWithQueueOptionsAsync` | Run a local save, lookup and clear with the default queue, an explicit slot count, a supplied scheduler and seeded equal-priority scheduling. Shows keyed and unkeyed delegates returning `Task` and `Task<T>`. |
| `ScheduleByPriorityAsync` | Pause a one-slot queue, enqueue two keyed tasks and resume it. The higher priority task starts first. |
| `SerializeEditsAsync` | Open a second slot, serialize two edits with the same key, read an unkeyed result and cancel a queued save. |
| `LoadPreviewAsync` | Signal cancellation while paused, then consume keyed and unkeyed observable results. |
| `Main` | Set the scheduler and seed for equal-priority work, wait for shutdown and dispose the queue. |
