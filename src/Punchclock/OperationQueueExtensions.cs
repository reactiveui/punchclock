// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace Punchclock;

/// <summary>
/// Extension methods associated with the <see cref="OperationQueue"/>.
/// </summary>
public static class OperationQueueExtensions
{
    /// <summary>
    /// Adds a operation to the operation queue.
    /// </summary>
    /// <typeparam name="T">The type of item contained within our observable.</typeparam>
    /// <param name="operationQueue">The operation queue to add our operation to.</param>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="key">A key to apply to the operation. Items with the same key will be run in order.</param>
    /// <param name="asyncOperation">The async method to execute when scheduled.</param>
    /// <param name="token">A cancellation token which if signalled, the operation will be cancelled.</param>
    /// <returns>A task to monitor the progress.</returns>
    public static Task<T> Enqueue<T>(this OperationQueue operationQueue, int priority, string key, Func<Task<T>> asyncOperation, CancellationToken token)
    {
        if (operationQueue == null)
        {
            throw new ArgumentNullException(nameof(operationQueue));
        }

        return operationQueue.EnqueueObservableOperation(priority, key, ConvertTokenToObservable(token), () => asyncOperation().ToObservable())
            .ToTask(token);
    }

    /// <summary>
    /// Adds a operation to the operation queue.
    /// </summary>
    /// <param name="operationQueue">The operation queue to add our operation to.</param>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="key">A key to apply to the operation. Items with the same key will be run in order.</param>
    /// <param name="asyncOperation">The async method to execute when scheduled.</param>
    /// <param name="token">A cancellation token which if signalled, the operation will be cancelled.</param>
    /// <returns>A task to monitor the progress.</returns>
    public static Task Enqueue(this OperationQueue operationQueue, int priority, string key, Func<Task> asyncOperation, CancellationToken token)
    {
        if (operationQueue == null)
        {
            throw new ArgumentNullException(nameof(operationQueue));
        }

        return operationQueue.EnqueueObservableOperation(priority, key, ConvertTokenToObservable(token), () => asyncOperation().ToObservable())
            .ToTask(token);
    }

    /// <summary>
    /// Adds a operation to the operation queue.
    /// </summary>
    /// <typeparam name="T">The type of item contained within our observable.</typeparam>
    /// <param name="operationQueue">The operation queue to add our operation to.</param>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="key">A key to apply to the operation. Items with the same key will be run in order.</param>
    /// <param name="asyncOperation">The async method to execute when scheduled.</param>
    /// <returns>A task to monitor the progress.</returns>
    public static Task<T> Enqueue<T>(this OperationQueue operationQueue, int priority, string key, Func<Task<T>> asyncOperation)
    {
        if (operationQueue == null)
        {
            throw new ArgumentNullException(nameof(operationQueue));
        }

        return operationQueue.EnqueueObservableOperation(priority, key, Observable.Never<Unit>(), () => asyncOperation().ToObservable())
            .ToTask();
    }

    /// <summary>
    /// Adds a operation to the operation queue.
    /// </summary>
    /// <param name="operationQueue">The operation queue to add our operation to.</param>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="key">A key to apply to the operation. Items with the same key will be run in order.</param>
    /// <param name="asyncOperation">The async method to execute when scheduled.</param>
    /// <returns>A task to monitor the progress.</returns>
    public static Task Enqueue(this OperationQueue operationQueue, int priority, string key, Func<Task> asyncOperation)
    {
        if (operationQueue == null)
        {
            throw new ArgumentNullException(nameof(operationQueue));
        }

        return operationQueue.EnqueueObservableOperation(priority, key, Observable.Never<Unit>(), () => asyncOperation().ToObservable())
            .ToTask();
    }

    /// <summary>
    /// Adds a operation to the operation queue.
    /// </summary>
    /// <typeparam name="T">The type of item contained within our observable.</typeparam>
    /// <param name="operationQueue">The operation queue to add our operation to.</param>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="asyncOperation">The async method to execute when scheduled.</param>
    /// <returns>A task to monitor the progress.</returns>
    public static Task<T> Enqueue<T>(this OperationQueue operationQueue, int priority, Func<Task<T>> asyncOperation)
    {
        if (operationQueue == null)
        {
            throw new ArgumentNullException(nameof(operationQueue));
        }

        return operationQueue.EnqueueObservableOperation(priority, () => asyncOperation().ToObservable())
            .ToTask();
    }

    /// <summary>
    /// Adds a operation to the operation queue.
    /// </summary>
    /// <param name="operationQueue">The operation queue to add our operation to.</param>
    /// <param name="priority">The priority of operation. Higher priorities run before lower ones.</param>
    /// <param name="asyncOperation">The async method to execute when scheduled.</param>
    /// <returns>A task to monitor the progress.</returns>
    public static Task Enqueue(this OperationQueue operationQueue, int priority, Func<Task> asyncOperation)
    {
        if (operationQueue == null)
        {
            throw new ArgumentNullException(nameof(operationQueue));
        }

        return operationQueue.EnqueueObservableOperation(priority, () => asyncOperation().ToObservable())
            .ToTask();
    }

    private static IObservable<Unit> ConvertTokenToObservable(CancellationToken token) =>
        Observable.Create<Unit>(observer =>
        {
            if (token.IsCancellationRequested)
            {
                observer.OnError(new ArgumentException("Token is already cancelled", nameof(token)));
                return Disposable.Empty;
            }

            return token.Register(() =>
            {
                observer.OnNext(Unit.Default);
                observer.OnCompleted();
            });
        });
}
