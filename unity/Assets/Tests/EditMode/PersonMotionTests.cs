using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>記憶の中の人の動き（<see cref="PersonMotion"/>）の決まり</summary>
    public sealed class PersonMotionTests
    {
        [Test]
        public void StillBelowTheFloorSpeed()
        {
            Assert.That(PersonMotion.Choose(0f, 1.2f, PersonMotion.Gait.Walk, true), Is.EqualTo(PersonMotion.Gait.Stand));
            Assert.That(PersonMotion.Choose(PersonMotion.StandBelow * 0.5f, 1.2f, PersonMotion.Gait.Run, true),
                Is.EqualTo(PersonMotion.Gait.Stand));
        }

        [Test]
        public void WalksAtItsOwnPace()
        {
            Assert.That(PersonMotion.Choose(1.2f, 1.2f, PersonMotion.Gait.Stand, true), Is.EqualTo(PersonMotion.Gait.Walk));
            Assert.That(PersonMotion.Choose(0.2f, 1.2f, PersonMotion.Gait.Stand, true), Is.EqualTo(PersonMotion.Gait.Walk));
        }

        [Test]
        public void RunsWhenFarFasterThanItsWalk()
        {
            Assert.That(PersonMotion.Choose(1.2f * PersonMotion.RunEnter + 0.01f, 1.2f, PersonMotion.Gait.Walk, true),
                Is.EqualTo(PersonMotion.Gait.Run));
            Assert.That(PersonMotion.Choose(5f, 1.2f, PersonMotion.Gait.Walk, false), Is.EqualTo(PersonMotion.Gait.Walk),
                "走りの動きが無ければ歩く");
        }

        // 境目で歩きと走りが行き来しないよう、戻る速さは移る速さより低い
        [Test]
        public void RunningKeepsRunningBetweenTheTwoLines()
        {
            var between = 1.2f * (PersonMotion.RunEnter + PersonMotion.RunLeave) * 0.5f;
            Assert.That(PersonMotion.Choose(between, 1.2f, PersonMotion.Gait.Run, true), Is.EqualTo(PersonMotion.Gait.Run));
            Assert.That(PersonMotion.Choose(between, 1.2f, PersonMotion.Gait.Walk, true), Is.EqualTo(PersonMotion.Gait.Walk));
        }

        [Test]
        public void SlowWalkersShortenTheirStride()
        {
            Assert.That(PersonMotion.ReachOf(1.2f, 1.2f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(PersonMotion.ReachOf(3f, 1.2f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(PersonMotion.ReachOf(0.3f, 1.2f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(PersonMotion.ReachOf(0.01f, 1.2f), Is.EqualTo(PersonMotion.Reaches[0]));
        }

        [Test]
        public void StrideIsReadBetweenTheMeasuredSteps()
        {
            var table = new[] { 0.3f, 0.5f, 0.7f, 0.9f, 1.1f };
            Assert.That(PersonMotion.StrideAt(table, 0.1f), Is.EqualTo(0.3f));
            Assert.That(PersonMotion.StrideAt(table, 1f), Is.EqualTo(1.1f));
            Assert.That(PersonMotion.StrideAt(table, 0.575f), Is.EqualTo(0.6f).Within(1e-4f));
        }

        [Test]
        public void TicksComeTwelveASecond()
        {
            Assert.That(PersonMotion.Fps, Is.InRange(10f, 12f));
            Assert.That(PersonMotion.TicksIn(PersonMotion.Tick * 0.9f), Is.EqualTo(0));
            Assert.That(PersonMotion.TicksIn(PersonMotion.Tick), Is.EqualTo(1));
            Assert.That(PersonMotion.TicksIn(PersonMotion.Tick * 2.5f), Is.EqualTo(2));
        }

        // ---- 目線 ------------------------------------------------------------------------

        // 主が上限の内にいれば、首と頭はそのまま主の目へ向く
        [Test]
        public void TheHeadTurnsToThePlayerWithinReach()
        {
            var rest = new Vector2(0f, 20f);
            Assert.That(PersonMotion.GazeAt(new Vector2(10f, 30f), rest), Is.EqualTo(new Vector2(10f, 30f)));
            Assert.That(PersonMotion.GazeAt(new Vector2(-20f, -10f), rest), Is.EqualTo(new Vector2(-20f, -10f)));
        }

        // 上限の外は上限で止める。首が折れて見えない
        [Test]
        public void TheHeadStopsAtItsLimits()
        {
            var rest = Vector2.zero;
            Assert.That(PersonMotion.GazeAt(new Vector2(80f, 0f), rest).x, Is.EqualTo(PersonMotion.GazeSide));
            Assert.That(PersonMotion.GazeAt(new Vector2(-80f, 0f), rest).x, Is.EqualTo(-PersonMotion.GazeSide));
            Assert.That(PersonMotion.GazeAt(new Vector2(0f, 70f), rest).y, Is.EqualTo(PersonMotion.GazeDown));
            Assert.That(PersonMotion.GazeAt(new Vector2(0f, -30f), rest).y, Is.EqualTo(-PersonMotion.GazeUp));
        }

        // 主が後ろ寄りや真上に近ければ見るのをやめ、姿勢の顔の向き（机のノートなど）に戻す
        [Test]
        public void TheHeadLetsGoWhenThePlayerIsBehindOrRightAbove()
        {
            var rest = new Vector2(0f, 30f);
            Assert.That(PersonMotion.GazeAt(new Vector2(150f, 0f), rest), Is.EqualTo(rest));
            Assert.That(PersonMotion.GazeAt(new Vector2(0f, -PersonMotion.GazeUpRelease - 1f), rest), Is.EqualTo(rest));
        }

        // ---- 動かしてみる ----------------------------------------------------------------

        static AnimationClip Clip(float length)
        {
            var clip = new AnimationClip();
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, length, 0f));
            return clip;
        }

        static PersonMotion Walker(out GameObject go)
        {
            go = new GameObject("walker");
            go.hideFlags = HideFlags.HideAndDontSave;
            var motion = go.AddComponent<PersonMotion>();
            var so = new SerializedObject(motion);
            so.FindProperty("idle").objectReferenceValue = Clip(2f);
            so.FindProperty("walk").objectReferenceValue = Clip(1.5f);
            var row = so.FindProperty("strides");
            row.arraySize = PersonMotion.Reaches.Length;
            for (var i = 0; i < row.arraySize; i++) row.GetArrayElementAtIndex(i).floatValue = 1.5f * PersonMotion.Reaches[i];
            so.FindProperty("lag").floatValue = 0.25f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return motion;
        }

        // 足の運びは時計でなく進んだ距離で進む。一周期の進みぶん歩けば周期がちょうど一回りする
        [Test]
        public void TheCycleFollowsTheDistanceWalked()
        {
            GameObject go;
            var motion = Walker(out go);
            try
            {
                motion.Restart();
                motion.Late();
                // 自然な速さ（1 m/s）で 0.75 m。歩幅は詰めないので、一周期 1.5 m の半分
                var step = 1f * PersonMotion.Tick;
                for (var i = 0; i < 9; i++)
                {
                    go.transform.position += Vector3.forward * step;
                    motion.Step(PersonMotion.Tick);
                    motion.Late();
                }
                Assert.That(motion.Current, Is.EqualTo(PersonMotion.Gait.Walk));
                Assert.That(motion.Reach, Is.EqualTo(1f).Within(1e-3f));
                Assert.That(motion.Phase, Is.EqualTo(9f * step / 1.5f).Within(1e-3f));
            }
            finally
            {
                motion.Close();
                Object.DestroyImmediate(go);
            }
        }

        // こまのあいだは模型が止まっている。根が進んでも模型はこまの頭の置き場に留まる
        [Test]
        public void TheBodyWaitsForTheNextTick()
        {
            GameObject go;
            var motion = Walker(out go);
            var body = new GameObject("body").transform;
            body.SetParent(go.transform, false);
            var so = new SerializedObject(motion);
            so.FindProperty("body").objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                motion.Restart();
                motion.Late();
                var held = body.position;
                go.transform.position += Vector3.forward * 0.02f;
                motion.Step(PersonMotion.Tick * 0.3f);
                motion.Late();
                Assert.That(body.position, Is.EqualTo(held), "こまの途中で模型が動いた");
                go.transform.position += Vector3.forward * 0.02f;
                motion.Step(PersonMotion.Tick);
                motion.Late();
                Assert.That(body.position.z, Is.EqualTo(go.transform.position.z).Within(1e-4f), "こまの頭で追いついていない");
            }
            finally
            {
                motion.Close();
                Object.DestroyImmediate(go);
            }
        }

        // 同じ記憶へ入り直すと、動きも頭から。周期も立ちの時計も、組み立てで決めたずらしの所へ戻る
        [Test]
        public void EnteringAgainStartsFromTheTop()
        {
            GameObject go;
            var motion = Walker(out go);
            try
            {
                motion.Restart();
                motion.Late();
                var first = motion.IdleTime;
                for (var i = 0; i < 20; i++)
                {
                    go.transform.position += Vector3.forward * 0.1f;
                    motion.Step(PersonMotion.Tick);
                    motion.Late();
                }
                Assert.That(motion.Phase, Is.Not.EqualTo(0f));
                motion.Close();
                motion.Restart();
                motion.Late();
                Assert.That(motion.Phase, Is.EqualTo(0f));
                Assert.That(motion.Current, Is.EqualTo(PersonMotion.Gait.Stand));
                Assert.That(motion.IdleTime, Is.EqualTo(first));
                Assert.That(first, Is.EqualTo(0.25f * 2f).Within(1e-5f));
            }
            finally
            {
                motion.Close();
                Object.DestroyImmediate(go);
            }
        }
    }
}
