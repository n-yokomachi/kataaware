using NUnit.Framework;

namespace HalfAware.Tests
{
    public class SceneMenuTests
    {
        [Test]
        public void TitlesAndScenesLineUp()
        {
            Assert.That(SceneMenu.Titles.Length, Is.EqualTo(SceneMenu.Scenes.Length));
            Assert.That(SceneMenu.Count, Is.EqualTo(SceneMenu.Scenes.Length));
        }

        [Test]
        public void PicksBySpokenNumber()
        {
            Assert.That(SceneMenu.Pick(1), Is.EqualTo("Room"));
            Assert.That(SceneMenu.Pick(2), Is.EqualTo("Alley"));
            Assert.That(SceneMenu.Pick(3), Is.EqualTo("Connect"));
            Assert.That(SceneMenu.Pick(4), Is.EqualTo("Dive"));
            Assert.That(SceneMenu.Pick(5), Is.EqualTo("Rest"));
            // 小休止より後ろは、場面を足すと番号がずれる。物語の順に並んでいるかを前後で見る
            var order = new System.Collections.Generic.List<string>(SceneMenu.Scenes);
            Assert.That(order.IndexOf("Notice"), Is.GreaterThan(order.IndexOf("Rest")), "自室・気づきは小休止の後");
            Assert.That(order.IndexOf("Drive"), Is.GreaterThan(order.IndexOf("Notice")), "車内は自室・気づきの後");
            Assert.That(order.IndexOf("Village"), Is.GreaterThan(order.IndexOf("Drive")), "村は車内の後");
            Assert.That(SceneMenu.Pick(SceneMenu.Count), Is.EqualTo(SceneMenu.Reunion), "終わりは対面");
        }

        // 数字は鍵盤から直に読んでいて、読んでいるのは 1〜9 と 0（0 は 10 行目）。
        // それより多く並べると、一覧に出ているのに押せない番号ができる
        [Test]
        public void EveryNumberCanBeTyped()
        {
            Assert.That(SceneMenu.Count, Is.LessThanOrEqualTo(SceneMenu.Keys));
            for (var i = 0; i < SceneMenu.Count; i++)
                Assert.That(SceneMenu.KeyOf(i), Is.Not.Empty, SceneMenu.Titles[i] + " の鍵が無い");
            Assert.That(SceneMenu.KeyOf(9), Is.EqualTo("0"));
            Assert.That(SceneMenu.KeyOf(10), Is.Empty);
        }

        [Test]
        public void NoPickOutsideTheList()
        {
            Assert.That(SceneMenu.Pick(0), Is.Null);
            Assert.That(SceneMenu.Pick(-1), Is.Null);
            Assert.That(SceneMenu.Pick(SceneMenu.Count + 1), Is.Null);
        }

        [Test]
        public void StayingPutIsNotATarget()
        {
            Assert.That(SceneMenu.Target(1, "Room"), Is.Null);
            Assert.That(SceneMenu.Target(2, "Room"), Is.EqualTo("Alley"));
            Assert.That(SceneMenu.Target(0, "Room"), Is.Null);
        }

        // 組み立ての一覧に入っていないシーンは SceneManager.LoadScene が読めない。
        // 数字を押した先で落ちるので、並べたものは全部入っていること
        [Test]
        public void EveryListedSceneIsInTheBuild()
        {
            var built = new System.Collections.Generic.List<string>();
            foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
                built.Add(System.IO.Path.GetFileNameWithoutExtension(scene.path));
            foreach (var name in SceneMenu.Scenes)
                Assert.That(built, Does.Contain(SceneMenu.SceneOf(name)), name + " が組み立ての一覧に無い");
        }
    }
}
