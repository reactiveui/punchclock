// Copyright (c) 2025 ReactiveUI and Contributors. All rights reserved.
// Licensed to the ReactiveUI and Contributors under one or more agreements.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace Punchclock;

/// <summary>
/// A subject which emits items using the specified scheduler.
/// Manages default observer fallback when no active subscribers exist.
/// </summary>
/// <typeparam name="T">The type of item to emit.</typeparam>
internal class ScheduledSubject<T> : ISubject<T>, IDisposable
{
    /// <summary>
    /// Synchronization primitive guarding mutations to subscription state.
    /// </summary>
    /// <remarks>
    /// Protects updates to <see cref="_defaultObserverSub"/> and <see cref="_observerRefCount"/>.
    /// </remarks>
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif

    /// <summary>
    /// The default observer to receive items when no other subscribers are active.
    /// </summary>
    private readonly IObserver<T>? _defaultObserver;

    /// <summary>
    /// The scheduler used for emitting items to observers.
    /// </summary>
    private readonly IScheduler _scheduler;

    /// <summary>
    /// The underlying subject that manages the observable stream.
    /// </summary>
    private readonly Subject<T> _subject = new();

    /// <summary>
    /// Reference count of active non-default observers.
    /// </summary>
    private int _observerRefCount;

    /// <summary>
    /// Subscription handle for the default observer. Null when no default observer is active.
    /// </summary>
    private IDisposable? _defaultObserverSub;

    /// <summary>
    /// Tracks whether this instance has been disposed.
    /// </summary>
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledSubject{T}"/> class.
    /// </summary>
    /// <param name="scheduler">The scheduler to emit items on.</param>
    /// <param name="defaultObserver">A default observer which will receive values when no other subscribers are active. Can be null.</param>
    public ScheduledSubject(IScheduler scheduler, IObserver<T>? defaultObserver = null)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(scheduler);
#else
        if (scheduler is null)
        {
            throw new ArgumentNullException(nameof(scheduler));
        }
#endif

        _scheduler = scheduler;
        _defaultObserver = defaultObserver;

        if (defaultObserver != null)
        {
            _defaultObserverSub = _subject.ObserveOn(_scheduler).Subscribe(defaultObserver);
        }
    }

    /// <inheritdoc />
    public void OnCompleted() => _subject.OnCompleted();

    /// <inheritdoc />
    public void OnError(Exception error) => _subject.OnError(error);

    /// <inheritdoc />
    public void OnNext(T value) => _subject.OnNext(value);

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(observer);
#else
        if (observer is null)
        {
            throw new ArgumentNullException(nameof(observer));
        }
#endif

        IDisposable? defaultSubToDispose = null;

        lock (_gate)
        {
            // If we have a default observer subscription active, disable it
            if (_defaultObserverSub != null)
            {
                defaultSubToDispose = _defaultObserverSub;
                _defaultObserverSub = null;
            }

            Interlocked.Increment(ref _observerRefCount);
        }

        // Dispose outside the lock
        defaultSubToDispose?.Dispose();

        return new CompositeDisposable(
            _subject.ObserveOn(_scheduler).Subscribe(observer),
            Disposable.Create(() =>
            {
                var defaultObserver = _defaultObserver;
                if (Interlocked.Decrement(ref _observerRefCount) <= 0 && defaultObserver != null)
                {
                    lock (_gate)
                    {
                        // Re-check inside lock in case another subscription happened
                        if (Volatile.Read(ref _observerRefCount) <= 0 && _defaultObserverSub is null)
                        {
                            _defaultObserverSub = _subject.ObserveOn(_scheduler).Subscribe(defaultObserver);
                        }
                    }
                }
            }));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources that are disposable and handles cleanup of unmanaged items.
    /// </summary>
    /// <param name="disposing">If we are disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            _subject.Dispose();
            _defaultObserverSub?.Dispose();
        }

        _isDisposed = true;
    }
}
