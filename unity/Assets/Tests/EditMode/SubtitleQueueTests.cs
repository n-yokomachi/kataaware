using NUnit.Framework;

namespace HalfAware.Tests
{
    public class SubtitleQueueTests
    {
        [Test]
        public void ShowsTheFirstQueuedLine()
        {
            var q = new SubtitleQueue();
            q.Enqueue(new[] { "一行目", "二行目" });
            Assert.That(q.Current, Is.EqualTo("一行目"));
            Assert.That(q.IsTalking, Is.True);
        }

        [Test]
        public void AdvancesLineByLineAndEmptiesAtTheEnd()
        {
            var q = new SubtitleQueue();
            q.Enqueue(new[] { "一行目", "二行目" });
            q.Advance();
            Assert.That(q.Current, Is.EqualTo("二行目"));
            q.Advance();
            Assert.That(q.Current, Is.Null);
            Assert.That(q.IsTalking, Is.False);
        }

        [Test]
        public void AppendsLinesBehindTheOnesStillWaiting()
        {
            var q = new SubtitleQueue();
            q.Enqueue(new[] { "a" });
            q.Enqueue(new[] { "b" });
            q.Advance();
            Assert.That(q.Current, Is.EqualTo("b"));
        }

        [Test]
        public void IgnoresAnEmptyEnqueue()
        {
            var q = new SubtitleQueue();
            q.Enqueue(new string[0]);
            Assert.That(q.Current, Is.Null);
            Assert.That(q.IsTalking, Is.False);
        }

        [Test]
        public void ClearDropsEverything()
        {
            var q = new SubtitleQueue();
            q.Enqueue(new[] { "a", "b" });
            q.Clear();
            Assert.That(q.Current, Is.Null);
            q.Enqueue(new[] { "c" });
            Assert.That(q.Current, Is.EqualTo("c"));
        }
    }
}
