using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>記憶の中で人や鳩を動かす線（<see cref="Mover"/>）の決まり</summary>
    public sealed class MoverTests
    {
        static Mover Make(out GameObject go, float nextSpan)
        {
            go = new GameObject("mover");
            go.hideFlags = HideFlags.HideAndDontSave;
            var mover = go.AddComponent<Mover>();
            var so = new SerializedObject(mover);
            so.FindProperty("from").vector3Value = Vector3.zero;
            so.FindProperty("to").vector3Value = new Vector3(0f, 0f, 2f);
            so.FindProperty("at").floatValue = 1f;
            so.FindProperty("span").floatValue = 1f;
            so.FindProperty("ease").boolValue = false;
            so.FindProperty("ground").boolValue = false;
            so.FindProperty("next").vector3Value = new Vector3(1f, 0f, 2f);
            so.FindProperty("nextAt").floatValue = 5f;
            so.FindProperty("nextSpan").floatValue = nextSpan;
            so.ApplyModifiedPropertiesWithoutUndo();
            return mover;
        }

        // その場で振り向く人（記憶 12 の先輩）。動き出す秒の前は置いた向き、後は振り向いた向き。
        // 背を向けたまま会話の相手をしていた、と差し戻された
        [Test]
        public void ATurnerFacesAwayUntilHerCueThenTurns()
        {
            GameObject go;
            var mover = Make(out go, 0f);
            try
            {
                var so = new SerializedObject(mover);
                so.FindProperty("turns").boolValue = true;
                so.FindProperty("yawFrom").floatValue = 20f;
                so.FindProperty("yawTo").floatValue = 182f;
                so.ApplyModifiedPropertiesWithoutUndo();
                mover.Play(0.5f);
                Assert.That(go.transform.localEulerAngles.y, Is.EqualTo(20f).Within(1e-3f));
                mover.Play(1.2f);
                Assert.That(go.transform.localEulerAngles.y, Is.EqualTo(182f).Within(1e-3f));
                mover.Play(0f);
                Assert.That(go.transform.localEulerAngles.y, Is.EqualTo(20f).Within(1e-3f), "頭から流し直しても振り向いたまま");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // 二本目が無ければ、一本目の終わりで止まったまま
        [Test]
        public void OneLineStopsAtItsEnd()
        {
            GameObject go;
            var mover = Make(out go, 0f);
            try
            {
                Assert.That(mover.Where(0f), Is.EqualTo(Vector3.zero));
                Assert.That(mover.Where(1.5f).z, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(mover.Where(9f), Is.EqualTo(new Vector3(0f, 0f, 2f)));
                Assert.That(mover.Returns, Is.False);
                Assert.That(mover.Until, Is.EqualTo(2f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // 駆け出して戻ってくる人。一本目の終わりで待ち、二本目の秒になったらそこから次の先へ
        [Test]
        public void TheSecondLineStartsWhereTheFirstEnded()
        {
            GameObject go;
            var mover = Make(out go, 2f);
            try
            {
                Assert.That(mover.Where(3f), Is.EqualTo(new Vector3(0f, 0f, 2f)), "二本目の前は一本目の終わりで待つ");
                Assert.That(mover.Where(6f).x, Is.EqualTo(0.5f).Within(1e-4f));
                Assert.That(mover.Where(20f), Is.EqualTo(new Vector3(1f, 0f, 2f)));
                Assert.That(mover.End, Is.EqualTo(new Vector3(1f, 0f, 2f)));
                Assert.That(mover.Until, Is.EqualTo(7f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
