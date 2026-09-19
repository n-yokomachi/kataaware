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

        [Test]
        public void ListsEveryScene()
        {
            var text = SceneMenu.Compose("Room");
            for (var i = 0; i < SceneMenu.Count; i++)
            {
                Assert.That(text, Does.Contain(SceneMenu.Titles[i]));
                Assert.That(text, Does.Contain((i + 1).ToString()));
            }
        }

        [Test]
        public void MarksWhereYouAre()
        {
            Assert.That(SceneMenu.Compose("Alley"), Does.Contain("いま"));
            // 一覧に無い場面から開いても落ちない。印が付かないだけ
            Assert.That(SceneMenu.Compose("Nowhere"), Does.Not.Contain("いま"));
        }

        [Test]
        public void TellsHowToClose()
        {
            Assert.That(SceneMenu.Compose("Room"), Does.Contain("Tab"));
        }
    }
}
