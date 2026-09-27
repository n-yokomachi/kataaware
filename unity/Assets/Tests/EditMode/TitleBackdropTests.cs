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
            Assert.AreEqual(7, StageMap.StageOf("Notice", ""));
            Assert.AreEqual(8, StageMap.StageOf("Drive", ""));
            Assert.AreEqual(9, StageMap.StageOf("Village", StageMap.Morning));
        }

        [Test]
        public void TheTitleAndUnmadeStagesAreNotSaved()
        {
            Assert.AreEqual(0, StageMap.StageOf(TitleScreen.SceneName, ""));
            Assert.AreEqual(0, StageMap.StageOf(null, ""));
            // 夕方の村は場面 6（庭の記憶）、場面 10（対面）は朝の村。表の値は GardenMemoryTests・ReunionTests で見る。
            // 場面は 1〜10 まで揃ったので、無い場面は範囲の外だけ
            Assert.IsNull(StageMap.SceneOf(0));
            Assert.IsNull(StageMap.SceneOf(StageMap.Last + 1));
            Assert.IsFalse(StageMap.Playable(StageMap.Last + 1));
        }

        [Test]
        public void EveryStageMapsBackToItsScene()
        {
            for (var s = StageMap.First; s <= StageMap.Last; s++)
            {
                Assert.IsNotEmpty(StageMap.TitleOf(s), "場面 " + s);
                var scene = StageMap.SceneOf(s);
                if (scene == null) continue;
                // 場面 10 は場面 9 と同じ村の朝。シーンと時刻からは場面 9 に当たり、場面 10 かは村の演出の印（ReunionHandoff）で分ける
                var expected = s == ReunionHandoff.Stage ? 9 : s;
                Assert.AreEqual(expected, StageMap.StageOf(scene, StageMap.HourOf(s)), scene);
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
                // 庭の記憶の行は村の夕方（場面 6）。村の行は朝（場面 9）
                var entry = SceneMenu.Scenes[i];
                var scene = SceneMenu.SceneOf(entry);
                var hour = entry == SceneMenu.Garden ? StageMap.Evening : scene == "Village" ? StageMap.Morning : "";
                // 対面の行も村の朝。場面 10 かは村の演出の印で分ける
                var stage = entry == SceneMenu.Reunion ? ReunionHandoff.Stage : StageMap.StageOf(scene, hour);
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
            Assert.AreEqual("dive_estate", TitleBackdrops.FileName(TitleBackdrop.Dive));
            Assert.AreEqual("village_a1_morning", TitleBackdrops.FileName(TitleBackdrop.VillageMorning));
            Assert.AreEqual("village_a1_evening", TitleBackdrops.FileName(TitleBackdrop.VillageEvening));
            Assert.AreEqual("Village", TitleBackdrops.SceneOf(TitleBackdrop.VillageEvening));
            Assert.AreEqual("Dive", TitleBackdrops.SceneOf(TitleBackdrop.Dive));
        }

        // ---- 環境音 ------------------------------------------------------------

        [Test]
        public void TheRoomSoundsAsLoudAsInTheRoom()
        {
            var room = TitleBackdrops.SoundsOf(TitleBackdrop.Room);
            Assert.AreEqual(1, room.Length);
            Assert.AreEqual("RoomTone", room[0].File);
            Assert.AreEqual(RoomTone.DefaultVolume, room[0].Volume, 1e-6f);
        }

        [Test]
        public void TheAlleyHasTheCrowdAndTheRainOfTheStreet()
        {
            var alley = TitleBackdrops.SoundsOf(TitleBackdrop.Alley);
            Assert.AreEqual(2, alley.Length);
            Assert.AreEqual("CrowdLoop", alley[0].File);
            Assert.AreEqual(CrowdNoise.StreetDefault, alley[0].Volume, 1e-6f);
            Assert.AreEqual("RainLoop", alley[1].File);
            Assert.AreEqual(TitleBackdrops.AlleyRain, alley[1].Volume, 1e-6f);
        }

        [Test]
        public void TheDiveAndTheGarageAreSilent()
        {
            Assert.AreEqual(0, TitleBackdrops.SoundsOf(TitleBackdrop.Dive).Length);
            Assert.AreEqual(0, TitleBackdrops.SoundsOf(TitleBackdrop.Drive).Length);
        }

        [Test]
        public void TheVillageSoundsAsInTheGardenOfTheVillage()
        {
            // 朝の庭（アーチのトンネルの先）では、麦の風は路地の途中で消えきっている。鳥の声の朝の村の輪だけ
            Assert.AreEqual(0f, VillageAmbience.Reach(TitleBackdrops.VillageEyeX,
                VillageAmbience.DefaultWheatFadeFrom, VillageAmbience.DefaultWheatFadeTo), 1e-6f);
            var morning = TitleBackdrops.SoundsOf(TitleBackdrop.VillageMorning);
            Assert.AreEqual(1, morning.Length);
            Assert.AreEqual("VillageMorning", morning[0].File);
            Assert.AreEqual(VillageAmbience.DefaultMorningVillage, morning[0].Volume, 1e-6f);
            // 夕方は麦の風だけ
            var evening = TitleBackdrops.SoundsOf(TitleBackdrop.VillageEvening);
            Assert.AreEqual(1, evening.Length);
            Assert.AreEqual("WheatWind", evening[0].File);
            Assert.AreEqual(VillageAmbience.DefaultEveningWheat, evening[0].Volume, 1e-6f);
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
