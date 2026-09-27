using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>場面 9 の終わりと場面 10（対面）の、場面の番号・デバッグの一覧・文字の値・段の進み</summary>
    public class ReunionTests
    {
        [TearDown]
        public void Reset()
        {
            ReunionHandoff.Clear();
            GardenHandoff.Clear();
            SaveStore.Box = null;
        }

        // ---- 場面の番号 ------------------------------------------------------------

        [Test]
        public void StageTenIsTheMorningVillage()
        {
            Assert.AreEqual("Village", StageMap.SceneOf(10));
            Assert.AreEqual(StageMap.Morning, StageMap.HourOf(10));
            Assert.IsTrue(StageMap.Playable(10));
            Assert.AreEqual("対面", StageMap.TitleOf(10));
            // 村の朝を読んだだけでは場面 9。場面 10 は卓の前で替わる
            Assert.AreEqual(9, StageMap.StageOf("Village", StageMap.Morning));
            Assert.AreEqual(6, StageMap.StageOf("Village", StageMap.Evening));
        }

        [Test]
        public void TheHandoffIsTakenOnce()
        {
            ReunionHandoff.Pending = true;
            Assert.IsTrue(ReunionHandoff.Take());
            Assert.IsFalse(ReunionHandoff.Take());
        }

        [Test]
        public void TheConsoleSaysTheMorningVillage()
        {
            Assert.AreEqual(ConsolePlace.For("Village"), ConsolePlace.For(SceneMenu.Reunion));
            Assert.AreEqual(ConsolePlace.For("Village"), ConsolePlace.ForStage(10));
        }

        // ---- デバッグの一覧 ---------------------------------------------------------

        [Test]
        public void TheReunionIsTheTenthRowOnTheZeroKey()
        {
            var at = System.Array.IndexOf(SceneMenu.Scenes, SceneMenu.Reunion);
            Assert.AreEqual(9, at, "10 行目");
            Assert.AreEqual("対面", SceneMenu.Titles[at]);
            Assert.AreEqual("0", SceneMenu.KeyOf(at));
            Assert.AreEqual("1", SceneMenu.KeyOf(0));
            // 0 の鍵は 10 行目として渡す
            Assert.AreEqual(SceneMenu.Reunion, SceneMenu.Pick(SceneMenu.Keys));
            Assert.AreEqual("Village", SceneMenu.SceneOf(SceneMenu.Reunion));
        }

        [Test]
        public void TheMenuOpensTheVillageAtTheTable()
        {
            SceneMenu.Prepare(SceneMenu.Reunion);
            Assert.IsTrue(ReunionHandoff.Pending);
            Assert.IsFalse(GardenHandoff.Pending);
            SceneMenu.Prepare(SceneMenu.Garden);
            Assert.IsFalse(ReunionHandoff.Pending);
            Assert.IsTrue(GardenHandoff.Pending);
            SceneMenu.Prepare("Village");
            Assert.IsFalse(ReunionHandoff.Pending);
            Assert.IsFalse(GardenHandoff.Pending);
        }

        [Test]
        public void TheMenuKnowsWhenWeAreFacingTheTwin()
        {
            Assert.AreEqual(SceneMenu.Reunion, SceneMenu.Here("Village", false, true));
            Assert.AreEqual(SceneMenu.Garden, SceneMenu.Here("Village", true, false));
            Assert.AreEqual("Village", SceneMenu.Here("Village", false, false));
            Assert.AreEqual("Room", SceneMenu.Here("Room", false, true));
            // 対面にいる時は対面の行を押しても移らない。村の行は場面 9 の村へ移る
            var reunion = System.Array.IndexOf(SceneMenu.Scenes, SceneMenu.Reunion) + 1;
            var village = System.Array.IndexOf(SceneMenu.Scenes, "Village") + 1;
            Assert.IsNull(SceneMenu.Target(reunion, SceneMenu.Reunion));
            Assert.AreEqual("Village", SceneMenu.Target(village, SceneMenu.Reunion));
        }

        // ---- 文字 -----------------------------------------------------------------

        [Test]
        public void TheVoiceIsTheGardenCallUnbroken()
        {
            var voice = Reunion.Voice;
            Assert.AreEqual("「" + string.Format(GardenMemory.CallSource, GardenMemory.TwinName) + "」", voice);
            StringAssert.Contains(GardenMemory.TwinName, voice);
            // 名前の行は出さない（場面 6 の崩れた一行と同じ帯）
            Assert.AreEqual(string.Empty, Speech.Who(voice));
            // 場面 6 で出した崩れた形とは違う（字が欠けていない）
            Assert.AreNotEqual(GardenMemory.Call, voice);
            Assert.AreEqual(GardenMemory.Call.Length, voice.Length);
        }

        [Test]
        public void TheTwinSpeaksUnderHerName()
        {
            Assert.AreEqual(GardenMemory.TwinName, Speech.Who(Reunion.TwinSays));
            StringAssert.Contains(Reunion.HeroineName, Reunion.TwinSays);
        }

        // ---- 段の進み --------------------------------------------------------------

        [Test]
        public void TheTableZoneIsMeasuredFromAbove()
        {
            var table = new Vector3(-1.8f, 0.1f, 16.9f);
            Assert.IsTrue(ReunionDirector.Within(new Vector3(-3.35f, 0.06f, 16.75f), table, 2.2f));
            // 小路の上（テラスの西の縁の外）では入らない
            Assert.IsFalse(ReunionDirector.Within(new Vector3(-4.2f, 0f, 15.5f), table, 2.2f));
            // 高さは見ない
            Assert.IsTrue(ReunionDirector.Within(new Vector3(-1.8f, 5f, 16.9f), table, 2.2f));
        }

        [Test]
        public void WithoutTheHandoffTheDirectorDoesNotGetInTheWay()
        {
            var go = new GameObject("ReunionDirector");
            try
            {
                var d = go.AddComponent<ReunionDirector>();
                Assert.IsFalse(d.Running);
                Assert.IsTrue(d.Settled);
                Assert.IsNull(d.Capture());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TheReunionRunsFromTheDoorToTheBlackout()
        {
            var box = new MemoryBox();
            SaveStore.Box = box;
            var go = new GameObject("ReunionDirector");
            try
            {
                var d = go.AddComponent<ReunionDirector>();
                d.Begin();
                Assert.AreEqual(ReunionDirector.Beat.Door, d.Current);
                Assert.IsTrue(ReunionHandoff.Active);
                // 場面 10 を流している間は途中の形を残さない（記憶するは場面 10 の頭を書く）
                Assert.IsFalse(d.Settled);
                // 出てきて立ち止まり、口元を覆い、一歩踏み出す。その順に並ぶ
                Assert.Greater(d.GaspAt, d.StopAt);
                Assert.Greater(d.StepAt, d.GaspAt);
                Assert.Greater(d.ReleaseAt, d.StepAt);
                d.Step(d.StepAt, false);
                Assert.AreEqual(ReunionDirector.Beat.Door, d.Current, "口元を覆って一歩踏み出すまでは封じたまま");
                d.Step(d.ReleaseAt, false);
                // 片割れが繋がっていなければ、そのまま頬の段へ
                d.Step(0.1f, false);
                d.Step(0.1f, false);
                Assert.AreEqual(ReunionDirector.Beat.Touch, d.Current);
                d.Step(5f, false);
                Assert.AreEqual(ReunionDirector.Beat.Montage, d.Current);
                // モンタージュは三枚。(a)・(b)・(c)
                Assert.AreEqual(0, d.MontageFrame);
                d.Step(1.3f, false);
                Assert.AreEqual(1, d.MontageFrame);
                d.Step(1.3f, false);
                Assert.AreEqual(2, d.MontageFrame);
                d.Step(1.3f, false);
                Assert.AreEqual(ReunionDirector.Beat.Voice, d.Current);
                // 言葉と独白は E で送るまで残る
                d.Step(10f, false);
                Assert.AreEqual(ReunionDirector.Beat.Voice, d.Current);
                d.Step(0.1f, true);
                Assert.AreEqual(ReunionDirector.Beat.Words, d.Current);
                d.Step(0.1f, true);
                Assert.AreEqual(ReunionDirector.Beat.Last, d.Current);
                d.Step(0.1f, true);
                Assert.AreEqual(ReunionDirector.Beat.Closing, d.Current);
                // 黒へ落として、クリアの印を付ける。タイトルと終わりのクレジットはエンディングの場面が受け持つ
                Assert.Greater(d.EndAt, 0f);
                d.Step(d.EndAt * 0.5f, false);
                Assert.AreEqual(ReunionDirector.Beat.Closing, d.Current);
                Assert.IsFalse(SaveStore.Cleared);
                d.Step(d.EndAt, false);
                Assert.AreEqual(ReunionDirector.Beat.Done, d.Current);
                Assert.IsTrue(d.Cleared);
                Assert.IsTrue(SaveStore.Cleared, "クリアの印");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---- 人の形 ---------------------------------------------------------------

        [Test]
        public void APoseIsHeldOnlyWhileTheDirectorAsks()
        {
            var go = new GameObject("Person");
            try
            {
                var m = go.AddComponent<PersonMotion>();
                Assert.AreEqual(0, m.PoseCount);
                Assert.AreEqual(-1, m.PoseIndex);
                m.Pose(1, 2f);
                Assert.AreEqual(1, m.PoseIndex);
                Assert.AreEqual(1f, m.PoseWeight, 1e-5f);
                m.Pose(-1, 0f);
                Assert.AreEqual(-1, m.PoseIndex);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
