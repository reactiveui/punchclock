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
/// A subject which emits using the specified scheduler.
/// </summary>
/// <typeparam name="T">The type of item to emit.</typeparam>
internal class ScheduledSubject<T> : ISubject<T>, IDisposable
{
    private readonly IObserver<T>? _defaultObserver;
    private readonly IScheduler _scheduler;
    private readonly Subject<T> _subject = new();

    private int _observerRefCount;
    private IDisposable? _defaultObserverSub;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledSubject{T}"/> class.
    /// </summary>
    /// <param name="scheduler">The scheduler to emit items on.</param>
    /// <param name="defaultObserver">A default observable which will get values if no other subscribes.</param>
    public ScheduledSubject(IScheduler scheduler, IObserver<T>? defaultObserver = null)
    {
        _scheduler = scheduler;
        _defaultObserver = defaultObserver;

        if (defaultObserver != null)
        {
            _defaultObserverSub = _subject.ObserveOn(_scheduler).Subscribe(_defaultObserver);
        }
    }

    /// <inheritdoc />
    public void OnCompleted() => _subject.OnCompleted();

    /// <inheritdoc />
    public void OnError(Exception error) => _subject.OnError(error);

    /// <inheritdoc />
    public void OnNext(T value) => _subject.OnNext(value);

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T>? observer)
    {
        if (_defaultObserverSub != null)
        {
            _defaultObserverSub.Dispose();
            _defaultObserverSub = null;
        }

        Interlocked.Increment(ref _observerRefCount);

        return new CompositeDisposable(
            _subject.ObserveOn(_scheduler).Subscribe(observer),
            Disposable.Create(() =>
            {
                if (Interlocked.Decrement(ref _observerRefCount) <= 0 && _defaultObserver != null)
                {
                    _defaultObserverSub = _subject.ObserveOn(_scheduler).Subscribe(_defaultObserver);
                }
            }));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources that are disposable and handles cleanup of unmanaged items.
    /// </summary>
    /// <param name="isDisposing">If we are disposing managed resources.</param>
    protected virtual void Dispose(bool isDisposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (isDisposing)
        {
            _subject?.Dispose();
            _defaultObserverSub?.Dispose();
        }

        _isDisposed = true;
    }
}
