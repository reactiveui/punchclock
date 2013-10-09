using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Punchclock
{
    class PrioritySemaphoreSubject<T> : ISubject<T>
        where T : IComparable<T>
    {
        readonly ISubject<T> _inner;
        PriorityQueue<T> _nextItems = new PriorityQueue<T>();
        int _count;

        int _MaximumCount;
        public int MaximumCount {
            get { return _MaximumCount; }
            set {
                _MaximumCount = value;
                yieldUntilEmptyOrBlocked();
            }
        }

        public PrioritySemaphoreSubject(int maxCount, IScheduler sched = null)
        {
            _inner = (sched != null ? (ISubject<T>)new ScheduledSubject<T>(sched) : new Subject<T>());
            MaximumCount = maxCount;
        }

        public void OnNext(T value)
        {
            var queue = Interlocked.CompareExchange(ref _nextItems, null, null);
            if (queue == null)
                return;

            lock (queue) {
                queue.Enqueue(value);
            }

            yieldUntilEmptyOrBlocked();
        }

        public void Release()
        {
            Interlocked.Decrement(ref _count);
            yieldUntilEmptyOrBlocked();
        }

        public void OnCompleted()
        {
            var queue = Interlocked.Exchange(ref _nextItems, null);
            if (queue == null)
                return;

            T[] items;
            lock (queue) {
                items = queue.DequeueAll();
            }

            foreach (var v in items) {
                _inner.OnNext(v);
            }

            _inner.OnCompleted();
        }

        public void OnError(Exception error)
        {
            var queue = Interlocked.Exchange(ref _nextItems, null);
            _inner.OnError(error);
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            return _inner.Subscribe(observer);
        }

        void yieldUntilEmptyOrBlocked()
        {
            var queue = Interlocked.CompareExchange(ref _nextItems, null, null);

            if (queue == null) {
                return;
            }

            while (_count < MaximumCount) {
                T next;
                lock (queue) {
                    if (queue.Count == 0) {
                        break;
                    }

                    next = queue.Dequeue();
                }

                _inner.OnNext(next);

                if (Interlocked.Increment(ref _count) >= MaximumCount) {
                    break;
                }
            }
        }
    }
}
