using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>公園の鳩（<see cref="Pigeon"/>）の決まり。エディタのまま一こまずつ進めて確かめる</summary>
    public sealed class PigeonTests
    {
        /// <summary>
        /// 飛び立つ鳩を一羽。居場所 (0, 0, 0)、1 秒で飛び立って 1.5 秒で (0, 3.5, 2.5) へ上がる。
        /// Mover より先に鳩を付ける（Mover は有効になった瞬間に置き場を決めるので、そのとき鳩がいなければ自分で置く）
        /// </summary>
        static Pigeon Make(out GameObject go, out Mover mover, Pigeon.Leg leg)
        {
            go = new GameObject("dove");
            go.hideFlags = HideFlags.HideAndDontSave;
            var bird = go.AddComponent<Pigeon>();
            var sb = new SerializedObject(bird);
            sb.FindProperty("leg").enumValueIndex = (int)leg;
            sb.FindProperty("seed").intValue = 3;
            sb.FindProperty("roam").floatValue = 0.4f;
            sb.ApplyModifiedPropertiesWithoutUndo();
            mover = go.AddComponent<Mover>();
            var so = new SerializedObject(mover);
            so.FindProperty("from").vector3Value = Vector3.zero;
            so.FindProperty("to").vector3Value = new Vector3(0f, 3.5f, 2.5f);
            so.FindProperty("at").floatValue = 1f;
            so.FindProperty("span").floatValue = 1.5f;
            so.FindProperty("ease").boolValue = false;
            so.FindProperty("ground").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return bird;
        }

        /// <summary>DiveDirector の Drift と同じく、毎フレーム記憶の時計を Mover へ渡し、鳩のこまを進める</summary>
        static void Run(Mover mover, Pigeon bird, float from, float to)
        {
            const float dt = 1f / 60f;
            for (var t = from; t < to; t += dt)
            {
                mover.Play(t, -1f);
                bird.Step(dt);
            }
        }

        // 合図の前は線を運ばない。一直線に運んでいた頃は、飛び立つ前から地面の鳩が空の方へ滑っていた
        [Test]
        public void StaysOnTheGroundUntilTheTakeoff()
        {
            GameObject go;
            Mover mover;
            var bird = Make(out go, out mover, Pigeon.Leg.Leaves);
            var eye = new GameObject("eye");
            eye.hideFlags = HideFlags.HideAndDontSave;
            eye.transform.position = new Vector3(20f, 1.6f, 20f);
            Pigeon.Watcher = eye.transform;
            try
            {
                bird.Cue(0f, -1f);
                Run(mover, bird, 0f, 0.95f);
                Assert.That(go.transform.localPosition.y, Is.EqualTo(0f).Within(1e-4f), "飛び立つ前は地面");
                Assert.That(new Vector2(go.transform.localPosition.x, go.transform.localPosition.z).magnitude, Is.LessThan(0.5f), "居場所のまわりから出ない");
            }
            finally { Pigeon.Watcher = null; Object.DestroyImmediate(eye); Object.DestroyImmediate(go); }
        }

        // 羽ばたいて斜めに上がり、線の先を越えて空へ散り、しばらくして見えなくなる。上がるあいだは羽ばたきの三枚を回す
        [Test]
        public void TakesOffFlappingThenScattersAndIsGone()
        {
            GameObject go;
            Mover mover;
            var bird = Make(out go, out mover, Pigeon.Leg.Leaves);
            var eye = new GameObject("eye");
            eye.hideFlags = HideFlags.HideAndDontSave;
            eye.transform.position = new Vector3(20f, 1.6f, 20f);
            Pigeon.Watcher = eye.transform;
            try
            {
                bird.Cue(0f, -1f);
                Run(mover, bird, 0f, 1.6f);
                Assert.That(go.transform.localPosition.y, Is.GreaterThan(1f), "上がっている");
                var shapes = new System.Collections.Generic.HashSet<Pigeon.Shape>();
                const float dt = 1f / 60f;
                for (var t = 1.6f; t < 2.4f; t += dt) { mover.Play(t, -1f); bird.Step(dt); shapes.Add(bird.Current); }
                Assert.That(shapes, Has.Member(Pigeon.Shape.FlapUp).And.Member(Pigeon.Shape.FlapDown), "羽ばたく");
                Run(mover, bird, 2.4f, 3.0f);
                Assert.That(go.transform.localPosition.z, Is.GreaterThan(2.5f), "線の先を越えて散る");
                Assert.That(bird.Gone, Is.False);
                Run(mover, bird, 3.0f, 1f + 1.5f + Pigeon.GoneAfter + 0.3f);
                Assert.That(bird.Gone, Is.True, "遠い空の点を出し続けない");
            }
            finally { Pigeon.Watcher = null; Object.DestroyImmediate(eye); Object.DestroyImmediate(go); }
        }

        // 主が足元まで来たら、立ち止まっていても短く飛んで離れる。主から遠ざかる向きへ
        [Test]
        public void HopsAwayWhenThePlayerStandsOnIt()
        {
            GameObject go;
            Mover mover;
            var bird = Make(out go, out mover, Pigeon.Leg.Stays);
            var eye = new GameObject("eye");
            eye.hideFlags = HideFlags.HideAndDontSave;
            eye.transform.position = new Vector3(0.2f, 1.6f, 0f);
            Pigeon.Watcher = eye.transform;
            try
            {
                bird.Cue(0f, -1f);
                Run(mover, bird, 0f, 0.3f);
                Assert.That(bird.Doing, Is.EqualTo("Hop"), "短く飛ぶ");
                Run(mover, bird, 0.3f, 1.6f);
                Assert.That(go.transform.localPosition.x, Is.LessThan(-0.8f), "主（+x）から離れた所に降りる");
                Assert.That(go.transform.localPosition.y, Is.EqualTo(0f).Within(1e-4f), "降りている");
            }
            finally { Pigeon.Watcher = null; Object.DestroyImmediate(eye); Object.DestroyImmediate(go); }
        }

        // 立ち止まっている人のそば（足元より外）では逃げない。ついばみ続ける
        [Test]
        public void IgnoresAPlayerStandingStillNearby()
        {
            GameObject go;
            Mover mover;
            var bird = Make(out go, out mover, Pigeon.Leg.Stays);
            var eye = new GameObject("eye");
            eye.hideFlags = HideFlags.HideAndDontSave;
            eye.transform.position = new Vector3(0.9f, 1.6f, 0f);
            Pigeon.Watcher = eye.transform;
            try
            {
                bird.Cue(0f, -1f);
                for (var i = 0; i < 240; i++)
                {
                    mover.Play(i / 60f, -1f);
                    bird.Step(1f / 60f);
                    Assert.That(bird.Doing, Is.Not.EqualTo("Hop").And.Not.EqualTo("Flee"));
                }
            }
            finally { Pigeon.Watcher = null; Object.DestroyImmediate(eye); Object.DestroyImmediate(go); }
        }
    }
}
