using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Xunit;

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

        [Fact]
        public void KeyedItemsShouldBeSerialized()
        {
            var subj1 = new AsyncSubject<int>();
            var subj2 = new AsyncSubject<int>();

            var subscribeCount1 = 0;
            var input1Subj = new AsyncSubject<int>();
            var input1 = Observable.Defer(() => {
                subscribeCount1++;
                return input1Subj;
            });
            var subscribeCount2 = 0;
            var input2Subj = new AsyncSubject<int>();
            var input2 = Observable.Defer(() => {
                subscribeCount2++;
                return input2Subj;
            });

            var fixture = new OperationQueue(2);

            // Block up the queue
            foreach (var v in new[] { subj1, subj2, }) {
                fixture.EnqueueObservableOperation(5, () => v);
            }

            // subj1,2 are live, input1,2 are in queue
            var out1 = fixture.EnqueueObservableOperation(5, "key", () => input1).CreateCollection();
            var out2 = fixture.EnqueueObservableOperation(5, "key", () => input2).CreateCollection();
            Assert.Equal(0, subscribeCount1);
            Assert.Equal(0, subscribeCount2);

            // Dispatch both subj1 and subj2, we should end up with input1 live, 
            // but input2 in queue because of the key
            subj1.OnNext(42); subj1.OnCompleted();
            subj2.OnNext(42); subj2.OnCompleted();
            Assert.Equal(1, subscribeCount1);
            Assert.Equal(0, subscribeCount2);
            Assert.Equal(0, out1.Count);
            Assert.Equal(0, out2.Count);

            // Dispatch input1, input2 can now execute
            input1Subj.OnNext(42); input1Subj.OnCompleted();
            Assert.Equal(1, subscribeCount1);
            Assert.Equal(1, subscribeCount2);
            Assert.Equal(1, out1.Count);
            Assert.Equal(0, out2.Count);

            // Dispatch input2, everything is finished
            input2Subj.OnNext(42); input2Subj.OnCompleted();
            Assert.Equal(1, subscribeCount1);
            Assert.Equal(1, subscribeCount2);
            Assert.Equal(1, out1.Count);
            Assert.Equal(1, out2.Count);
        }


        [Fact]
        public void NonkeyedItemsShouldRunInParallel()
        {
            Assert.False(true);
        }

        [Fact]
        public void ShutdownShouldSignalOnceEverythingCompletes()
        {
            Assert.False(true);
        }
    }
}
