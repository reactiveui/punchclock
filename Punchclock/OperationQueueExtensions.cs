using System;
using System.Collections.Generic;
using System.Reactive.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive;

namespace Punchclock
{
    public static class OperationQueueExtensions
    {
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