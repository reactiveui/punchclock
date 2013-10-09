using System;
using System.Collections.Generic;
using System.Reactive.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive;
using System.Threading;
using System.Reactive.Subjects;

namespace Punchclock
{
    public static class OperationQueueExtensions
    {
        public static Task<T> Enqueue<T>(this OperationQueue This, int priority, string key, CancellationToken token, Func<Task<T>> asyncOperation)
        {
            var cancel = new AsyncSubject<Unit>();

            if (token.IsCancellationRequested) {
                throw new ArgumentException("Token is already cancelled");
            }

            token.Register(() => { cancel.OnNext(Unit.Default); cancel.OnCompleted(); });
            return This.EnqueueObservableOperation(priority, key, cancel, () => asyncOperation().ToObservable()).ToTask(token);
        }

        public static Task<T> Enqueue<T>(this OperationQueue This, int priority, string key, Func<Task<T>> asyncOperation)
        {
            return This.EnqueueObservableOperation(priority, key, Observable.Never<Unit>(), () => asyncOperation().ToObservable()).ToTask();
        }

        public static Task<T> Enqueue<T>(this OperationQueue This, int priority, Func<Task<T>> asyncOperation)
        {
            return This.EnqueueObservableOperation(priority, () => asyncOperation().ToObservable()).ToTask();
        }
    }
}