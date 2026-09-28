using NUnit.Framework;

namespace HalfAware.Tests
{
    /// <summary>
    /// コンソールの設定の枠（設計書 1 節）。カメラの速さの範囲と刻み、残して読み直す、見回しの速さへの効き、
    /// 枠の上下と左右、右クリック（Esc）の戻り方、マウスの押した所からの値、左右の押し続け
    /// </summary>
    public class SettingsTests
    {
        MemoryBox box;

        [SetUp]
        public void Swap()
        {
            box = new MemoryBox();
            GameSettings.Box = box;
        }

        [TearDown]
        public void Restore()
        {
            GameSettings.Box = null;
            SaveStore.Box = null;
            ConsoleMenu.DebugOverride = null;
        }

        static SettingDial Look { get { return GameSettings.LookScale; } }

        // ---- 範囲と刻み --------------------------------------------------------

        [Test]
        public void TheCameraSpeedRunsFromAQuarterToTwiceInTwentiethsFromOne()
        {
            Assert.AreEqual(0.25f, Look.Min);
            Assert.AreEqual(2f, Look.Max);
            Assert.AreEqual(0.05f, Look.Step);
            Assert.AreEqual(1f, Look.Default);
            Assert.AreEqual(35, Look.Steps);
            Assert.AreEqual(1f, Look.Value, "何も書いていなければ既定");
            Assert.AreEqual("HalfAware.Settings.LookScale", Look.Key);
        }

        [Test]
        public void ValuesAreClampedAndSnappedToTheStep()
        {
            Look.Value = 5f;
            Assert.AreEqual(2f, Look.Value);
            Look.Value = 0f;
            Assert.AreEqual(0.25f, Look.Value);
            Look.Value = -3f;
            Assert.AreEqual(0.25f, Look.Value);
            Look.Value = 1.03f;
            Assert.AreEqual(1.05f, Look.Value, 1e-6f);
            Look.Value = 1.02f;
            Assert.AreEqual(1f, Look.Value, 1e-6f);
            Look.Value = float.NaN;
            Assert.AreEqual(1f, Look.Value, "NaN は既定");
            // 刻みで動かしても端数が溜まらない
            Look.Value = 0.25f;
            for (var i = 0; i < 15; i++) Look.Nudge(1);
            Assert.AreEqual(1f, Look.Value);
            Assert.AreEqual("1.00×", Look.Text(Look.Value));
        }

        [Test]
        public void NudgingStopsAtTheEnds()
        {
            Look.Nudge(1);
            Assert.AreEqual(1.05f, Look.Value, 1e-6f);
            Look.Nudge(-3);
            Assert.AreEqual(0.9f, Look.Value, 1e-6f);
            Look.Nudge(-100);
            Assert.AreEqual(0.25f, Look.Value);
            Look.Nudge(100);
            Assert.AreEqual(2f, Look.Value);
            Assert.AreEqual("2.00×", Look.Text(Look.Value));
            Assert.AreEqual("0.50×", Look.Text(0.5f));
        }

        // ---- 残して読み直す ----------------------------------------------------

        [Test]
        public void ItIsWrittenAndReadBackAfterARestart()
        {
            Look.Value = 0.5f;
            Assert.AreEqual("0.5", box.Get(Look.Key));
            // 動かすたびには Flush しない。枠を離れる・閉じる時にまとめて一度
            Assert.AreEqual(0, box.Flushes);
            Assert.IsTrue(GameSettings.Unsaved);
            GameSettings.Commit();
            Assert.AreEqual(1, box.Flushes);
            GameSettings.Commit();
            Assert.AreEqual(1, box.Flushes, "書いていなければ Flush しない");
            // 起動し直したのと同じ（覚えている値を捨てて、鍵から読み直す）
            GameSettings.Forget();
            Assert.AreEqual(0.5f, Look.Value);
            Look.Value = 1.75f;
            GameSettings.Forget();
            Assert.AreEqual(1.75f, Look.Value);
        }

        [Test]
        public void BrokenOrStrayValuesReadAsTheNearestAllowed()
        {
            box.Set(Look.Key, "not a number");
            GameSettings.Forget();
            Assert.AreEqual(1f, Look.Value);
            box.Set(Look.Key, "9");
            GameSettings.Forget();
            Assert.AreEqual(2f, Look.Value);
            box.Set(Look.Key, "0.73");
            GameSettings.Forget();
            Assert.AreEqual(0.75f, Look.Value, 1e-6f);
            box.Set(Look.Key, "NaN");
            GameSettings.Forget();
            Assert.AreEqual(1f, Look.Value);
        }

        [Test]
        public void TheSettingIsKeptApartFromTheSaves()
        {
            // セーブと同じ置き場に置いても、鍵が重ならず、セーブを消しても残る
            SaveStore.Box = box;
            SaveStore.Write(SaveSlot.First, new SaveData { stage = 1, scene = "Room" });
            SaveStore.MarkCleared();
            Look.Value = 1.5f;
            GameSettings.Commit();
            StringAssert.DoesNotStartWith(SaveStore.SaveKey, Look.Key);
            Assert.AreNotEqual(SaveStore.ClearedKey, Look.Key);
            foreach (var slot in SaveStore.All) SaveStore.Forget(slot);
            SaveStore.ForgetCleared();
            GameSettings.Forget();
            Assert.AreEqual(1.5f, Look.Value);
        }

        [Test]
        public void ResetBringsEverythingBackToDefault()
        {
            Look.Value = 0.4f;
            GameSettings.ResetAll();
            Assert.AreEqual(1f, Look.Value);
            Assert.AreEqual("1", box.Get(Look.Key));
        }

        // ---- 見回しの速さ ------------------------------------------------------

        [Test]
        public void LookSensitivityIsTheBaseTimesTheCameraSpeed()
        {
            Assert.AreEqual(0.042f, PlayerController.BaseLookSensitivity);
            Assert.AreEqual(0.042f, PlayerController.LookSensitivity, 1e-7f);
            Look.Value = 0.5f;
            Assert.AreEqual(0.021f, PlayerController.LookSensitivity, 1e-7f);
            Look.Value = 2f;
            Assert.AreEqual(0.084f, PlayerController.LookSensitivity, 1e-7f);
            Look.Value = 0.25f;
            Assert.AreEqual(0.0105f, PlayerController.LookSensitivity, 1e-7f);
        }

        // ---- 枠の上下と左右 ----------------------------------------------------

        [Test]
        public void TheTableHasTheCameraHeadingItsSpeedAndReset()
        {
            var rows = ConsoleSettings.Rows;
            Assert.AreEqual(3, rows.Length);
            Assert.AreEqual(SettingKind.Heading, rows[0].Kind);
            Assert.AreEqual("カメラ", rows[0].Label);
            Assert.AreEqual(SettingKind.Dial, rows[1].Kind);
            Assert.AreEqual("カメラの速さ", rows[1].Label);
            Assert.AreSame(GameSettings.LookScale, rows[1].Dial);
            Assert.AreEqual(SettingKind.Reset, rows[2].Kind);
            Assert.AreEqual("既定に戻す", rows[2].Label);
            Assert.AreEqual("設定", ConsoleSettings.Title);
        }

        static ConsoleMenu OpenSettings()
        {
            var m = new ConsoleMenu();
            m.Hover((int)ConsoleAction.Settings);
            Assert.AreEqual(ConsoleAction.Settings, m.Decide("Room"));
            Assert.AreEqual(ConsolePanel.Settings, m.Panel);
            return m;
        }

        [Test]
        public void SettingsOpensOnTheCameraSpeedAndSkipsTheHeading()
        {
            var m = OpenSettings();
            Assert.AreEqual(1, m.Row, "小見出しの次から");
            Assert.AreSame(GameSettings.LookScale, m.RowDial);
            Assert.AreEqual(ConsoleSettings.Rows.Length, m.Rows);
            m.MoveRow(1);
            Assert.AreEqual(2, m.Row);
            Assert.IsNull(m.RowDial);
            Assert.AreEqual(SettingKind.Reset, m.SettingRow.Kind);
            m.MoveRow(1);
            Assert.AreEqual(2, m.Row, "下の端で止まる");
            m.MoveRow(-1);
            m.MoveRow(-1);
            Assert.AreEqual(1, m.Row, "小見出しへは上がらない");
            m.HoverRow(0);
            Assert.AreEqual(1, m.Row, "小見出しに重ねても選ばない");
            Assert.IsFalse(m.Usable(0));
            m.HoverRow(2);
            Assert.AreEqual(2, m.Row);
        }

        [Test]
        public void LeftAndRightMoveTheDialNotTheButtons()
        {
            var m = OpenSettings();
            m.Move(1);
            Assert.AreEqual(1.05f, Look.Value, 1e-6f);
            m.Move(-1);
            m.Move(-1);
            m.Move(-1);
            Assert.AreEqual(0.9f, Look.Value, 1e-6f);
            Assert.AreEqual(ConsoleAction.Settings, m.Selected, "ボタンは動かない");
            Assert.AreEqual(ConsolePanel.Settings, m.Panel, "枠は閉じない");
            // 既定に戻すの行では、左右は何もしない
            m.MoveRow(1);
            m.Move(1);
            m.Move(-1);
            Assert.AreEqual(0.9f, Look.Value, 1e-6f);
            Assert.AreEqual(ConsoleAction.Settings, m.Selected);
            Assert.AreEqual(ConsolePanel.Settings, m.Panel);
        }

        [Test]
        public void ResetWorksOnlyOnItsRow()
        {
            var m = OpenSettings();
            Look.Value = 1.6f;
            Assert.IsFalse(m.ResetSettings(), "つまみの行では戻さない");
            Assert.AreEqual(1.6f, Look.Value, 1e-6f);
            m.MoveRow(1);
            Assert.IsTrue(m.ResetSettings());
            Assert.AreEqual(1f, Look.Value);
            Assert.AreEqual(ConsolePanel.Settings, m.Panel, "枠は開いたまま");
        }

        [Test]
        public void OutsideTheSettingsPanelNothingMovesTheDial()
        {
            var m = new ConsoleMenu();
            Assert.IsFalse(m.Nudge(1));
            Assert.IsFalse(m.ResetSettings());
            Assert.IsNull(m.SettingRow);
            m.Move(1);
            Assert.AreEqual(1f, Look.Value);
            Assert.AreEqual(ConsoleAction.Recall, m.Selected);
        }

        [Test]
        public void BackClosesTheSettingsThenTheConsole()
        {
            var m = OpenSettings();
            m.Move(1);
            Assert.IsTrue(m.Back(), "右クリック・Esc は枠を閉じる");
            Assert.AreEqual(ConsolePanel.None, m.Panel);
            Assert.AreEqual(ConsoleAction.Settings, m.Selected, "ボタンの選びはそのまま");
            Assert.AreEqual(1.05f, Look.Value, 1e-6f, "動かした値はそのまま");
            // 枠を閉じた後の左右は、今どおりボタンを選ぶ
            m.Move(-1);
            Assert.AreEqual(ConsoleAction.CloseEyes, m.Selected);
            Assert.IsFalse(m.Back(), "何も開いていなければコンソールを閉じる");
        }

        [Test]
        public void MovingOffTheSettingsButtonWithTheMouseKeepsThePanel()
        {
            var m = OpenSettings();
            m.Hover((int)ConsoleAction.Remember);
            Assert.AreEqual(ConsolePanel.Settings, m.Panel);
            // 決めると、選んでいるボタンの枠へ替わる
            m.Decide("Room");
            Assert.AreEqual(ConsolePanel.Remember, m.Panel);
        }

        [Test]
        public void AReleaseBuildHasSettingsAsTheLastButton()
        {
            ConsoleMenu.DebugOverride = false;
            var m = new ConsoleMenu();
            m.Move(10);
            Assert.AreEqual(ConsoleAction.Settings, m.Selected);
            m.Decide("Room");
            Assert.AreEqual(ConsolePanel.Settings, m.Panel);
            m.Move(1);
            Assert.AreEqual(1.05f, Look.Value, 1e-6f);
        }

        // ---- マウスの押した所 --------------------------------------------------

        [Test]
        public void ThePressedSpotOnTheTrackGivesTheValue()
        {
            // 当たりの幅 144、両端の溝の外 3（つまみの半分）。溝は 3 から 141
            const float width = 144f;
            const float inset = 3f;
            Assert.AreEqual(0f, ConsoleSettings.TrackAt(0f, width, inset));
            Assert.AreEqual(0f, ConsoleSettings.TrackAt(2f / width, width, inset), "溝の左の外は左の端");
            Assert.AreEqual(1f, ConsoleSettings.TrackAt(1f, width, inset));
            Assert.AreEqual(1f, ConsoleSettings.TrackAt(142f / width, width, inset), "溝の右の外は右の端");
            Assert.AreEqual(0.5f, ConsoleSettings.TrackAt(0.5f, width, inset), 1e-6f);
            Assert.AreEqual(0.25f, Look.AtFraction(ConsoleSettings.TrackAt(0f, width, inset)));
            Assert.AreEqual(2f, Look.AtFraction(ConsoleSettings.TrackAt(1f, width, inset)));
            // 既定（1 倍）の所は溝の 3/7。その少し脇を押しても 1 倍に揃う
            var one = (inset + (width - inset * 2f) * 0.75f / 1.75f) / width;
            Assert.AreEqual(1f, Look.AtFraction(ConsoleSettings.TrackAt(one, width, inset)), 1e-6f);
            Assert.AreEqual(1f, Look.AtFraction(ConsoleSettings.TrackAt(one + 1f / width, width, inset)), 1e-6f);
            // 溝の 1/5 は 0.25 + 1.75 × 0.2 = 0.6
            var fifth = (inset + (width - inset * 2f) * 0.2f) / width;
            Assert.AreEqual(0.6f, Look.AtFraction(ConsoleSettings.TrackAt(fifth, width, inset)), 1e-6f);
            Assert.AreEqual(0.4285714f, Look.Fraction(1f), 1e-5f);
            // 幅の無い当たり
            Assert.AreEqual(0f, ConsoleSettings.TrackAt(0.5f, 4f, 3f));
        }

        // ---- 左右の押し続け ----------------------------------------------------

        [Test]
        public void HoldingMovesOnceThenRepeats()
        {
            var r = new HoldRepeat();
            Assert.AreEqual(1, r.Step(1, 1, 10f), "押した時に一つ");
            Assert.AreEqual(0, r.Step(0, 1, 10.1f));
            Assert.AreEqual(0, r.Step(0, 1, 10f + HoldRepeat.Delay - 0.01f), "待つ間は動かない");
            Assert.AreEqual(1, r.Step(0, 1, 10f + HoldRepeat.Delay + 0.001f));
            Assert.AreEqual(0, r.Step(0, 1, 10f + HoldRepeat.Delay + 0.01f));
            Assert.AreEqual(1, r.Step(0, 1, 10f + HoldRepeat.Delay + HoldRepeat.Every + 0.002f));
            // 離したら止まる。押し直すとまた一つから
            Assert.AreEqual(0, r.Step(0, 0, 11f));
            Assert.AreEqual(0, r.Step(0, 1, 12f), "押し直しの無い押し続けでは動かない");
            Assert.AreEqual(-1, r.Step(-1, -1, 13f));
            Assert.AreEqual(0, r.Step(0, -1, 13.2f));
            // 反対へ押し替えたら、その向きで一つ
            Assert.AreEqual(1, r.Step(1, 1, 13.3f));
            Assert.AreEqual(0, r.Step(0, 1, 13.3f + HoldRepeat.Delay - 0.01f));
        }

        [Test]
        public void KeysHeldBeforeThePanelOpenedDoNotMoveIt()
        {
            var r = new HoldRepeat();
            // 右の鍵で設定のボタンへ移り、押したまま E で枠を開いた。押し続けは数えない
            r.Release();
            for (var i = 0; i < 30; i++) Assert.AreEqual(0, r.Step(0, 1, 1f + i * 0.1f));
        }

        [Test]
        public void ASlowFrameMovesOnlyOnce()
        {
            var r = new HoldRepeat();
            r.Step(1, 1, 0f);
            Assert.AreEqual(1, r.Step(0, 1, 2f));
            Assert.AreEqual(1, r.Step(0, 1, 2.01f), "遅れを取り戻すのは一フレームに一つ");
            Assert.AreEqual(0, r.Step(0, 1, 2.02f));
        }
    }
}
