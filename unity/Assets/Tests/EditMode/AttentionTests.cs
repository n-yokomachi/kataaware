using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 調べている間、見回しを封じる。封じる間は、その物の字幕・二択・その物が起こした止まり（SceneFlow.StillAttending）。
    /// 封じるのは者ごとに数える（PlayerController.HoldLook）
    /// </summary>
    public class AttentionTests
    {
        [Test]
        public void TheQueueCountsWhatWasQueuedAndWhatWasRead()
        {
            var q = new SubtitleQueue();
            q.Enqueue(new[] { "a", "b" });
            Assert.AreEqual(2, q.Queued);
            Assert.AreEqual(0, q.Passed);
            q.Advance();
            Assert.AreEqual(1, q.Passed);
            q.Advance();
            Assert.AreEqual(2, q.Passed, "最後の行を送れば、積んだぶん全部を読み終えた");
            q.Enqueue(new[] { "c" });
            Assert.AreEqual(3, q.Queued, "空にしても数は減らない");
            q.Clear();
            Assert.AreEqual(3, q.Passed);
        }

        [Test]
        public void HeldWhileTheThingsLinesAreStillShowing()
        {
            Assert.IsTrue(SceneFlow.StillAttending(1, 2, false, 10f, 0f));
            Assert.IsFalse(SceneFlow.StillAttending(2, 2, false, 10f, 0f));
        }

        [Test]
        public void HeldWhileTheChoiceIsUp()
        {
            Assert.IsTrue(SceneFlow.StillAttending(2, 2, true, 10f, 0f));
        }

        [Test]
        public void HeldWhileTheFreezeItCausedLasts()
        {
            Assert.IsTrue(SceneFlow.StillAttending(2, 2, false, 10f, 10.5f));
            Assert.IsFalse(SceneFlow.StillAttending(2, 2, false, 10.5f, 10.5f));
        }

        [Test]
        public void LinesADirectorAddsLaterDoNotKeepTheLookHeld()
        {
            // 調べたその時に積んだ所までを覚える。そのあと演出が積む行（路地裏の買い手の台詞）は数えない
            var q = new SubtitleQueue();
            q.Enqueue(new[] { "調べた文" });
            var mark = q.Queued;
            q.Advance();
            q.Enqueue(new[] { "買い手の台詞", "買い手の台詞 2" });
            Assert.IsTrue(q.IsTalking);
            Assert.IsFalse(SceneFlow.StillAttending(q.Passed, mark, false, 0f, 0f));
        }

        [Test]
        public void EachHolderHasToLetGo()
        {
            var go = new GameObject("AttentionTests.Player");
            try
            {
                var player = go.AddComponent<PlayerController>();
                var flow = new object();
                var gate = new object();
                Assert.IsFalse(player.LookHeld);
                player.HoldLook(flow);
                player.HoldLook(gate);
                player.HoldLook(flow);
                Assert.IsTrue(player.LookHeld);
                player.FreeLook(flow);
                Assert.IsTrue(player.LookHeld, "戸がまだ封じている");
                player.FreeLook(gate);
                Assert.IsFalse(player.LookHeld);
                player.FreeLook(gate);
                player.HoldLook(null);
                Assert.IsFalse(player.LookHeld, "空の者は数えない");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
