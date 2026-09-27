using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>場面 6（庭の記憶）の文字の値と、村を夕方の庭として開く印の受け渡し</summary>
    public class GardenMemoryTests
    {
        [TearDown]
        public void Reset()
        {
            GardenHandoff.Clear();
        }

        // ---- 見出しと板 ------------------------------------------------------------

        [Test]
        public void TheCaptionIsTheTwinsRowInTheDiveFormat()
        {
            // 場面 4 の見出しと同じ書式（「女　6　『メイ』　2156/03/02 07:14」）で、片割れ（記憶の主）の欄
            Assert.AreEqual("女　" + GardenMemory.TwinAge + "　『" + GardenMemory.TwinName + "』　" + GardenMemory.Stamp, GardenMemory.Row);
            Assert.AreEqual("2163/08/16 19:45", ConsolePlace.Stamp(GardenMemory.Row));
        }

        [Test]
        public void TheWomansBoardHasAGarbledName()
        {
            StringAssert.Contains("『" + GardenMemory.Garbled + "』", GardenMemory.WomanRow);
            StringAssert.DoesNotContain(GardenMemory.TwinName, GardenMemory.WomanRow);
            // 板は名前の鉤括弧までを出す。日時は右上の見出しが持つ
            Assert.IsTrue(GardenMemory.WomanRow.EndsWith("』"));
        }

        // ---- 名を呼ぶ一行の崩し ----------------------------------------------------

        [Test]
        public void TheCallKeepsItsLengthAndHidesTheName()
        {
            var source = string.Format(GardenMemory.CallSource, GardenMemory.TwinName);
            var garbled = GardenMemory.Garble(source, GardenMemory.TwinName);
            Assert.AreEqual(source.Length, garbled.Length);
            Assert.AreNotEqual(source, garbled);
            var at = source.IndexOf(GardenMemory.TwinName, System.StringComparison.Ordinal);
            for (var i = at; i < at + GardenMemory.TwinName.Length; i++)
                Assert.AreEqual('　', garbled[i], "名の字 " + source[i] + " が残っている");
        }

        [Test]
        public void PunctuationStaysAndTheShapeRepeats()
        {
            var garbled = GardenMemory.Garble("あいうえお、かきくけこ。", "");
            Assert.AreEqual('、', garbled[5]);
            Assert.AreEqual('。', garbled[11]);
            Assert.AreEqual(garbled, GardenMemory.Garble("あいうえお、かきくけこ。", ""));
        }

        [Test]
        public void SomeLettersSurviveSoTheShapeReadsAsSpeech()
        {
            var source = string.Format(GardenMemory.CallSource, GardenMemory.TwinName);
            var garbled = GardenMemory.Garble(source, GardenMemory.TwinName);
            var kept = 0;
            for (var i = 0; i < source.Length; i++)
                if (source[i] == garbled[i] && source[i] != '、' && source[i] != '。') kept++;
            Assert.Greater(kept, 0);
            Assert.Less(kept, source.Length - GardenMemory.TwinName.Length);
        }

        [Test]
        public void TheCallIsSpeechWithoutASpeakerName()
        {
            // 名前の行を出さない。鉤括弧から始まるので、字幕は話し手の名を分けない
            var line = GardenMemory.Call;
            Assert.IsTrue(line.StartsWith("「") && line.EndsWith("」"));
            Assert.AreEqual(string.Empty, Speech.Who(line));
        }

        [Test]
        public void BrokenLettersAreOnlyGivenForKnownShapes()
        {
            Assert.AreEqual("コ", GardenMemory.BrokenOf('ロ'));
            Assert.AreEqual(string.Empty, GardenMemory.BrokenOf('ぬ'));
        }

        // ---- 場面の番号と頭の行 -------------------------------------------------------

        [Test]
        public void TheEveningVillageIsStageSix()
        {
            Assert.AreEqual(6, StageMap.StageOf("Village", StageMap.Evening));
            Assert.AreEqual(9, StageMap.StageOf("Village", StageMap.Morning));
            Assert.AreEqual("Village", StageMap.SceneOf(6));
            Assert.AreEqual(StageMap.Evening, StageMap.HourOf(6));
            Assert.IsTrue(StageMap.Playable(6));
        }

        [Test]
        public void TheConsoleSaysDivingWithTheMemorysDate()
        {
            var line = ConsolePlace.Diving + ConsolePlace.Gap + GardenMemory.Stamp + ConsolePlace.Gap + GardenMemory.Place;
            Assert.AreEqual(line, ConsolePlace.Garden);
            Assert.AreEqual(line, ConsolePlace.ForStage(6));
            Assert.AreEqual(line, ConsolePlace.For(SceneMenu.Garden));
            // 朝の村は朝の村のまま
            Assert.AreNotEqual(line, ConsolePlace.For("Village"));
            Assert.AreEqual(ConsolePlace.For("Village"), ConsolePlace.ForStage(9));
        }

        // ---- 村を夕方の庭として開く印 ---------------------------------------------------

        [Test]
        public void TheHandoffIsTakenOnce()
        {
            GardenHandoff.Pending = true;
            Assert.IsTrue(GardenHandoff.Take());
            Assert.IsFalse(GardenHandoff.Take());
        }

        [Test]
        public void TheMenuOpensTheVillageAsTheGarden()
        {
            Assert.AreEqual("Village", SceneMenu.SceneOf(SceneMenu.Garden));
            Assert.AreEqual("Room", SceneMenu.SceneOf("Room"));
            SceneMenu.Prepare(SceneMenu.Garden);
            Assert.IsTrue(GardenHandoff.Pending);
            SceneMenu.Prepare("Village");
            Assert.IsFalse(GardenHandoff.Pending);
        }

        [Test]
        public void TheMenuKnowsWhichVillageWeAreIn()
        {
            var garden = System.Array.IndexOf(SceneMenu.Scenes, SceneMenu.Garden) + 1;
            var village = System.Array.IndexOf(SceneMenu.Scenes, "Village") + 1;
            Assert.Greater(garden, 0);
            Assert.AreEqual("庭の記憶", SceneMenu.Titles[garden - 1]);
            Assert.AreEqual(SceneMenu.Garden, SceneMenu.Here("Village", true));
            Assert.AreEqual("Village", SceneMenu.Here("Village", false));
            Assert.AreEqual("Room", SceneMenu.Here("Room", true));
            // 庭にいる時は庭の行を押しても移らない。村の行は朝の村へ移る
            Assert.IsNull(SceneMenu.Target(garden, SceneMenu.Here("Village", true)));
            Assert.AreEqual("Village", SceneMenu.Target(village, SceneMenu.Here("Village", true)));
            Assert.AreEqual(SceneMenu.Garden, SceneMenu.Target(garden, SceneMenu.Here("Village", false)));
        }

        // ---- 段の進み --------------------------------------------------------------

        [Test]
        public void TheVillageStaysMorningWithoutTheHandoff()
        {
            var go = new GameObject("GardenMemoryDirector");
            try
            {
                var d = go.AddComponent<GardenMemoryDirector>();
                Assert.AreEqual(GardenMemoryDirector.Beat.Off, d.Current);
                // 場面 9 では記憶するを邪魔しない。残す物も無い
                Assert.IsTrue(d.Settled);
                Assert.IsNull(d.Capture());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TheMemoryRunsFromTheCallToTheCut()
        {
            var go = new GameObject("GardenMemoryDirector");
            try
            {
                var d = go.AddComponent<GardenMemoryDirector>();
                d.Begin();
                Assert.AreEqual(GardenMemoryDirector.Beat.Calling, d.Current);
                Assert.IsTrue(GardenHandoff.Active);
                // 1 分の受け身の場面なので、流している間は途中の形を残さない（記憶するは場面の頭を書く）
                Assert.IsFalse(d.Settled);
                // 名を呼ぶ一行は E で送るまで残る
                d.Step(5f, false);
                Assert.AreEqual(GardenMemoryDirector.Beat.Calling, d.Current);
                d.Step(0.1f, true);
                Assert.AreEqual(GardenMemoryDirector.Beat.Leaning, d.Current);
                // 乗り出して戻ったら、庭を見る
                d.Step(3f, false);
                Assert.AreEqual(GardenMemoryDirector.Beat.Garden, d.Current);
                // 女性が繋がっていなくても、いつかは途切れる
                d.Step(120f, false);
                Assert.AreEqual(GardenMemoryDirector.Beat.Cut, d.Current);
                Assert.IsFalse(d.Settled);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TheGardenComesRightAfterTheRest()
        {
            var rest = System.Array.IndexOf(SceneMenu.Scenes, "Rest");
            Assert.AreEqual(SceneMenu.Garden, SceneMenu.Scenes[rest + 1]);
        }
    }
}
