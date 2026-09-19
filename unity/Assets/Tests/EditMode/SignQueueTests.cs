using NUnit.Framework;

namespace HalfAware.Tests
{
    public class SignQueueTests
    {
        static SignQueue Three()
        {
            return new SignQueue(new[]
            {
                new[] { "一" },
                new[] { "二" },
                new[] { "三", "三の続き" },
            });
        }

        [Test]
        public void RunsInOrderWhicheverSignYouRead()
        {
            var q = Three();
            Assert.That(q.Next(), Is.EqualTo(new[] { "一" }));
            Assert.That(q.Next(), Is.EqualTo(new[] { "二" }));
            Assert.That(q.Next(), Is.EqualTo(new[] { "三", "三の続き" }));
        }

        [Test]
        public void CountsWhatHasBeenRead()
        {
            var q = Three();
            Assert.That(q.At, Is.EqualTo(0));
            Assert.That(q.Read, Is.False);
            q.Next();
            Assert.That(q.At, Is.EqualTo(1));
            q.Next();
            q.Next();
            Assert.That(q.Read, Is.True);
        }

        [Test]
        public void KeepsTheLastPageInsteadOfFallingSilent()
        {
            var q = Three();
            q.Next();
            q.Next();
            var last = q.Next();
            Assert.That(q.Next(), Is.EqualTo(last));
            Assert.That(q.Next(), Is.EqualTo(last));
        }

        [Test]
        public void EmptyQueueSaysNothing()
        {
            var q = new SignQueue(new string[0][]);
            Assert.That(q.Count, Is.EqualTo(0));
            Assert.That(q.Next(), Is.Empty);
            Assert.That(q.Read, Is.True);
        }

        [Test]
        public void RewindStartsOver()
        {
            var q = Three();
            q.Next();
            q.Next();
            q.Rewind();
            Assert.That(q.Next(), Is.EqualTo(new[] { "一" }));
        }

        [Test]
        public void SignIdsAreToldApartFromTheRest()
        {
            Assert.That(AlleyIds.IsSign(AlleyIds.Sign(0)), Is.True);
            Assert.That(AlleyIds.IsSign(AlleyIds.Sign(4)), Is.True);
            Assert.That(AlleyIds.IsSign(AlleyIds.Board), Is.False);
            Assert.That(AlleyIds.IsSign(AlleyIds.Table), Is.False);
            Assert.That(AlleyIds.IsSign(AlleyIds.StallSign), Is.False);
            Assert.That(AlleyIds.IsSign(null), Is.False);
        }

        [Test]
        public void SignAndPageAreDifferentIds()
        {
            Assert.That(AlleyIds.Sign(0), Is.Not.EqualTo(AlleyIds.Page(0)));
        }
    }
}
