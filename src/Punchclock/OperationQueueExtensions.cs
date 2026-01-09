// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Punchclock;

/// <summary>
/// Extension methods associated with the <see cref="OperationQueue"/>.
/// Provides convenient Task-based overloads for enqueueing operations.
/// </summary>
public static class OperationQueueExtensions
{
    /// <summary>
    /// Adds an operation to the operation queue with priority, key, and cancellation support.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the operation.</typeparam>
    /// <param name="operationQueue">The operation queue to add the operation to.</param>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="key">A key to apply to the operation. Items with the same key will be run in order. Pass null for non-keyed operations.</param>
    /// <param name="asyncOperation">The async method to execute when scheduled.</param>
    /// <param name="token">A cancellation token which if signalled, will cancel the operation.</param>
    /// <returns>A task that completes when the operation completes, containing the result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operationQueue"/> or <paramref name="asyncOperation"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via <paramref name="token"/>.</exception>
    public static Task<T> Enqueue<T>(this OperationQueue operationQueue, int priority, string key, Func<Task<T>> asyncOperation, CancellationToken token)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(operationQueue);
        ArgumentNullException.ThrowIfNull(asyncOperation);
#else
        if (operationQueue is null)
        {
            throw new ArgumentNullException(nameof(operationQueue));
        }

        if (asyncOperation is null)
        {
            throw new ArgumentNullException(nameof(asyncOperation));
        }
#endif

        // Fast path: if token can't be cancelled, use the no-token overload to avoid registration overhead
        if (!token.CanBeCanceled)
        {
            return Enqueue(operationQueue, priority, key, asyncOperation);
        }

        // Fast path: if token is already canceled, return immediately without enqueueing
        if (token.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(token);
        }

        return operationQueue.EnqueueObservableOperation(priority, key, ConvertTokenToObservable(token), () => asyncOperation().ToObservable())
            .ToTask(token);
    }

    /// <summary>
    /// Adds an operation to the operation queue with priority, key, and cancellation support.
    /// </summary>
    /// <param name="operationQueue">The operation queue to add the operation to.</param>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="key">A key to apply to the operation. Items with the same key will be run in order. Pass null for non-keyed operations.</param>
    /// <param name="asyncOperation">The async method to execute when scheduled.</param>
    /// <param name="token">A cancellation token which if signalled, will cancel the operation.</param>
    /// <returns>A task that completes when the operation completes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operationQueue"/> or <paramref name="asyncOperation"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via <paramref name="token"/>.</exception>
    public static Task Enqueue(this OperationQueue operationQueue, int priority, string key, Func<Task> asyncOperation, CancellationToken token)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(operationQueue);
        ArgumentNullException.ThrowIfNull(asyncOperation);
#else
        if (operationQueue is null)
        {
            throw new ArgumentNullException(nameof(operationQueue));
        }

        if (asyncOperation is null)
        {
            throw new ArgumentNullException(nameof(asyncOperation));
        }
#endif

        // Fast path: if token can't be cancelled, use the no-token overload to avoid registration overhead
        if (!token.CanBeCanceled)
        {
            return Enqueue(operationQueue, priority, key, asyncOperation);
        }

        // Fast path: if token is already canceled, return immediately without enqueueing
        if (token.IsCancellationRequested)
        {
            return Task.FromCanceled(token);
        }

        return operationQueue.EnqueueObservableOperation(priority, key, ConvertTokenToObservable(token), () => asyncOperation().ToObservable())
            .ToTask(token);
    }

    /// <summary>
    /// Adds an operation to the operation queue with priority and key.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the operation.</typeparam>
    /// <param name="operationQueue">The operation queue to add the operation to.</param>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="key">A key to apply to the operation. Items with the same key will be run in order. Pass null for non-keyed operations.</param>
    /// <param name="asyncOperation">The async method to execute when scheduled.</param>
    /// <returns>A task that completes when the operation completes, containing the result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operationQueue"/> or <paramref name="asyncOperation"/> is null.</exception>
    public static Task<T> Enqueue<T>(this OperationQueue operationQueue, int priority, string key, Func<Task<T>> asyncOperation)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(operationQueue);
        ArgumentNullException.ThrowIfNull(asyncOperation);
#else
        if (operationQueue is null)
        {
            throw new ArgumentNullException(nameof(operationQueue));
        }

        if (asyncOperation is null)
        {
            throw new ArgumentNullException(nameof(asyncOperation));
        }
#endif

        return operationQueue.EnqueueObservableOperation(priority, key, Observable.Never<Unit>(), () => asyncOperation().ToObservable())
            .ToTask();
    }

    /// <summary>
    /// Adds an operation to the operation queue with priority and key.
    /// </summary>
    /// <param name="operationQueue">The operation queue to add the operation to.</param>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="key">A key to apply to the operation. Items with the same key will be run in order. Pass null for non-keyed operations.</param>
    /// <param name="asyncOperation">The async method to execute when scheduled.</param>
    /// <returns>A task that completes when the operation completes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operationQueue"/> or <paramref name="asyncOperation"/> is null.</exception>
    public static Task Enqueue(this OperationQueue operationQueue, int priority, string key, Func<Task> asyncOperation)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(operationQueue);
        ArgumentNullException.ThrowIfNull(asyncOperation);
#else
        if (operationQueue is null)
        {
            throw new ArgumentNullException(nameof(operationQueue));
        }

        if (asyncOperation is null)
        {
            throw new ArgumentNullException(nameof(asyncOperation));
        }
#endif

        return operationQueue.EnqueueObservableOperation(priority, key, Observable.Never<Unit>(), () => asyncOperation().ToObservable())
            .ToTask();
    }

    /// <summary>
    /// Adds a non-keyed operation to the operation queue with priority.
    /// Non-keyed operations can run concurrently with other non-keyed operations.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the operation.</typeparam>
    /// <param name="operationQueue">The operation queue to add the operation to.</param>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="asyncOperation">The async method to execute when scheduled.</param>
    /// <returns>A task that completes when the operation completes, containing the result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operationQueue"/> or <paramref name="asyncOperation"/> is null.</exception>
    public static Task<T> Enqueue<T>(this OperationQueue operationQueue, int priority, Func<Task<T>> asyncOperation)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(operationQueue);
        ArgumentNullException.ThrowIfNull(asyncOperation);
#else
        if (operationQueue is null)
        {
            throw new ArgumentNullException(nameof(operationQueue));
        }

        if (asyncOperation is null)
        {
            throw new ArgumentNullException(nameof(asyncOperation));
        }
#endif

        return operationQueue.EnqueueObservableOperation(priority, () => asyncOperation().ToObservable())
            .ToTask();
    }

    /// <summary>
    /// Adds a non-keyed operation to the operation queue with priority.
    /// Non-keyed operations can run concurrently with other non-keyed operations.
    /// </summary>
    /// <param name="operationQueue">The operation queue to add the operation to.</param>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="asyncOperation">The async method to execute when scheduled.</param>
    /// <returns>A task that completes when the operation completes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operationQueue"/> or <paramref name="asyncOperation"/> is null.</exception>
    public static Task Enqueue(this OperationQueue operationQueue, int priority, Func<Task> asyncOperation)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(operationQueue);
        ArgumentNullException.ThrowIfNull(asyncOperation);
#else
        if (operationQueue is null)
        {
            throw new ArgumentNullException(nameof(operationQueue));
        }

        if (asyncOperation is null)
        {
            throw new ArgumentNullException(nameof(asyncOperation));
        }
#endif

        return operationQueue.EnqueueObservableOperation(priority, () => asyncOperation().ToObservable())
            .ToTask();
    }

    /// <summary>
    /// Converts a <see cref="CancellationToken"/> to an observable that signals when cancellation is requested.
    /// </summary>
    /// <param name="token">The cancellation token to convert.</param>
    /// <returns>
    /// An observable that emits <see cref="Unit.Default"/> when the token is cancelled.
    /// For non-cancellable tokens, returns an observable that never completes (fast path).
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown immediately if the token is already cancelled.</exception>
#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    internal static IObservable<Unit> ConvertTokenToObservable(CancellationToken token) =>
        ConvertTokenToObservable(null, token);

    /// <summary>
    /// Converts a <see cref="CancellationToken"/> to an observable that signals when cancellation is requested.
    /// This overload accepts a scheduler for testing purposes.
    /// </summary>
    /// <param name="scheduler">Optional scheduler for subscription. Used for testing temporal behavior.</param>
    /// <param name="token">The cancellation token to convert.</param>
    /// <returns>
    /// An observable that emits <see cref="Unit.Default"/> when the token is cancelled.
    /// For non-cancellable tokens, returns an observable that never completes (fast path).
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown immediately if the token is already cancelled.</exception>
#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    internal static IObservable<Unit> ConvertTokenToObservable(IScheduler? scheduler, CancellationToken token)
    {
        // Fast path: non-cancellable tokens never cancel, so return never-completing observable
        if (!token.CanBeCanceled)
        {
            return Observable.Never<Unit>();
        }

        // Fast path: already cancelled tokens throw immediately
        if (token.IsCancellationRequested)
        {
            return Observable.Throw<Unit>(new OperationCanceledException(token));
        }

        // Standard path: create observable that signals on cancellation
        var obs = Observable.Create<Unit>(observer =>
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

        return scheduler != null ? obs.SubscribeOn(scheduler) : obs;
    }
}
