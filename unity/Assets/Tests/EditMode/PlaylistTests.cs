using NUnit.Framework;

namespace HalfAware.Tests
{
    public class PlaylistTests
    {
        // 4 曲。ヤードの曲と同じくらいの長さ
        static readonly float[] Four = { 224f, 224f, 224f, 251f };

        [Test]
        public void TheTotalIsTheSumOfTheTracks()
        {
            Assert.AreEqual(923f, Playlist.Total(Four), 1e-3f);
        }

        [Test]
        public void TheStartIsTheFirstTrack()
        {
            int index;
            float time;
            Assert.That(Playlist.Locate(0f, Four, out index, out time), Is.True);
            Assert.AreEqual(0, index);
            Assert.AreEqual(0f, time, 1e-4f);
        }

        [Test]
        public void APositionInsideTheSecondTrack()
        {
            int index;
            float time;
            Playlist.Locate(300f, Four, out index, out time);
            Assert.AreEqual(1, index);
            Assert.AreEqual(76f, time, 1e-3f);
        }

        [Test]
        public void ABoundaryBelongsToTheNextTrack()
        {
            int index;
            float time;
            Playlist.Locate(448f, Four, out index, out time);
            Assert.AreEqual(2, index);
            Assert.AreEqual(0f, time, 1e-3f);
        }

        [Test]
        public void TheLastTrackRunsToTheEnd()
        {
            int index;
            float time;
            Playlist.Locate(922.5f, Four, out index, out time);
            Assert.AreEqual(3, index);
            Assert.AreEqual(250.5f, time, 1e-3f);
        }

        [Test]
        public void PastTheEndWrapsBackToTheFirstTrack()
        {
            int index;
            float time;
            Playlist.Locate(923f + 10f, Four, out index, out time);
            Assert.AreEqual(0, index);
            Assert.AreEqual(10f, time, 1e-3f);
        }

        [Test]
        public void ANegativePositionWrapsFromTheEnd()
        {
            int index;
            float time;
            Playlist.Locate(-1f, Four, out index, out time);
            Assert.AreEqual(3, index);
            Assert.AreEqual(250f, time, 1e-3f);
        }

        [Test]
        public void ATrackThatCouldNotBeReadIsSkipped()
        {
            var lengths = new[] { 224f, 0f, 224f };
            int index;
            float time;
            Playlist.Locate(230f, lengths, out index, out time);
            Assert.AreEqual(2, index, "長さ 0 の曲には入らない");
            Assert.AreEqual(6f, time, 1e-3f);
            Assert.AreEqual(2, Playlist.Next(0, lengths));
        }

        [Test]
        public void NothingToPlayIsReported()
        {
            int index;
            float time;
            Assert.That(Playlist.Locate(10f, new[] { 0f, 0f }, out index, out time), Is.False);
            Assert.AreEqual(-1, index);
            Assert.AreEqual(-1, Playlist.Next(0, new[] { 0f, 0f }));
            Assert.That(Playlist.Locate(10f, new float[0], out index, out time), Is.False);
        }

        [Test]
        public void AfterTheLastTrackComesTheFirst()
        {
            Assert.AreEqual(1, Playlist.Next(0, Four));
            Assert.AreEqual(0, Playlist.Next(3, Four));
        }

        [Test]
        public void ASingleTrackFollowsItself()
        {
            Assert.AreEqual(0, Playlist.Next(0, new[] { 200f }));
        }
    }
}
