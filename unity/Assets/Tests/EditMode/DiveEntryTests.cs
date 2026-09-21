using NUnit.Framework;

namespace HalfAware.Tests
{
    public sealed class DiveEntryTests
    {
        static DiveEntry[] TwoEntries()
        {
            return new[]
            {
                new DiveEntry { seen = new[] { new Seen { name = "A", target = 1 } } },
                new DiveEntry { seen = new[] { new Seen { name = "B", target = 0 } } },
            };
        }

        [Test]
        public void ClosedWhenEveryTargetIsInRange()
        {
            Assert.IsTrue(DiveRoster.Closed(TwoEntries()));
        }

        [Test]
        public void NotClosedWhenATargetIsNegative()
        {
            var all = TwoEntries();
            all[0].seen[0].target = -1;
            Assert.IsFalse(DiveRoster.Closed(all));
        }

        [Test]
        public void NotClosedWhenATargetIsPastTheEndOfTheList()
        {
            var all = TwoEntries();
            all[1].seen[0].target = 2;
            Assert.IsFalse(DiveRoster.Closed(all));
        }

        [Test]
        public void ANullSeenDoesNotFail()
        {
            var all = new[] { new DiveEntry { seen = null } };
            Assert.IsTrue(DiveRoster.Closed(all));
        }
    }
}
