// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace Punchclock.Reactive;
#else

namespace Punchclock;
#endif

/// <summary>Extension methods associated with the <see cref="OperationQueue"/>. Provides convenient Task-based overloads for enqueueing operations.</summary>
public static class OperationQueueExtensions
{
    /// <summary>Enqueues an operation to the operation queue with priority, key, and cancellation support.</summary>
    /// <param name="operationQueue">The operation queue to add the operation to.</param>
    extension(OperationQueue operationQueue)
    {
        /// <summary>Adds an operation to the operation queue with priority, key, and cancellation support.</summary>
        /// <typeparam name="T">The type of value returned by the operation.</typeparam>
        /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
        /// <param name="key">A key to apply to the operation. Items with the same key will be run in order. Pass null for non-keyed operations.</param>
        /// <param name="asyncOperation">The async method to execute when scheduled.</param>
        /// <param name="token">A cancellation token which if signalled, will cancel the operation.</param>
        /// <returns>A task that completes when the operation completes, containing the result.</returns>
        /// <exception cref="ArgumentExceptionHelper">Thrown when <paramref name="operationQueue"/> or <paramref name="asyncOperation"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via <paramref name="token"/>.</exception>
        public Task<T> Enqueue<T>(int priority, string key, Func<Task<T>> asyncOperation, CancellationToken token)
        {
            ArgumentExceptionHelper.ThrowIfNull(operationQueue);
            ArgumentExceptionHelper.ThrowIfNull(asyncOperation);

            // Fast path: if token can't be cancelled, avoid registration overhead.
            if (!token.CanBeCanceled)
            {
                return operationQueue.EnqueueObservableOperation(priority, key, Signal.Silent<Unit>(), () => Signal.FromTask(asyncOperation()))
                    .ToTask(CancellationToken.None);
            }

            // Fast path: if token is already canceled, return immediately without enqueueing
            if (token.IsCancellationRequested)
            {
                return Task.FromCanceled<T>(token);
            }

            return operationQueue.EnqueueObservableOperation(priority, key, ConvertTokenToObservable(token), () => Signal.FromTask(asyncOperation()))
                .ToTask(token);
        }

        /// <summary>Adds an operation to the operation queue with priority, key, and cancellation support.</summary>
        /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
        /// <param name="key">A key to apply to the operation. Items with the same key will be run in order. Pass null for non-keyed operations.</param>
        /// <param name="asyncOperation">The async method to execute when scheduled.</param>
        /// <param name="token">A cancellation token which if signalled, will cancel the operation.</param>
        /// <returns>A task that completes when the operation completes.</returns>
        /// <exception cref="ArgumentExceptionHelper">Thrown when <paramref name="operationQueue"/> or <paramref name="asyncOperation"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via <paramref name="token"/>.</exception>
        public Task Enqueue(int priority, string key, Func<Task> asyncOperation, CancellationToken token)
        {
            ArgumentExceptionHelper.ThrowIfNull(operationQueue);
            ArgumentExceptionHelper.ThrowIfNull(asyncOperation);

            // Fast path: if token can't be cancelled, avoid registration overhead.
            if (!token.CanBeCanceled)
            {
                return operationQueue.EnqueueObservableOperation(priority, key, Signal.Silent<Unit>(), () => Signal.FromTask(ToRxVoidTask(asyncOperation)))
                    .ToTask(CancellationToken.None);
            }

            // Fast path: if token is already canceled, return immediately without enqueueing
            if (token.IsCancellationRequested)
            {
                return Task.FromCanceled(token);
            }

            return operationQueue.EnqueueObservableOperation(priority, key, ConvertTokenToObservable(token), () => Signal.FromTask(ToRxVoidTask(asyncOperation)))
                .ToTask(token);
        }

        /// <summary>Adds an operation to the operation queue with priority and key.</summary>
        /// <typeparam name="T">The type of value returned by the operation.</typeparam>
        /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
        /// <param name="key">A key to apply to the operation. Items with the same key will be run in order. Pass null for non-keyed operations.</param>
        /// <param name="asyncOperation">The async method to execute when scheduled.</param>
        /// <returns>A task that completes when the operation completes, containing the result.</returns>
        /// <exception cref="ArgumentExceptionHelper">Thrown when <paramref name="operationQueue"/> or <paramref name="asyncOperation"/> is null.</exception>
        public Task<T> Enqueue<T>(int priority, string key, Func<Task<T>> asyncOperation)
        {
            ArgumentExceptionHelper.ThrowIfNull(operationQueue);
            ArgumentExceptionHelper.ThrowIfNull(asyncOperation);

            return operationQueue.EnqueueObservableOperation(priority, key, Signal.Silent<Unit>(), () => Signal.FromTask(asyncOperation()))
                .ToTask();
        }

        /// <summary>Adds an operation to the operation queue with priority and key.</summary>
        /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
        /// <param name="key">A key to apply to the operation. Items with the same key will be run in order. Pass null for non-keyed operations.</param>
        /// <param name="asyncOperation">The async method to execute when scheduled.</param>
        /// <returns>A task that completes when the operation completes.</returns>
        /// <exception cref="ArgumentExceptionHelper">Thrown when <paramref name="operationQueue"/> or <paramref name="asyncOperation"/> is null.</exception>
        public Task Enqueue(int priority, string key, Func<Task> asyncOperation)
        {
            ArgumentExceptionHelper.ThrowIfNull(operationQueue);
            ArgumentExceptionHelper.ThrowIfNull(asyncOperation);

            return operationQueue.EnqueueObservableOperation(priority, key, Signal.Silent<Unit>(), () => Signal.FromTask(ToRxVoidTask(asyncOperation)))
                .ToTask();
        }

        /// <summary>
        /// Adds a non-keyed operation to the operation queue with priority.
        /// Non-keyed operations can run concurrently with other non-keyed operations.
        /// </summary>
        /// <typeparam name="T">The type of value returned by the operation.</typeparam>
        /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
        /// <param name="asyncOperation">The async method to execute when scheduled.</param>
        /// <returns>A task that completes when the operation completes, containing the result.</returns>
        /// <exception cref="ArgumentExceptionHelper">Thrown when <paramref name="operationQueue"/> or <paramref name="asyncOperation"/> is null.</exception>
        public Task<T> Enqueue<T>(int priority, Func<Task<T>> asyncOperation)
        {
            ArgumentExceptionHelper.ThrowIfNull(operationQueue);
            ArgumentExceptionHelper.ThrowIfNull(asyncOperation);

            return operationQueue.EnqueueObservableOperation(priority, () => Signal.FromTask(asyncOperation()))
                .ToTask();
        }

        /// <summary>
        /// Adds a non-keyed operation to the operation queue with priority.
        /// Non-keyed operations can run concurrently with other non-keyed operations.
        /// </summary>
        /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
        /// <param name="asyncOperation">The async method to execute when scheduled.</param>
        /// <returns>A task that completes when the operation completes.</returns>
        /// <exception cref="ArgumentExceptionHelper">Thrown when <paramref name="operationQueue"/> or <paramref name="asyncOperation"/> is null.</exception>
        public Task Enqueue(int priority, Func<Task> asyncOperation)
        {
            ArgumentExceptionHelper.ThrowIfNull(operationQueue);
            ArgumentExceptionHelper.ThrowIfNull(asyncOperation);

            return operationQueue.EnqueueObservableOperation(priority, () => Signal.FromTask(ToRxVoidTask(asyncOperation)))
                .ToTask();
        }
    }

    /// <summary>Converts a <see cref="CancellationToken"/> to an observable that signals when cancellation is requested.</summary>
    /// <param name="token">The cancellation token to convert.</param>
    /// <returns>
    /// An observable that emits <see cref="Unit.Default"/> when the token is cancelled.
    /// For non-cancellable tokens, returns an observable that never completes (fast path).
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown immediately if the token is already cancelled.</exception>
#if NET8_0_OR_GREATER
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
#endif
    internal static IObservable<Unit> ConvertTokenToObservable(CancellationToken token) =>
        ConvertTokenToObservable(null, token);

    /// <summary>
    /// Converts a <see cref="CancellationToken"/> to an observable that signals when cancellation is requested.
    /// This overload accepts a scheduler for testing purposes.
    /// </summary>
    /// <param name="scheduler">Optional sequencer for subscription. Used for testing temporal behavior.</param>
    /// <param name="token">The cancellation token to convert.</param>
    /// <returns>
    /// An observable that emits <see cref="Unit.Default"/> when the token is cancelled.
    /// For non-cancellable tokens, returns an observable that never completes (fast path).
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown immediately if the token is already cancelled.</exception>
#if NET8_0_OR_GREATER
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
#endif
    internal static IObservable<Unit> ConvertTokenToObservable(IScheduler? scheduler, CancellationToken token)
    {
        // Fast path: non-cancellable tokens never cancel, so return never-completing observable
        if (!token.CanBeCanceled)
        {
            return Signal.Silent<Unit>();
        }

        // Fast path: already cancelled tokens throw immediately
        if (token.IsCancellationRequested)
        {
            return Signal.Fail<Unit>(new OperationCanceledException(token));
        }

        // Standard path: create observable that signals on cancellation
        var obs = Signal.Create<Unit>(observer =>
        {
            // Double-check cancellation after observable creation
            if (token.IsCancellationRequested)
            {
                observer.OnError(new OperationCanceledException(token));
                return Disposable.Empty;
            }

            return token.Register(() =>
            {
                observer.OnNext(Unit.Default);
                observer.OnCompleted();
            });
        });

        return scheduler is not null ? obs.SubscribeOn(scheduler) : obs;
    }

    /// <summary>Converts a non-generic task operation into a task that emits <see cref="Unit"/>.</summary>
    /// <param name="asyncOperation">The async operation to execute.</param>
    /// <returns>A task that produces <see cref="Unit.Default"/> after completion.</returns>
    private static async Task<Unit> ToRxVoidTask(Func<Task> asyncOperation)
    {
        await asyncOperation().ConfigureAwait(false);
        return Unit.Default;
    }
}
