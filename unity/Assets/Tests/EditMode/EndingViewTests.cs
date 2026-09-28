using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAware.Tests
{
    /// <summary>
    /// エンディングの見回しの限り（EndingView）。角度の限りの重さと止まり方、左の限りで片割れの体が画面に入らないこと
    /// </summary>
    public class EndingViewTests
    {
        static readonly Vector3[] None = new Vector3[0];

        [Test]
        public void TheRightEdgeGetsHeavyAndStopsWithoutSpringingBack()
        {
            var v = EndingView.Default;
            var far = v.Step(new Vector2(v.right - 30f, v.front.y), new Vector2(1f, 0f), Vector3.zero, None, 16f / 9f, 70f);
            Assert.That(far.x - (v.right - 30f), Is.EqualTo(1f).Within(1e-4f), "限りから遠い所では重くならない");
            var near = v.Step(new Vector2(v.right - 2f, v.front.y), new Vector2(1f, 0f), Vector3.zero, None, 16f / 9f, 70f);
            Assert.That(near.x - (v.right - 2f), Is.LessThan(0.6f), "限りの手前では重い");
            var at = v.front;
            for (var i = 0; i < 400; i++) at = v.Step(at, new Vector2(3f, 0f), Vector3.zero, None, 16f / 9f, 70f);
            Assert.That(at.x, Is.EqualTo(v.right).Within(1e-3f), "限りまで回って止まる");
            var still = v.Step(at, Vector2.zero, Vector3.zero, None, 16f / 9f, 70f);
            Assert.That(still, Is.EqualTo(at), "手を離しても戻らない");
            var back = v.Step(at, new Vector2(-1f, 0f), Vector3.zero, None, 16f / 9f, 70f);
            Assert.That(at.x - back.x, Is.EqualTo(1f).Within(1e-4f), "限りから離れる向きは重くない");
        }

        [Test]
        public void UpAndDownStopAtTheirLimits()
        {
            var v = EndingView.Default;
            var at = v.front;
            for (var i = 0; i < 400; i++) at = v.Step(at, new Vector2(0f, 3f), Vector3.zero, None, 16f / 9f, 70f);
            Assert.That(at.y, Is.EqualTo(v.down).Within(1e-3f));
            for (var i = 0; i < 400; i++) at = v.Step(at, new Vector2(0f, -3f), Vector3.zero, None, 16f / 9f, 70f);
            Assert.That(at.y, Is.EqualTo(v.up).Within(1e-3f));
        }

        [Test]
        public void APointInFrontIsInsideAndAPointBehindIsOutside()
        {
            var ahead = new[] { new Vector3(0f, 0f, 5f) };
            Assert.That(EndingView.Clearance(Vector2.zero, Vector3.zero, ahead, 16f / 9f, 70f), Is.LessThan(0f));
            var behind = new[] { new Vector3(0f, 0f, -5f) };
            Assert.That(EndingView.Clearance(Vector2.zero, Vector3.zero, behind, 16f / 9f, 70f), Is.GreaterThan(90f));
            // 真左 90 度の点は、横の画角の半分（16:9 で 51 度ほど）の外に 39 度ほど残る
            var side = new[] { new Vector3(-5f, 0f, 0f) };
            var c = EndingView.Clearance(Vector2.zero, Vector3.zero, side, 16f / 9f, 70f);
            Assert.That(c, Is.EqualTo(90f - Mathf.Atan(Mathf.Tan(35f * Mathf.Deg2Rad) * 16f / 9f) * Mathf.Rad2Deg).Within(0.01f));
        }

        /// <summary>
        /// 組み上がったエンディングの場面で、片割れの座った体の全部の頂点が、見回せるどの向きでも画面の外にあること。
        /// 左・左下・左上・下から左など、いくつもの向きへ回し続け、止まるまでの道の途中を全部の頂点で確かめる。
        /// 縦横比は 4:3・16:9・21:9。組み立て前（片割れのメッシュが無い）なら調べない
        /// </summary>
        [Test]
        public void TheTwinNeverEntersTheFrameWhileLookingAround()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) Assert.Ignore("開いているシーンに未保存の変更がある");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Ending.unity", OpenSceneMode.Single);
                var d = Object.FindFirstObjectByType<EndingDirector>(FindObjectsInactive.Include);
                Assert.That(d, Is.Not.Null);
                var twin = GameObject.Find("Ending/Twin");
                var verts = new List<Vector3>();
                if (twin != null)
                    foreach (var mf in twin.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var r = mf.GetComponent<MeshRenderer>();
                        if (r == null || !r.enabled || mf.sharedMesh == null) continue;
                        foreach (var p in mf.sharedMesh.vertices) verts.Add(mf.transform.TransformPoint(p));
                    }
                if (verts.Count == 0) Assert.Ignore("片割れの座った体のメッシュが無い（HalfAware/Build the ending の前）");
                Assert.That(d.TwinProbe.Length, Is.GreaterThan(100), "片割れの点が拾えている");
                d.Apply(20f);
                var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                var eye = player.Eye.position;
                var v = d.View;
                var dirs = new[]
                {
                    new Vector2(-1f, 0f), new Vector2(-1f, 0.6f), new Vector2(-1f, -0.6f), new Vector2(-0.4f, 1f),
                    new Vector2(-1f, 1f), new Vector2(-1f, -1f), new Vector2(0f, 1f),
                };
                foreach (var aspect in new[] { 4f / 3f, 16f / 9f, 21f / 9f })
                {
                    var reach = v.front.x;
                    foreach (var dir in dirs)
                    {
                        // 真下へ下ろしてから左へ、の順でも回す
                        foreach (var first in new[] { Vector2.zero, new Vector2(0f, 1.5f) })
                        {
                            var at = v.front;
                            for (var step = 0; step < 80; step++) at = v.Step(at, first, eye, d.TwinProbe, aspect, 70f);
                            for (var step = 0; step < 160; step++)
                            {
                                at = v.Step(at, dir * 1.5f, eye, d.TwinProbe, aspect, 70f);
                                if (step % 2 == 1) continue;
                                if (EndingView.Clearance(at, eye, d.TwinProbe, aspect, 70f) > 12f) continue;
                                var c = EndingView.Clearance(at, eye, verts, aspect, 70f);
                                Assert.That(c, Is.GreaterThan(0f), "縦横比 " + aspect + " で向き " + at + " のとき、片割れの体が画面に入った");
                            }
                            reach = Mathf.Min(reach, at.x);
                        }
                    }
                    // 左へは既定の向きから少なくとも 10 度は回れる（限りが詰まりすぎていない）
                    Assert.That(reach, Is.LessThan(v.front.x - 10f), "縦横比 " + aspect + " で左へほとんど回れない");
                }
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }
}
