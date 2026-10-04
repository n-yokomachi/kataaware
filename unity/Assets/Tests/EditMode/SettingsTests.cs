using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HalfAware.Tests
{
    /// <summary>
    /// コンソールの設定の枠（設計書 1 節）。カメラの速さの範囲と刻み、減色の強さ・ディザの強さの範囲と刻みと効く時、残して読み直す（動きが止まってから書く）、
    /// 見回しの速さへの効き（マウスとスティック）、枠の上下と左右（つまみとフィルター）、右クリック（Esc）の戻り方、マウスの押した所からの値、左右の押し続け
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
            ScreenFilter.Use(ScreenFilterKind.Standard);
        }

        static SettingDial Look { get { return GameSettings.LookScale; } }

        // ---- 範囲と刻み --------------------------------------------------------

        [Test]
        public void TheCameraSpeedRunsFromAQuarterToTwiceInTwentiethsFromOneAndAHalf()
        {
            Assert.AreEqual(0.25f, Look.Min);
            Assert.AreEqual(2f, Look.Max);
            Assert.AreEqual(0.05f, Look.Step);
            // 2026-09-29 にオーナーの指示で既定を 1 倍から 1.5 倍にした（「デフォルトのカメラ速度を1.5に」）
            Assert.AreEqual(1.5f, Look.Default);
            Assert.AreEqual(35, Look.Steps);
            Assert.AreEqual(1.5f, Look.Value, "何も書いていなければ既定");
            Assert.AreEqual("1.50×", Look.Text(Look.Default));
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
            Assert.AreEqual(1.5f, Look.Value, "NaN は既定");
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
            Assert.AreEqual(1.55f, Look.Value, 1e-6f);
            Look.Nudge(-3);
            Assert.AreEqual(1.4f, Look.Value, 1e-6f);
            Look.Nudge(-100);
            Assert.AreEqual(0.25f, Look.Value);
            Look.Nudge(100);
            Assert.AreEqual(2f, Look.Value);
            Assert.AreEqual("2.00×", Look.Text(Look.Value));
            Assert.AreEqual("0.50×", Look.Text(0.5f));
        }

        // ---- 減色の強さ・ディザの強さ --------------------------------------------

        [Test]
        public void TheFilterStrengthsRunFromNoneToFullInFivePercents()
        {
            foreach (var dial in new[] { GameSettings.FilterTint, GameSettings.FilterDots })
            {
                Assert.AreEqual(0f, dial.Min);
                Assert.AreEqual(1f, dial.Max);
                Assert.AreEqual(0.05f, dial.Step);
                Assert.AreEqual(20, dial.Steps);
                Assert.AreEqual(dial.Default, dial.Value, "何も書いていなければ既定");
                Assert.AreEqual("50%", dial.Text(0.5f));
                Assert.AreEqual("0%", dial.Text(0f));
                Assert.AreEqual("100%", dial.Text(1f));
                Assert.AreEqual("55%", dial.Text(0.55f));
                dial.Value = 0.33f;
                Assert.AreEqual(0.35f, dial.Value, 1e-6f, "5% 刻みへ揃える");
                Assert.AreEqual("35%", dial.Text(dial.Value));
                dial.Nudge(-100);
                Assert.AreEqual(0f, dial.Value);
                dial.Nudge(100);
                Assert.AreEqual(1f, dial.Value);
                Assert.AreEqual("1", box.Get(dial.Key));
            }
            // 既定は画面の加工の既定と同じ値
            Assert.AreEqual(ScreenFilter.DefaultTint, GameSettings.FilterTint.Default);
            Assert.AreEqual(ScreenFilter.DefaultDots, GameSettings.FilterDots.Default);
            Assert.AreEqual("HalfAware.Settings.FilterTint", GameSettings.FilterTint.Key);
            Assert.AreEqual("HalfAware.Settings.FilterDots", GameSettings.FilterDots.Key);
            CollectionAssert.Contains(GameSettings.All, GameSettings.FilterTint, "読み直しと既定に戻すが回る");
            CollectionAssert.Contains(GameSettings.All, GameSettings.FilterDots);
        }

        [Test]
        public void TheFilterStrengthsAreWrittenAndReadBack()
        {
            // 既定と同じ値は書かないので、既定から離れた値で見る
            GameSettings.FilterTint.Value = 0.85f;
            GameSettings.FilterDots.Value = 0.65f;
            Assert.AreEqual("0.85", box.Get(GameSettings.FilterTint.Key));
            Assert.AreEqual("0.65", box.Get(GameSettings.FilterDots.Key));
            GameSettings.Commit();
            GameSettings.Forget();
            Assert.AreEqual(0.85f, GameSettings.FilterTint.Value, 1e-6f);
            Assert.AreEqual(0.65f, GameSettings.FilterDots.Value, 1e-6f);
            box.Set(GameSettings.FilterDots.Key, "7");
            box.Set(GameSettings.FilterTint.Key, "broken");
            GameSettings.Forget();
            Assert.AreEqual(1f, GameSettings.FilterDots.Value, "範囲の外は端");
            Assert.AreEqual(GameSettings.FilterTint.Default, GameSettings.FilterTint.Value, "読めない字は既定");
        }

        [Test]
        public void ADialTellsWhenItChanges()
        {
            var told = 0;
            System.Action count = () => told++;
            GameSettings.FilterTint.Value = 0.5f;
            GameSettings.FilterTint.Changed += count;
            try
            {
                GameSettings.FilterTint.Value = 0.5f;
                Assert.AreEqual(0, told, "同じ値では知らせない");
                GameSettings.FilterTint.Nudge(1);
                Assert.AreEqual(1, told);
                GameSettings.FilterTint.Nudge(-100);
                GameSettings.FilterTint.Nudge(-1);
                Assert.AreEqual(2, told, "端で止まって変わらなければ知らせない");
                GameSettings.ResetAll();
                Assert.AreEqual(3, told, "既定に戻すでも知らせる");
                box.Set(GameSettings.FilterTint.Key, "0.2");
                GameSettings.Forget();
                Assert.AreEqual(0.2f, GameSettings.FilterTint.Value, 1e-6f);
                Assert.AreEqual(3, told, "鍵から読み直しただけでは知らせない");
            }
            finally
            {
                GameSettings.FilterTint.Changed -= count;
            }
        }

        [Test]
        public void TheStrengthRowsWorkOnlyWithTheDitherFilter()
        {
            var tint = ConsoleSettings.Rows[4];
            var dots = ConsoleSettings.Rows[5];
            Assert.IsFalse(tint.Live, "標準の型には効かない");
            Assert.IsFalse(dots.Live);
            GameSettings.Filter.Value = (int)ScreenFilterKind.Dither;
            Assert.IsTrue(tint.Live);
            Assert.IsTrue(dots.Live);
            // ほかの行はいつも効いている
            foreach (var i in new[] { 1, 3, 6 }) Assert.IsTrue(ConsoleSettings.Rows[i].Live, ConsoleSettings.Rows[i].Label);
            GameSettings.Filter.Value = (int)ScreenFilterKind.Standard;
            foreach (var i in new[] { 1, 3, 6 }) Assert.IsTrue(ConsoleSettings.Rows[i].Live, ConsoleSettings.Rows[i].Label);
            // 効いていなくても選べて、動かせる
            var m = OpenSettings();
            m.MoveRow(2);
            Assert.AreSame(GameSettings.FilterTint, m.RowDial);
            m.Move(-1);
            Assert.AreEqual(GameSettings.FilterTint.Snap(GameSettings.FilterTint.Default - 0.05f), GameSettings.FilterTint.Value, 1e-6f);
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
        public void ItIsFlushedHalfASecondAfterTheLastMove()
        {
            GameSettings.Tick(0f);
            Assert.AreEqual(0, box.Flushes, "動かしていなければ書かない");
            // 押し続けで続けて動かす（0.05 秒ごと）。動いている間は書かない
            for (var i = 0; i < 10; i++)
            {
                Look.Nudge(1);
                GameSettings.Tick(10f + i * HoldRepeat.Every);
            }
            Assert.AreEqual(0, box.Flushes);
            var last = 10f + 9 * HoldRepeat.Every;
            GameSettings.Tick(last + GameSettings.SaveDelay - 0.01f);
            Assert.AreEqual(0, box.Flushes, "止まってから 0.5 秒たつまでは待つ");
            GameSettings.Tick(last + GameSettings.SaveDelay + 0.01f);
            Assert.AreEqual(1, box.Flushes, "止まって 0.5 秒で一度だけ書く");
            Assert.IsFalse(GameSettings.Unsaved);
            GameSettings.Tick(last + 5f);
            Assert.AreEqual(1, box.Flushes);
            // 閉じる時は待たずに書く
            Look.Nudge(-1);
            GameSettings.Tick(20f);
            GameSettings.Commit();
            Assert.AreEqual(2, box.Flushes);
            GameSettings.Tick(30f);
            Assert.AreEqual(2, box.Flushes, "書いた後に待っていた分は書かない");
            // 書いた値は、起動し直しても残る
            GameSettings.Forget();
            Assert.AreEqual(1.95f, Look.Value, 1e-6f);
        }

        [Test]
        public void BrokenOrStrayValuesReadAsTheNearestAllowed()
        {
            box.Set(Look.Key, "not a number");
            GameSettings.Forget();
            Assert.AreEqual(1.5f, Look.Value);
            box.Set(Look.Key, "9");
            GameSettings.Forget();
            Assert.AreEqual(2f, Look.Value);
            box.Set(Look.Key, "0.73");
            GameSettings.Forget();
            Assert.AreEqual(0.75f, Look.Value, 1e-6f);
            box.Set(Look.Key, "NaN");
            GameSettings.Forget();
            Assert.AreEqual(1.5f, Look.Value);
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
            GameSettings.Filter.Value = (int)ScreenFilterKind.Dither;
            GameSettings.FilterTint.Value = 1f;
            GameSettings.FilterDots.Value = 0.1f;
            GameSettings.ResetAll();
            Assert.AreEqual(1.5f, Look.Value);
            Assert.AreEqual("1.5", box.Get(Look.Key));
            Assert.AreEqual((int)ScreenFilterKind.Standard, GameSettings.Filter.Value);
            Assert.AreEqual("Standard", box.Get(GameSettings.Filter.Key));
            Assert.AreEqual(ScreenFilter.DefaultTint, GameSettings.FilterTint.Value);
            Assert.AreEqual(ScreenFilter.DefaultDots, GameSettings.FilterDots.Value);
            Assert.AreEqual(ScreenFilter.DefaultTint.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), box.Get(GameSettings.FilterTint.Key));
            Assert.AreEqual(ScreenFilter.DefaultDots.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), box.Get(GameSettings.FilterDots.Key));
        }

        // ---- 見回しの速さ ------------------------------------------------------

        [Test]
        public void LookSensitivityIsTheBaseTimesTheCameraSpeed()
        {
            // 2026-09-28 にオーナーの指示で 1 倍の速さを半分にした（0.042 → 0.021）
            Assert.AreEqual(0.021f, PlayerController.BaseLookSensitivity);
            // 既定（1.5 倍）で 0.0315 度／画素
            Assert.AreEqual(0.0315f, PlayerController.LookSensitivity, 1e-7f);
            Look.Value = 1f;
            Assert.AreEqual(0.021f, PlayerController.LookSensitivity, 1e-7f);
            Look.Value = 0.5f;
            Assert.AreEqual(0.0105f, PlayerController.LookSensitivity, 1e-7f);
            Look.Value = 2f;
            Assert.AreEqual(0.042f, PlayerController.LookSensitivity, 1e-7f);
            Look.Value = 0.25f;
            Assert.AreEqual(0.00525f, PlayerController.LookSensitivity, 1e-7f);
        }

        [Test]
        public void TheStickTurnsByTiltTimesDegreesPerSecond()
        {
            // 2026-09-28 にオーナーの指示で、マウスと一緒に半分にした（120 → 60）
            Assert.AreEqual(60f, PlayerController.StickDegreesPerSecond);
            // 既定（1.5 倍）では倒しきって 1 秒で 90 度
            Assert.AreEqual(90f, PlayerController.StickLookSpeed, 1e-4f);
            // 1 倍なら倒しきって 1 秒で 60 度。半分倒せば 30 度。上下も同じ
            Look.Value = 1f;
            var full = PlayerController.LookTurn(new Vector2(1f, 0f), true, 1f);
            Assert.AreEqual(60f, full.x, 1e-4f);
            Assert.AreEqual(0f, full.y, 1e-4f);
            var half = PlayerController.LookTurn(new Vector2(-0.5f, 0.5f), true, 1f / 60f);
            Assert.AreEqual(-0.5f, half.x, 1e-4f);
            Assert.AreEqual(0.5f, half.y, 1e-4f);
            // 倒した量は 1 までに収める（ハットの斜めは (1, 1) で来る）
            var diagonal = PlayerController.LookTurn(new Vector2(1f, 1f), true, 1f);
            Assert.AreEqual(60f, diagonal.magnitude, 1e-3f);
            // 秒が無ければ回らない
            Assert.AreEqual(Vector2.zero, PlayerController.LookTurn(Vector2.one, true, 0f));
            // 設定の倍率はスティックにも掛かる
            Look.Value = 0.5f;
            Assert.AreEqual(30f, PlayerController.StickLookSpeed, 1e-4f);
            Assert.AreEqual(30f, PlayerController.LookTurn(new Vector2(1f, 0f), true, 1f).x, 1e-4f);
            Look.Value = 2f;
            Assert.AreEqual(120f, PlayerController.LookTurn(new Vector2(0f, -1f), true, 1f).y * -1f, 1e-4f);
        }

        [Test]
        public void TheMouseStillTurnsByPixelsWhateverTheFrame()
        {
            // マウスは動いた画素 × 0.021 度（1 倍の時）で、秒は関わらない
            Look.Value = 1f;
            var turn = PlayerController.LookTurn(new Vector2(100f, -50f), false, 1f / 60f);
            Assert.AreEqual(2.1f, turn.x, 1e-4f);
            Assert.AreEqual(-1.05f, turn.y, 1e-4f);
            Assert.AreEqual(turn, PlayerController.LookTurn(new Vector2(100f, -50f), false, 0.5f));
            // 大きく動かしても収めない（画素は倒した量ではない）
            Assert.AreEqual(21f, PlayerController.LookTurn(new Vector2(1000f, 0f), false, 0.01f).x, 1e-3f);
            Look.Value = 1.5f;
            Assert.AreEqual(3.15f, PlayerController.LookTurn(new Vector2(100f, 0f), false, 1f).x, 1e-4f);
        }

        [Test]
        public void TheGamepadStickIsReadAsAStickAndTheMouseAsPixels()
        {
            Assert.IsFalse(PlayerController.FromStick(null), "何も押していなければマウスと同じ扱い（入力は 0）");
            if (Mouse.current != null) Assert.IsFalse(PlayerController.FromStick(Mouse.current.delta));
            var pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Assert.IsTrue(PlayerController.FromStick(pad.rightStick));
                Assert.IsTrue(PlayerController.FromStick(pad.dpad));
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
            }
        }

        // ---- 枠の上下と左右 ----------------------------------------------------

        [Test]
        public void TheTableHasTheCameraTheScreenAndReset()
        {
            var rows = ConsoleSettings.Rows;
            Assert.AreEqual(7, rows.Length);
            Assert.AreEqual(SettingKind.Heading, rows[0].Kind);
            Assert.AreEqual("カメラ", rows[0].Label);
            Assert.AreEqual(SettingKind.Dial, rows[1].Kind);
            Assert.AreEqual("カメラの速さ", rows[1].Label);
            Assert.AreSame(GameSettings.LookScale, rows[1].Dial);
            Assert.IsNull(rows[1].Choice);
            Assert.AreEqual(SettingKind.Heading, rows[2].Kind);
            Assert.AreEqual("画面", rows[2].Label);
            Assert.AreEqual(SettingKind.Choice, rows[3].Kind);
            Assert.AreEqual("フィルター", rows[3].Label);
            Assert.AreSame(GameSettings.Filter, rows[3].Choice);
            Assert.IsNull(rows[3].Dial);
            Assert.AreEqual(SettingKind.Dial, rows[4].Kind);
            Assert.AreEqual("減色の強さ", rows[4].Label);
            Assert.AreSame(GameSettings.FilterTint, rows[4].Dial);
            Assert.AreEqual(SettingKind.Dial, rows[5].Kind);
            Assert.AreEqual("ディザの強さ", rows[5].Label);
            Assert.AreSame(GameSettings.FilterDots, rows[5].Dial);
            Assert.AreEqual(SettingKind.Reset, rows[6].Kind);
            Assert.AreEqual("既定に戻す", rows[6].Label);
            Assert.AreEqual("設定　　←→ で変える", ConsoleSettings.Title);
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
        public void SettingsOpensOnTheCameraSpeedAndSkipsTheHeadings()
        {
            var m = OpenSettings();
            Assert.AreEqual(1, m.Row, "小見出しの次から");
            Assert.AreSame(GameSettings.LookScale, m.RowDial);
            Assert.AreEqual(ConsoleSettings.Rows.Length, m.Rows);
            m.MoveRow(1);
            Assert.AreEqual(3, m.Row, "画面の小見出しを飛ばしてフィルターへ");
            Assert.IsNull(m.RowDial);
            Assert.AreEqual(SettingKind.Choice, m.SettingRow.Kind);
            Assert.AreSame(GameSettings.Filter, m.Settings.Choice);
            m.MoveRow(1);
            Assert.AreEqual(4, m.Row, "フィルターの下は減色の強さ");
            Assert.AreSame(GameSettings.FilterTint, m.RowDial);
            m.MoveRow(1);
            Assert.AreEqual(5, m.Row);
            Assert.AreSame(GameSettings.FilterDots, m.RowDial);
            m.MoveRow(1);
            Assert.AreEqual(6, m.Row);
            Assert.AreEqual(SettingKind.Reset, m.SettingRow.Kind);
            Assert.IsNull(m.Settings.Choice);
            m.MoveRow(1);
            Assert.AreEqual(6, m.Row, "下の端で止まる");
            m.MoveRow(-4);
            Assert.AreEqual(1, m.Row, "小見出しへは上がらない");
            m.MoveRow(-1);
            Assert.AreEqual(1, m.Row);
            m.HoverRow(0);
            Assert.AreEqual(1, m.Row, "小見出しに重ねても選ばない");
            Assert.IsFalse(m.Usable(0));
            Assert.IsFalse(m.Usable(2));
            m.HoverRow(2);
            Assert.AreEqual(1, m.Row);
            m.HoverRow(6);
            Assert.AreEqual(6, m.Row);
        }

        [Test]
        public void LeftAndRightMoveTheDialNotTheButtons()
        {
            var m = OpenSettings();
            m.Move(1);
            Assert.AreEqual(1.55f, Look.Value, 1e-6f);
            m.Move(-1);
            m.Move(-1);
            m.Move(-1);
            Assert.AreEqual(1.4f, Look.Value, 1e-6f);
            Assert.AreEqual(ConsoleAction.Settings, m.Selected, "ボタンは動かない");
            Assert.AreEqual(ConsolePanel.Settings, m.Panel, "枠は閉じない");
            // 既定に戻すの行では、左右は何もしない
            m.MoveRow(4);
            Assert.AreEqual(SettingKind.Reset, m.SettingRow.Kind);
            m.Move(1);
            m.Move(-1);
            Assert.AreEqual(1.4f, Look.Value, 1e-6f);
            Assert.AreEqual((int)ScreenFilterKind.Standard, GameSettings.Filter.Value);
            Assert.AreEqual(ConsoleAction.Settings, m.Selected);
            Assert.AreEqual(ConsolePanel.Settings, m.Panel);
        }

        [Test]
        public void LeftAndRightSwitchTheFilterOnItsRow()
        {
            var m = OpenSettings();
            m.MoveRow(1);
            Assert.AreEqual(SettingKind.Choice, m.SettingRow.Kind);
            m.Move(-1);
            Assert.AreEqual((int)ScreenFilterKind.Standard, GameSettings.Filter.Value, "左の端で止まる");
            m.Move(1);
            Assert.AreEqual((int)ScreenFilterKind.Dither, GameSettings.Filter.Value);
            Assert.AreEqual("Dither", box.Get(GameSettings.Filter.Key), "動かしたら鍵へ書く");
            m.Move(1);
            Assert.AreEqual((int)ScreenFilterKind.Dither, GameSettings.Filter.Value, "右の端で止まる");
            Assert.AreEqual(1.5f, Look.Value, "カメラの速さは動かない");
            Assert.AreEqual(ConsoleAction.Settings, m.Selected, "ボタンは動かない");
            Assert.AreEqual(ConsolePanel.Settings, m.Panel, "枠は閉じない");
            m.Move(-1);
            Assert.AreEqual((int)ScreenFilterKind.Standard, GameSettings.Filter.Value);
        }

        [Test]
        public void ResetWorksOnlyOnItsRow()
        {
            var m = OpenSettings();
            Look.Value = 1.6f;
            GameSettings.Filter.Value = (int)ScreenFilterKind.Dither;
            Assert.IsFalse(m.ResetSettings(), "つまみの行では戻さない");
            Assert.AreEqual(1.6f, Look.Value, 1e-6f);
            m.MoveRow(1);
            Assert.IsFalse(m.ResetSettings(), "フィルターの行では戻さない");
            Assert.AreEqual((int)ScreenFilterKind.Dither, GameSettings.Filter.Value);
            m.MoveRow(1);
            m.Move(-2);
            Assert.IsFalse(m.ResetSettings(), "減色の強さの行では戻さない");
            Assert.AreEqual(GameSettings.FilterTint.Snap(GameSettings.FilterTint.Default - 0.1f), GameSettings.FilterTint.Value, 1e-6f);
            m.MoveRow(2);
            Assert.IsTrue(m.ResetSettings());
            Assert.AreEqual(1.5f, Look.Value);
            Assert.AreEqual((int)ScreenFilterKind.Standard, GameSettings.Filter.Value, "フィルターも標準へ戻す");
            Assert.AreEqual(ScreenFilter.DefaultTint, GameSettings.FilterTint.Value, "強さも既定へ戻す");
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
            Assert.AreEqual(1.5f, Look.Value);
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
            Assert.AreEqual(1.55f, Look.Value, 1e-6f, "動かした値はそのまま");
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
            Assert.AreEqual(1.55f, Look.Value, 1e-6f);
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
            // 1 倍の所は溝の 3/7。その少し脇を押しても 1 倍に揃う
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
