using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// コンソールのデバッグ（場面の一覧のボタンと、数字で場面へ飛ぶ操作）は、エディタと開発用の書き出し（Development Build）だけで出す。
    /// 公開用の書き出しの形は <see cref="ConsoleMenu.DebugOverride"/> で作る
    /// </summary>
    public class ConsoleDebugTests
    {
        [TearDown]
        public void Clear()
        {
            ConsoleMenu.DebugOverride = null;
        }

        [Test]
        public void TheEditorShowsDebug()
        {
            // エディタでは Debug.isDebugBuild が真。書き出しでは Development Build の時だけ真
            Assert.IsTrue(Debug.isDebugBuild);
            Assert.IsTrue(ConsoleMenu.DebugShown);
            CollectionAssert.Contains(ConsoleMenu.Labels, ConsoleMenu.DebugLabel);
        }

        [Test]
        public void ADevelopmentBuildShowsDebugAndJumps()
        {
            ConsoleMenu.DebugOverride = true;
            CollectionAssert.AreEqual(new[] { "記憶する", "思い出す", "目を閉じる", "設定", "デバッグ" }, ConsoleMenu.Labels);
            var m = new ConsoleMenu();
            m.Move(5);
            Assert.AreEqual(ConsoleAction.Debug, m.Selected);
            Assert.AreEqual(ConsoleAction.Debug, m.Decide("Room"));
            Assert.IsTrue(m.Listing);
            Assert.AreEqual("Alley", ConsoleMenu.DigitTarget(2, "Room"));
        }

        [Test]
        public void AReleaseBuildHidesTheDebugButton()
        {
            ConsoleMenu.DebugOverride = false;
            Assert.IsFalse(ConsoleMenu.DebugShown);
            CollectionAssert.AreEqual(new[] { "記憶する", "思い出す", "目を閉じる", "設定" }, ConsoleMenu.Labels);
        }

        [Test]
        public void AReleaseBuildCannotReachTheSceneList()
        {
            ConsoleMenu.DebugOverride = false;
            var m = new ConsoleMenu();
            m.Move(5);
            Assert.AreEqual(ConsoleAction.Settings, m.Selected, "右の端は設定");
            m.Hover((int)ConsoleAction.Debug);
            Assert.AreEqual(ConsoleAction.Settings, m.Selected, "無いボタンには重ならない");
            Assert.AreEqual(ConsoleAction.Settings, m.Decide("Room"));
            Assert.AreEqual(ConsolePanel.Settings, m.Panel, "設定は公開用の書き出しでも開く");
            Assert.IsFalse(m.Listing);
            Assert.IsNull(m.RowTarget("Room"));
        }

        [Test]
        public void AReleaseBuildIgnoresTheDigits()
        {
            ConsoleMenu.DebugOverride = false;
            for (var d = 1; d <= SceneMenu.Keys; d++) Assert.IsNull(ConsoleMenu.DigitTarget(d, "Room"), "数字 " + d);
        }
    }
}
