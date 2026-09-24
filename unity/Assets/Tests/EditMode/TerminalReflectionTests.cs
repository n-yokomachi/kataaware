using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class TerminalReflectionTests
    {
        static readonly string[] Lines =
        {
            "世間ではホロコンソールが人気だが私はもっぱら物理モニターを使っている",
            "というのもほら、",
            "こうして反射で自分の顔が見られるからだ",
            "黒い髪に琥珀色の目。左目の下のほくろ",
            "抜け出した後の自己同定のために、鏡を見ることは大切だ",
        };

        [Test]
        public void HiddenBeforeTheThirdLine()
        {
            Assert.IsFalse(TerminalReflection.Showing(Lines, Lines[0], 2));
            Assert.IsFalse(TerminalReflection.Showing(Lines, Lines[1], 2));
        }

        [Test]
        public void ShownFromTheThirdLineToTheLast()
        {
            for (var i = 2; i < Lines.Length; i++)
                Assert.IsTrue(TerminalReflection.Showing(Lines, Lines[i], 2), "行 " + i);
        }

        [Test]
        public void HiddenWhenNothingIsSaidOrAnotherTextIsSaid()
        {
            Assert.IsFalse(TerminalReflection.Showing(Lines, null, 2));
            Assert.IsFalse(TerminalReflection.Showing(Lines, "ほかの対象の文", 2));
            Assert.IsFalse(TerminalReflection.Showing(null, Lines[2], 2));
        }

        [Test]
        public void MouthIsBelowWhereTheEyeIsReflected()
        {
            var spot = TerminalReflection.Spot(new Vector2(0.05f, 0.10f), 0.11f, new Vector2(0.81f, 0.48f), new Vector2(0.15f, 0.09f));
            Assert.AreEqual(0.05f, spot.x, 1e-5f);
            Assert.AreEqual(-0.01f, spot.y, 1e-5f);
        }

        [Test]
        public void StaysInsideTheScreen()
        {
            var screen = new Vector2(0.81f, 0.48f);
            var size = new Vector2(0.15f, 0.09f);
            // 立って見下ろすと、口元の映る所は画面の上の外になる。画面の上の縁の内へ寄せる
            var high = TerminalReflection.Spot(new Vector2(0.9f, 0.6f), 0.11f, screen, size);
            Assert.AreEqual((0.81f - 0.15f) * 0.5f, high.x, 1e-5f);
            Assert.AreEqual((0.48f - 0.09f) * 0.5f, high.y, 1e-5f);
            var low = TerminalReflection.Spot(new Vector2(-0.9f, -0.6f), 0.11f, screen, size);
            Assert.AreEqual(-(0.81f - 0.15f) * 0.5f, low.x, 1e-5f);
            Assert.AreEqual(-(0.48f - 0.09f) * 0.5f, low.y, 1e-5f);
        }
    }
}
