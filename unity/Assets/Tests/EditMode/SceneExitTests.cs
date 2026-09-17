using NUnit.Framework;

namespace HalfAware.Tests
{
    public class SceneExitTests
    {
        [Test]
        public void WithNoNextSceneItStops()
        {
            Assert.IsFalse(SceneExit.Continues(null));
            Assert.IsFalse(SceneExit.Continues(""));
            Assert.IsFalse(SceneExit.Continues("   "));
        }

        [Test]
        public void WithANextSceneItGoesOn()
        {
            Assert.IsTrue(SceneExit.Continues("Alley"));
        }

        [Test]
        public void StoppingIsTheOnlyTimeItFadesOut()
        {
            Assert.IsTrue(SceneExit.FadesOut(""), "止まるときだけ暗転する");
            Assert.IsFalse(SceneExit.FadesOut("Alley"), "場面のつなぎは暗転を挟まない");
        }

        [Test]
        public void TheTargetLosesItsSpaces()
        {
            Assert.AreEqual("Alley", SceneExit.Target("  Alley  "));
        }

        [Test]
        public void ThereIsNoTargetWhenItStops()
        {
            Assert.IsNull(SceneExit.Target(""));
            Assert.IsNull(SceneExit.Target(null));
        }
    }
}
