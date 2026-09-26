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
            Assert.That(SceneMenu.Pick(6), Is.EqualTo("Drive"));
            Assert.That(SceneMenu.Pick(7), Is.EqualTo("Village"));
        }

        // 数字は鍵盤から直に読んでいて、読んでいるのは 1〜9。
        // それより多く並べると、一覧に出ているのに押せない番号ができる
        [Test]
        public void EveryNumberCanBeTyped()
        {
            Assert.That(SceneMenu.Count, Is.LessThanOrEqualTo(9));
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
                Assert.That(built, Does.Contain(name), name + " が組み立ての一覧に無い");
        }
    }
}
