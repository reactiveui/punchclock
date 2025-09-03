// Copyright (c) 2025 .NET Foundation and Contributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Punchclock;

internal abstract class KeyedOperation : IComparable<KeyedOperation>
{
    public bool CancelledEarly { get; set; }

    public int Priority { get; set; }

    public int Id { get; set; }

    public string? Key { get; set; }

    public IObservable<Unit>? CancelSignal { get; set; }

    public bool KeyIsDefault => string.IsNullOrEmpty(Key) || Key == OperationQueue.DefaultKey;

    public abstract IObservable<Unit> EvaluateFunc();

    public int CompareTo(KeyedOperation other)
    {
        // NB: Non-keyed operations always come before keyed operations in
        // order to make sure that serialized keyed operations don't take
        // up concurrency slots
        if (KeyIsDefault != other.KeyIsDefault)
        {
            // If this is non-keyed (default), it should sort before keyed -> return -1
            return KeyIsDefault ? -1 : 1;
        }

        return other.Priority.CompareTo(Priority);
    }
}

[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Generic implementation of same class name.")]
internal class KeyedOperation<T> : KeyedOperation
{
    public Func<IObservable<T>>? Func { get; set; }

    public ReplaySubject<T> Result { get; } = new ReplaySubject<T>();

    public override IObservable<Unit> EvaluateFunc()
    {
        if (Func == null)
        {
            return Observable.Empty<Unit>();
        }

        if (CancelledEarly)
        {
            return Observable.Empty<Unit>();
        }

        var signal = CancelSignal ?? Observable.Empty<Unit>();
        var ret = Func().TakeUntil(signal).Multicast(Result);
        ret.Connect();

        return ret.Select(_ => Unit.Default);
    }
}
