using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// タイトルの画面の設定（設計書 5 節）。ボタンの並びと上下、設定の枠の行（コンソールと同じ表に「戻る」）、
    /// コンソールとタイトルの画面が同じ設定の枠（SettingsPanel）を使うこと
    /// </summary>
    public class TitleSettingsTests
    {
        [SetUp]
        public void Swap()
        {
            GameSettings.Box = new MemoryBox();
        }

        [TearDown]
        public void Restore()
        {
            GameSettings.Box = null;
        }

        [Test]
        public void TheTitleButtonsAreBeginRecallAndSettings()
        {
            CollectionAssert.AreEqual(new[] { "はじめる", "思い出す", "設定" }, TitleScreen.Labels);
            Assert.AreEqual("設定", TitleScreen.Labels[TitleScreen.SettingsButton]);
            Assert.IsTrue(TitleScreen.ButtonUsable(TitleScreen.SettingsButton, false), "設定はセーブが無くても押せる");
            Assert.IsTrue(TitleScreen.ButtonUsable(TitleScreen.BeginButton, false));
            Assert.IsFalse(TitleScreen.ButtonUsable(TitleScreen.RecallButton, false));
            Assert.IsTrue(TitleScreen.ButtonUsable(TitleScreen.RecallButton, true));
            Assert.IsFalse(TitleScreen.ButtonUsable(3, true));
            Assert.IsFalse(TitleScreen.ButtonUsable(-1, true));
        }

        [Test]
        public void WithoutSavesUpAndDownSkipRecall()
        {
            Assert.AreEqual(TitleScreen.SettingsButton, TitleScreen.NextButton(TitleScreen.BeginButton, 1, false));
            Assert.AreEqual(TitleScreen.SettingsButton, TitleScreen.NextButton(TitleScreen.SettingsButton, 1, false), "下の端で止まる");
            Assert.AreEqual(TitleScreen.BeginButton, TitleScreen.NextButton(TitleScreen.SettingsButton, -1, false));
            Assert.AreEqual(TitleScreen.BeginButton, TitleScreen.NextButton(TitleScreen.BeginButton, -1, false), "上の端で止まる");
        }

        [Test]
        public void WithSavesEveryButtonIsReached()
        {
            Assert.AreEqual(TitleScreen.RecallButton, TitleScreen.NextButton(TitleScreen.BeginButton, 1, true));
            Assert.AreEqual(TitleScreen.SettingsButton, TitleScreen.NextButton(TitleScreen.RecallButton, 1, true));
            Assert.AreEqual(TitleScreen.RecallButton, TitleScreen.NextButton(TitleScreen.SettingsButton, -1, true));
        }

        [Test]
        public void TheTitleSettingsAreTheConsoleRowsWithBack()
        {
            var rows = TitleScreen.SettingRows;
            Assert.AreEqual(ConsoleSettings.Rows.Length + 1, rows.Length);
            for (var i = 0; i < ConsoleSettings.Rows.Length; i++)
                Assert.AreSame(ConsoleSettings.Rows[i], rows[i], "コンソールと同じ行");
            Assert.AreEqual(SettingKind.Back, rows[rows.Length - 1].Kind);
            Assert.AreEqual("戻る", rows[rows.Length - 1].Label);
            Assert.AreEqual(3, ConsoleSettings.Rows.Length, "コンソールの表は戻るを持たない");
        }

        [Test]
        public void OnTheTitleTheRowsRunDownToBack()
        {
            var list = new SettingsList(TitleScreen.SettingRows);
            list.Open();
            Assert.AreEqual(1, list.Row, "小見出しの次から");
            Assert.IsTrue(list.Nudge(-2));
            Assert.AreEqual(0.9f, GameSettings.LookScale.Value, 1e-6f);
            list.MoveRow(1);
            Assert.AreEqual(SettingKind.Reset, list.Selected.Kind);
            list.MoveRow(1);
            Assert.IsTrue(list.AtBack);
            Assert.IsFalse(list.Reset(), "戻るでは既定に戻さない");
            Assert.IsFalse(list.Nudge(1), "戻るでは左右は何もしない");
            list.MoveRow(1);
            Assert.IsTrue(list.AtBack, "下の端で止まる");
            list.MoveRow(-1);
            Assert.IsTrue(list.Reset());
            Assert.AreEqual(1f, GameSettings.LookScale.Value);
            list.MoveRow(-5);
            Assert.AreEqual(1, list.Row, "小見出しへは上がらない");
        }

        [Test]
        public void ThePanelIsTheSameForTheConsoleAndTheTitle()
        {
            var root = new GameObject("SettingsPanelTest", typeof(RectTransform), typeof(Canvas));
            try
            {
                var console = new SettingsPanel(new SettingsList(ConsoleSettings.Rows));
                var title = new SettingsPanel(new SettingsList(TitleScreen.SettingRows));
                var a = console.Build(root.transform, ConsoleSettings.Title, null);
                var b = title.Build(root.transform, ConsoleSettings.Title, null);
                Assert.AreEqual(SettingsPanel.Width, a.sizeDelta.x);
                Assert.AreEqual(SettingsPanel.Width, b.sizeDelta.x);
                Assert.Greater(b.sizeDelta.y, a.sizeDelta.y, "タイトルの画面は戻るの一行ぶん高い");
                Assert.IsFalse(a.gameObject.activeSelf, "閉じた形で組む");
                // 値を動かすと、どちらもつまみの位置と倍率の字が同じように変わる
                GameSettings.LookScale.Value = 0.5f;
                foreach (var p in new[] { console, title })
                {
                    p.List.Open();
                    p.Paint();
                    Assert.AreEqual(GameSettings.LookScale.Fraction(0.5f), p.KnobAt(1), 1e-6f);
                    StringAssert.Contains("0.50", p.TextAt(1));
                    Assert.AreEqual(-1f, p.KnobAt(0), "小見出しにつまみは無い");
                }
                Assert.AreEqual("戻る", title.TextAt(3));
                // 押す: 小見出しは何もしない、既定に戻すは戻す、戻るは呼び手へ知らせる
                var backs = 0;
                title.Back = () => backs++;
                title.Press(0);
                Assert.AreEqual(1, title.List.Row);
                title.Press(2);
                Assert.AreEqual(1f, GameSettings.LookScale.Value);
                Assert.AreEqual(0, backs);
                title.Press(3);
                Assert.AreEqual(1, backs);
                console.Back = () => backs++;
                console.Press(2);
                Assert.AreEqual(1, backs, "コンソールには戻るの行が無い");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
