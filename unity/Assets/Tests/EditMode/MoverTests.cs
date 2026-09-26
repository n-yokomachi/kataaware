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

        // 二本目に別の合図を持つ人（記憶 10 の孫息子）。二本目の合図が来るまでは一本目の終わりで待ち、
        // 合図からの秒で二本目を動く
        [Test]
        public void TheSecondLineWaitsForItsOwnCue()
        {
            GameObject go;
            var mover = Make(out go, 2f);
            try
            {
                var so = new SerializedObject(mover);
                so.FindProperty("nextCue").intValue = 6;
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(mover.NextCue, Is.EqualTo(6));
                Assert.That(mover.Where(30f, -1f), Is.EqualTo(new Vector3(0f, 0f, 2f)), "二本目の合図の前は一本目の終わりで待つ");
                Assert.That(mover.Where(30f, 6f).x, Is.EqualTo(0.5f).Within(1e-4f), "二本目は合図からの秒で動く");
                Assert.That(mover.Where(30f, 20f), Is.EqualTo(new Vector3(1f, 0f, 2f)));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // 歩いているあいだだけ Moving。動き出す前・一本目と二本目のあいだ・歩き終えた後は止まっている
        [Test]
        public void MovingOnlyWhileOnALine()
        {
            GameObject go;
            var mover = Make(out go, 2f);
            try
            {
                Assert.That(mover.Moving(0.5f, 0.5f), Is.False, "動き出す前");
                Assert.That(mover.Moving(1.5f, 1.5f), Is.True, "一本目");
                Assert.That(mover.Moving(3f, 3f), Is.False, "一本目と二本目のあいだ");
                Assert.That(mover.Moving(6f, 6f), Is.True, "二本目");
                Assert.That(mover.Moving(20f, 20f), Is.False, "歩き終えた後");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // 向き直る人。歩くあいだは歩く向き、歩き終えたら相手の方、二本目を歩き出したらその向き
        [Test]
        public void ASettlerTurnsToFaceWhenSheArrives()
        {
            GameObject go;
            var mover = Make(out go, 2f);
            try
            {
                var so = new SerializedObject(mover);
                so.FindProperty("turns").boolValue = true;
                so.FindProperty("yawFrom").floatValue = 10f;
                so.FindProperty("yawTo").floatValue = 90f;
                so.FindProperty("settles").boolValue = true;
                so.FindProperty("yawEnd").floatValue = 200f;
                so.FindProperty("yawNext").floatValue = 300f;
                so.ApplyModifiedPropertiesWithoutUndo();
                mover.Play(0.5f);
                Assert.That(go.transform.localEulerAngles.y, Is.EqualTo(10f).Within(0.01f));
                mover.Play(1.5f);
                Assert.That(go.transform.localEulerAngles.y, Is.EqualTo(90f).Within(0.01f));
                mover.Play(3f);
                Assert.That(go.transform.localEulerAngles.y, Is.EqualTo(200f).Within(0.01f));
                mover.Play(6f);
                Assert.That(go.transform.localEulerAngles.y, Is.EqualTo(300f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
