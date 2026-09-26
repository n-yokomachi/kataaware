using NUnit.Framework;

namespace HalfAware.Tests
{
    /// <summary>場面の番号の対応と、タイトルの画面の背景の選び方（設計書 5 節）</summary>
    public class TitleBackdropTests
    {
        static SaveData At(int stage)
        {
            return new SaveData { stage = stage, scene = StageMap.SceneOf(stage) ?? "Village" };
        }

        // ---- 場面の番号 --------------------------------------------------------

        [Test]
        public void TheScenesHaveTheirStageNumbers()
        {
            Assert.AreEqual(1, StageMap.StageOf("Room", ""));
            Assert.AreEqual(2, StageMap.StageOf("Alley", ""));
            Assert.AreEqual(3, StageMap.StageOf("Connect", ""));
            Assert.AreEqual(4, StageMap.StageOf("Dive", ""));
            Assert.AreEqual(5, StageMap.StageOf("Rest", ""));
            Assert.AreEqual(8, StageMap.StageOf("Drive", ""));
            Assert.AreEqual(9, StageMap.StageOf("Village", StageMap.Morning));
        }

        [Test]
        public void TheTitleAndUnmadeStagesAreNotSaved()
        {
            Assert.AreEqual(0, StageMap.StageOf(TitleScreen.SceneName, ""));
            Assert.AreEqual(0, StageMap.StageOf(null, ""));
            // 夕方の村は場面 6。まだ作っていないので、表に無い
            Assert.AreEqual(0, StageMap.StageOf("Village", StageMap.Evening));
            Assert.IsNull(StageMap.SceneOf(6));
            Assert.IsNull(StageMap.SceneOf(7));
            Assert.IsNull(StageMap.SceneOf(10));
            Assert.IsFalse(StageMap.Playable(7));
        }

        [Test]
        public void EveryStageMapsBackToItsScene()
        {
            for (var s = StageMap.First; s <= StageMap.Last; s++)
            {
                Assert.IsNotEmpty(StageMap.TitleOf(s), "場面 " + s);
                var scene = StageMap.SceneOf(s);
                if (scene == null) continue;
                Assert.AreEqual(s, StageMap.StageOf(scene, StageMap.HourOf(s)), scene);
            }
            Assert.AreEqual("Village", StageMap.SceneOf(9));
            Assert.AreEqual(StageMap.Morning, StageMap.HourOf(9));
            Assert.AreEqual(string.Empty, StageMap.TitleOf(0));
            Assert.AreEqual(string.Empty, StageMap.TitleOf(11));
        }

        [Test]
        public void EverySceneInTheDebugListHasAStage()
        {
            for (var i = 0; i < SceneMenu.Scenes.Length; i++)
            {
                var scene = SceneMenu.Scenes[i];
                var stage = StageMap.StageOf(scene, scene == "Village" ? StageMap.Morning : "");
                Assert.Greater(stage, 0, scene);
                Assert.AreEqual(SceneMenu.Titles[i], StageMap.TitleOf(stage), scene);
            }
        }

        // ---- 背景 ------------------------------------------------------------

        [Test]
        public void WithNoSaveTheRoomIsBehind()
        {
            Assert.AreEqual(TitleBackdrop.Room, TitleBackdrops.Pick(false, null));
        }

        [Test]
        public void AfterClearingTheMorningVillageComesBeforeTheNewestSave()
        {
            Assert.AreEqual(TitleBackdrop.VillageMorning, TitleBackdrops.Pick(true, null));
            Assert.AreEqual(TitleBackdrop.VillageMorning, TitleBackdrops.Pick(true, At(2)));
            Assert.AreEqual(TitleBackdrop.VillageMorning, TitleBackdrops.Pick(true, At(6)));
        }

        [Test]
        public void TheNewestSaveChoosesTheBackdrop()
        {
            Assert.AreEqual(TitleBackdrop.Room, TitleBackdrops.Pick(false, At(1)));
            Assert.AreEqual(TitleBackdrop.Alley, TitleBackdrops.Pick(false, At(2)));
            Assert.AreEqual(TitleBackdrop.Room, TitleBackdrops.Pick(false, At(3)));
            Assert.AreEqual(TitleBackdrop.Dive, TitleBackdrops.Pick(false, At(4)));
            Assert.AreEqual(TitleBackdrop.Room, TitleBackdrops.Pick(false, At(5)));
            Assert.AreEqual(TitleBackdrop.VillageEvening, TitleBackdrops.Pick(false, At(6)));
            Assert.AreEqual(TitleBackdrop.Room, TitleBackdrops.Pick(false, At(7)));
            Assert.AreEqual(TitleBackdrop.Drive, TitleBackdrops.Pick(false, At(8)));
            Assert.AreEqual(TitleBackdrop.VillageMorning, TitleBackdrops.Pick(false, At(9)));
            Assert.AreEqual(TitleBackdrop.VillageMorning, TitleBackdrops.Pick(false, At(10)));
        }

        [Test]
        public void TheVillageIsSunkLessAndTheEveningLeansToTheMorning()
        {
            Assert.AreEqual(1f, TitleBackdrops.Light(TitleBackdrop.VillageMorning));
            Assert.AreEqual(0f, TitleBackdrops.Light(TitleBackdrop.Room));
            Assert.AreEqual(0f, TitleBackdrops.Light(TitleBackdrop.Alley));
            Assert.AreEqual(0f, TitleBackdrops.Light(TitleBackdrop.Dive));
            Assert.AreEqual(0f, TitleBackdrops.Light(TitleBackdrop.Drive));
            // 夕方は朝とふつうのあいだより朝寄り
            var evening = TitleBackdrops.Light(TitleBackdrop.VillageEvening);
            Assert.Greater(evening, 0.5f);
            Assert.Less(evening, 1f);
            // 題の後ろは沈め、明るい背景はその外を明るく残す
            Assert.Less(TitleScreen.DimAt(0.5f, 0.3f, 1f), TitleScreen.DimAt(0.5f, 0.3f, 0f));
            Assert.Greater(TitleScreen.DimAt(0f, 0f, 0f), TitleScreen.DimAt(0.5f, 0.55f, 0f));
            var mid = TitleScreen.DimAt(0.2f, 0.4f, evening);
            Assert.Less(mid, TitleScreen.DimAt(0.2f, 0.4f, 0f));
            Assert.Greater(mid, TitleScreen.DimAt(0.2f, 0.4f, 1f));
        }

        [Test]
        public void EachBackdropHasItsPictureAndScene()
        {
            Assert.AreEqual(TitleBackdrops.Count, System.Enum.GetValues(typeof(TitleBackdrop)).Length);
            Assert.AreEqual("room_1", TitleBackdrops.FileName(TitleBackdrop.Room));
            Assert.AreEqual("dive_3", TitleBackdrops.FileName(TitleBackdrop.Dive));
            Assert.AreEqual("village_a1_morning", TitleBackdrops.FileName(TitleBackdrop.VillageMorning));
            Assert.AreEqual("village_a1_evening", TitleBackdrops.FileName(TitleBackdrop.VillageEvening));
            Assert.AreEqual("Village", TitleBackdrops.SceneOf(TitleBackdrop.VillageEvening));
            Assert.AreEqual("Dive", TitleBackdrops.SceneOf(TitleBackdrop.Dive));
        }

        // ---- 起動の表示の日時と場所 ------------------------------------------------

        [Test]
        public void TheBootLineTellsTheBackdropsTimeAndPlace()
        {
            Assert.AreEqual(ConsolePlace.For("Room"), TitleBackdrops.BootPlace(false, null, "x"));
            Assert.AreEqual(ConsolePlace.For("Village"), TitleBackdrops.BootPlace(true, At(3), "x"));
            Assert.AreEqual(ConsolePlace.For("Connect"), TitleBackdrops.BootPlace(false, At(3), "x"));
            Assert.AreEqual("潜行中　2156/03/02 07:14　団地", TitleBackdrops.BootPlace(false, At(4), "潜行中　2156/03/02 07:14　団地"));
            Assert.AreEqual(ConsolePlace.Diving, TitleBackdrops.BootPlace(false, At(4), ""));
            StringAssert.StartsWith(ConsolePlace.Diving, TitleBackdrops.BootPlace(false, At(6), ""));
            Assert.AreEqual(ConsolePlace.For("Village"), TitleBackdrops.BootPlace(false, At(10), ""));
        }
    }
}
