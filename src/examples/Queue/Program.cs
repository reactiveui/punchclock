// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace QueueExamples;

/// <summary>Schedules profile edits and background work on one queue.</summary>
internal static class Program
{
    /// <summary>Groups operations for the same profile.</summary>
    private const string Profile = "profile";

    /// <summary>Runs waiting foreground work first.</summary>
    private const int Foreground = 10;

    /// <summary>Bounds the time to finish every example.</summary>
    private const int TimeoutSeconds = 30;

    /// <summary>Runs the queue and drains it before exit.</summary>
    /// <returns>Completion after all results have been checked.</returns>
    private static async Task Main()
    {
        using var queue = new OperationQueue(maximumConcurrent: 1, randomizeEqualPriority: true, seed: 1, scheduler: QueueScheduler.Immediate);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
        await SaveWithQueueOptionsAsync().WaitAsync(deadline.Token);
        await ScheduleByPriorityAsync(queue, deadline.Token);
        await SerializeEditsAsync(queue, deadline.Token);
        await LoadPreviewAsync(queue, deadline.Token);
        _ = await queue.ShutdownQueue().ToTask(deadline.Token);
    }

    /// <summary>Runs a local save and lookup with each queue configuration.</summary>
    /// <returns>Completion after verifying the stored profile and draining each queue.</returns>
    private static async Task SaveWithQueueOptionsAsync()
    {
        Func<OperationQueue>[] configurations =
        [
            static () => new OperationQueue(),
            static () => new OperationQueue(maximumConcurrent: 1),
            static () => new OperationQueue(maximumConcurrent: 1, QueueScheduler.Immediate),
            static () => new OperationQueue(maximumConcurrent: 1, randomizeEqualPriority: true, seed: 1),
        ];
        foreach (var configure in configurations)
        {
            using var queue = configure();
            var records = new Dictionary<string, string>();
            await queue.Enqueue(0, Profile, () =>
            {
                records[Profile] = "Ada";
                return Task.CompletedTask;
            });
            var saved = await queue.Enqueue(Foreground, Profile, () => Task.FromResult(records[Profile]));
            await queue.Enqueue(0, () =>
            {
                records.Clear();
                return Task.CompletedTask;
            });
            _ = await queue.ShutdownQueue().ToTask(CancellationToken.None);
            if (saved != "Ada" || records.Count != 0)
            {
                throw new InvalidOperationException("The queue should save, retrieve and clear the profile in order.");
            }
        }
    }

    /// <summary>Queues two requests while paused so both compete for the same slot.</summary>
    /// <param name="queue">The single-slot queue.</param>
    /// <param name="token">Cancels the foreground request or the wait.</param>
    /// <returns>Completion after the order is checked.</returns>
    private static async Task ScheduleByPriorityAsync(OperationQueue queue, CancellationToken token)
    {
        const string Index = "index";
        var order = new List<string>();
        Task background;
        Task<string> foreground;
        using (queue.PauseQueue())
        {
            background = queue.Enqueue(
                0,
                Index,
                () =>
                {
                    order.Add(Index);
                    return Task.CompletedTask;
                },
                token);
            foreground = queue.Enqueue(
                Foreground,
                Profile,
                () =>
                {
                    order.Add(Profile);
                    return Task.FromResult("Ada");
                },
                token);
        }

        await Task.WhenAll(background, foreground).WaitAsync(token);
        if (order.SequenceEqual([Profile, Index]) && await foreground == "Ada")
        {
            return;
        }

        throw new InvalidOperationException("The foreground profile should be loaded first.");
    }

    /// <summary>Holds an edit open while a second edit waits on its key.</summary>
    /// <param name="queue">The queue shared by both edits.</param>
    /// <param name="token">Cancels the save or the wait.</param>
    /// <returns>Completion after both edits and a cancelled operation are checked.</returns>
    private static async Task SerializeEditsAsync(OperationQueue queue, CancellationToken token)
    {
        const int Slots = 2;
        queue.SetMaximumConcurrent(Slots);
        var save = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = queue.Enqueue(0, Profile, () => save.Task.WaitAsync(token), token);
        var second = queue.Enqueue(0, Profile, static () => Task.FromResult("saved"), token);
        if (second.IsCompleted)
        {
            throw new InvalidOperationException("The second edit should wait for the first.");
        }

        save.SetResult();
        await Task.WhenAll(first, second).WaitAsync(token);
        var title = await queue.Enqueue(0, static () => Task.FromResult(Profile));
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var skipped = queue.Enqueue(0, Profile, static () => Task.CompletedTask, cancel.Token);
        if (skipped.IsCanceled && title == Profile && await second == "saved")
        {
            return;
        }

        throw new InvalidOperationException("The saved profile should survive the cancelled edit.");
    }

    /// <summary>Cancels a queued observable when the preview closes.</summary>
    /// <param name="queue">The queue shared by the previews.</param>
    /// <param name="token">Cancels waiting for the visible preview.</param>
    /// <returns>Completion after checking which preview ran.</returns>
    private static async Task LoadPreviewAsync(OperationQueue queue, CancellationToken token)
    {
        using var closed = new PreviewClosed();
        var started = false;
        using (queue.PauseQueue())
        {
            using var subscription = queue.EnqueueObservableOperation(
                0,
                Profile,
                closed,
                () =>
                {
                    started = true;
                    return Signal.Emit("closed");
                }).Subscribe(static _ => { });
            closed.OnNext(true);
        }

        var preview = await queue.EnqueueObservableOperation(Foreground, Profile, static () => Signal.Emit("ready")).ToTask(token);
        var count = await queue.EnqueueObservableOperation(0, static () => Signal.Emit(1)).ToTask(token);
        if (!started && preview == "ready" && count == 1)
        {
            return;
        }

        throw new InvalidOperationException("Only the open preview should run.");
    }
}
