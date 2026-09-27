using NUnit.Framework;

namespace HalfAware.Tests
{
    /// <summary>場面 7（自室・気づき）の対象の並びと前提（シナリオ設計 11 節）</summary>
    public sealed class NoticeIdsTests
    {
        // 調べられるのは四つだけ。寄り道させない
        [Test]
        public void OnlyFourThingsCanBeExamined()
        {
            CollectionAssert.AreEqual(new[] { "log", "jack", "coat", "door" }, NoticeIds.Order);
            CollectionAssert.AllItemsAreUnique(NoticeIds.Order);
        }

        // ログ → ジャック → ジャケット → ドアの順。一つ前が済むまで次は選べない
        [Test]
        public void EachWaitsForTheOneBefore()
        {
            Assert.That(NoticeIds.After(NoticeIds.Log), Is.Empty);
            for (var i = 1; i < NoticeIds.Order.Length; i++)
                CollectionAssert.AreEqual(new[] { NoticeIds.Order[i - 1] }, NoticeIds.After(NoticeIds.Order[i]), NoticeIds.Order[i]);
        }

        // 抜くと立てる。抜く側（JackPull）の既定の id と同じ綴り
        [Test]
        public void PullingTheJackLetsHerStand()
        {
            Assert.AreEqual(NoticeIds.Jack, NoticeIds.StandAfter);
            Assert.AreEqual(RoomIds.Jack, NoticeIds.Jack);
        }

        // 場面 7 は自室。コンソールの頭の行と、セーブの場面の番号
        [Test]
        public void TheSceneIsStageSevenInTheRoom()
        {
            Assert.AreEqual(7, StageMap.StageOf("Notice", ""));
            Assert.AreEqual("Notice", StageMap.SceneOf(7));
            StringAssert.Contains("自室", ConsolePlace.For("Notice"));
            StringAssert.Contains("自室", ConsolePlace.ForStage(7));
        }

        // 同じ場面に並ぶ抜く側（JackPull）と、記憶する口の鍵が重ならない
        [Test]
        public void TheDirectorKeepsItsOwnKey()
        {
            var go = new UnityEngine.GameObject("notice");
            try
            {
                var director = go.AddComponent<NoticeDirector>();
                var pull = go.AddComponent<JackPull>();
                Assert.AreEqual("notice.steps", director.MemoryKey);
                Assert.AreNotEqual(pull.MemoryKey, director.MemoryKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
