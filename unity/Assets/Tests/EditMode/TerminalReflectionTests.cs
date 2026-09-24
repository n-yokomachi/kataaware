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
        public void FramingLooksAtTheRangeFromTheFront()
        {
            var centre = new Vector3(1f, 1.2f, 2f);
            var pose = TerminalReflection.Framing(centre, Quaternion.identity, 0f, 0f, 0.6f);
            Assert.AreEqual(1f, pose.position.x, 1e-5f);
            Assert.AreEqual(1.2f, pose.position.y, 1e-5f);
            Assert.AreEqual(2.6f, pose.position.z, 1e-5f, "体の正面（+z）に 0.6 m 離れる");
            Assert.AreEqual(-1f, (pose.rotation * Vector3.forward).z, 1e-5f, "体の方を向く");
        }

        [Test]
        public void FramingTurnsToTheRightAndLooksUpFromBelow()
        {
            var centre = Vector3.zero;
            var right = TerminalReflection.Framing(centre, Quaternion.identity, 90f, 0f, 1f);
            Assert.AreEqual(1f, right.position.x, 1e-5f, "yaw が正なら体の右から");
            var below = TerminalReflection.Framing(centre, Quaternion.identity, 0f, -30f, 1f);
            Assert.Less(below.position.y, 0f, "pitch が負なら下から");
            Assert.Greater((below.rotation * Vector3.forward).y, 0f, "見上げる");
            Assert.AreEqual(1f, (below.position - centre).magnitude, 1e-5f);
        }
    }
}
