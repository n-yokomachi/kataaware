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
        public void MirrorFoldsAPointAcrossTheScreen()
        {
            var p = TerminalReflection.Mirror(new Vector3(0.2f, 1.2f, -1.0f), new Vector3(0f, 1f, 0.5f), new Vector3(0f, 0f, -1f));
            Assert.AreEqual(0.2f, p.x, 1e-5f);
            Assert.AreEqual(1.2f, p.y, 1e-5f);
            Assert.AreEqual(2.0f, p.z, 1e-5f, "画面の手前 1.5 m の目は、画面の奥 1.5 m に映る");
        }

        [Test]
        public void WindowIsTheScreenSeenFromTheMirroredEye()
        {
            // 画面の奥 1.5 m から、画面（真ん中は右へ 0.1 m、上へ 0.05 m）を見る
            var at = new Vector3(0f, 1f, 2f);
            var rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            var w = TerminalReflection.Window(at, rotation, new Vector3(-0.1f, 1.05f, 0.5f), new Vector2(0.8f, 0.4f));
            // カメラは -z を向くので、カメラの右は世界の -x
            Assert.AreEqual(0.1f - 0.4f, w.x, 1e-5f);
            Assert.AreEqual(0.1f + 0.4f, w.y, 1e-5f);
            Assert.AreEqual(0.05f - 0.2f, w.z, 1e-5f);
            Assert.AreEqual(0.05f + 0.2f, w.w, 1e-5f);
        }
    }
}
