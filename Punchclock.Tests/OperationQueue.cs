using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Xunit;
using System.Reactive.Linq;

namespace Punchclock.Tests
{
    public class OperationQueueTests
    {
        [Fact]
        public void ItemsShouldBeDispatchedByPriority()
        {
            var subjects = Enumerable.Range(0, 5).Select(x => new AsyncSubject<int>()).ToArray();
            var priorities = new[] {5,5,5,10,1,};
            var fixture = new OperationQueue(2);

            // The two at the front are solely to stop up the queue, they get subscribed 
            // to immediately.
            var outputs = subjects.Zip(priorities,
                (inp, pri) => fixture.EnqueueObservableOperation(pri, () => inp).CreateCollection()) 
                .ToArray();

            // Alright, we've got the first two subjects taking up our two live 
            // slots, and 3,4,5 queued up. However, the order of completion should 
            // be "4,3,5" because of the priority.
            Assert.True(outputs.All(x => x.Count == 0));

            subjects[0].OnNext(42); subjects[0].OnCompleted();
            Assert.Equal(new[] { 1, 0, 0, 0, 0, }, outputs.Select(x => x.Count));

            // 0 => completed, 1,3 => live, 2,4 => queued. Make sure 4 *doesn't* fire because 
            // the priority should invert it.
            subjects[4].OnNext(42); subjects[4].OnCompleted();
            Assert.Equal(new[] { 1, 0, 0, 0, 0, }, outputs.Select(x => x.Count));

            // At the end, 0,1 => completed, 3,2 => live, 4 is queued
            subjects[1].OnNext(42); subjects[1].OnCompleted();
            Assert.Equal(new[] { 1, 1, 0, 0, 0, }, outputs.Select(x => x.Count));

            // At the end, 0,1,2,4 => completed, 3 is live (remember, we completed
            // 4 early)
            subjects[2].OnNext(42); subjects[2].OnCompleted();
            Assert.Equal(new[] { 1, 1, 1, 0, 1, }, outputs.Select(x => x.Count));

            subjects[3].OnNext(42); subjects[3].OnCompleted();
            Assert.Equal(new[] { 1, 1, 1, 1, 1, }, outputs.Select(x => x.Count));
        }
    }
}
