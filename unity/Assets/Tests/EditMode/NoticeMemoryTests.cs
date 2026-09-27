using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfAware.Tests
{
    /// <summary>
    /// 場面 7（自室・気づき）の記憶する・思い出す（コンソールと字幕の設計 5 節）。
    /// ジャックを抜いたか・ジャケットを着たかが、セーブの形（JSON）を通って戻る。
    /// シーンは開かず、部品を手元で組んで <see cref="SceneFlow.Use"/> で渡す（SceneMemoryTests と同じ作り）
    /// </summary>
    public class NoticeMemoryTests
    {
        readonly List<Object> made = new List<Object>();

        [SetUp]
        public void Swap()
        {
            SaveStore.Box = new MemoryBox();
        }

        [TearDown]
        public void Clean()
        {
            SaveStore.Box = null;
            for (var i = made.Count - 1; i >= 0; i--)
                if (made[i] != null) Object.DestroyImmediate(made[i]);
            made.Clear();
        }

        GameObject Make(string name, Transform parent = null)
        {
            var go = new GameObject("NoticeMemoryTests." + name);
            if (parent != null) go.transform.SetParent(parent, false);
            else made.Add(go);
            return go;
        }

        static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static SceneMemo ThroughTheStore(SceneMemo memo)
        {
            SaveStore.Write(SaveSlot.First, SaveFlow.Within(new SaveData { stage = 7, scene = "Notice" }, memo),
                new DateTime(2026, 9, 27, 5, 0, 0, DateTimeKind.Utc));
            var back = SaveStore.Read(SaveSlot.First);
            Assert.IsNotNull(back);
            Assert.IsTrue(back.within, "手動のセーブは場面の中の状態を持つ");
            return back.memo;
        }

        sealed class Notice
        {
            public PlayerController player;
            public SceneFlow flow;
            public NoticeDirector director;
            public Garment garment;
            public GameObject hung, log, blocker;
            public Transform jack, wrist, parked, chair;
        }

        /// <summary>場面の頭の形。座って、ジャックは手首に、ジャケットはコートハンガーに、モニターは伏せてある</summary>
        Notice MakeNotice()
        {
            var r = new Notice();
            r.player = Make("Player").AddComponent<PlayerController>();
            r.flow = Make("Flow").AddComponent<SceneFlow>();
            Wire(r.flow, "player", r.player);
            var so = new SerializedObject(r.flow);
            so.FindProperty("standAfter").stringValue = NoticeIds.StandAfter;
            so.ApplyModifiedPropertiesWithoutUndo();
            r.chair = Make("Chair").transform;
            r.chair.position = new Vector3(0f, 0f, 1f);
            r.blocker = Make("ChairBlocker");
            r.blocker.SetActive(false);
            Wire(r.flow, "chair", r.chair);
            Wire(r.flow, "chairBlocker", r.blocker);

            r.wrist = Make("Wrist").transform;
            r.jack = Make("Jack", r.wrist).transform;
            r.parked = Make("Parked").transform;
            r.parked.position = new Vector3(0.3f, 0.6f, 0.2f);
            var pull = Make("JackPull").AddComponent<JackPull>();
            Wire(pull, "flow", r.flow);
            Wire(pull, "jack", r.jack);
            Wire(pull, "parked", r.parked);

            r.garment = Make("Garment").AddComponent<Garment>();
            r.garment.Worn = false;
            r.hung = Make("Hung");
            r.log = Make("LogItem");
            r.log.SetActive(false);
            r.director = Make("NoticeDirector").AddComponent<NoticeDirector>();
            Wire(r.director, "flow", r.flow);
            Wire(r.director, "garment", r.garment);
            Wire(r.director, "hung", r.hung);
            Wire(r.director, "logItem", r.log);

            var items = new IInteractable[NoticeIds.Order.Length];
            for (var i = 0; i < items.Length; i++)
                items[i] = new FakeItem(NoticeIds.Order[i], Vector3.zero) { Required = true, After = NoticeIds.After(NoticeIds.Order[i]) };
            r.flow.Use(items, new ISceneMemory[] { pull, r.director });
            r.player.PlaceAt(Vector3.zero, 0f, HeadTurn.DefaultLimit, 0f, 0f, 1.26f);
            r.player.CanMove = false;
            return r;
        }

        [Test]
        public void AfterTheLogSheIsStillSeatedWithTheJackIn()
        {
            var a = MakeNotice();
            a.flow.Progress.Done.Add(NoticeIds.Log);
            a.player.PlaceAt(Vector3.zero, 0f, HeadTurn.DefaultLimit, 25f, 6f, 1.26f);
            var memo = ThroughTheStore(a.flow.Capture());

            var b = MakeNotice();
            b.flow.Restore(memo);
            Assert.IsTrue(b.director.Noticed, "気づいた後から始まる");
            Assert.IsTrue(b.log.activeSelf, "モニターは開いている");
            Assert.AreSame(b.wrist, b.jack.parent, "ジャックはまだ手首に挿さっている");
            Assert.IsFalse(b.garment.Worn);
            Assert.IsTrue(b.hung.activeSelf, "ジャケットはコートハンガーに掛かったまま");
            Assert.IsFalse(b.blocker.activeSelf, "座っている間は椅子のコライダーを切ったまま");
            Assert.AreEqual(25f, b.player.HeadYaw, 1e-3f, "首の向き");
            Assert.IsFalse(b.player.CanMove, "座ったまま");
        }

        [Test]
        public void AfterTheJackSheStandsWithTheJackParked()
        {
            var a = MakeNotice();
            foreach (var id in new[] { NoticeIds.Log, NoticeIds.Jack }) a.flow.Progress.Done.Add(id);
            a.player.PlaceAt(new Vector3(1f, 0f, -1f), 180f, 0f, 0f, 10f, PlayerController.StandingEyeHeight);
            a.player.CanMove = true;
            var memo = ThroughTheStore(a.flow.Capture());

            var b = MakeNotice();
            b.flow.Restore(memo);
            Assert.AreSame(b.parked, b.jack.parent, "ジャックは抜いて肘掛けの置き場に");
            Assert.IsFalse(b.garment.Worn, "ジャケットはまだ着ていない");
            Assert.IsTrue(b.hung.activeSelf);
            Assert.IsTrue(b.blocker.activeSelf, "立った後は椅子のコライダーが効く");
            Assert.That(b.chair.position.z, Is.LessThan(1f), "椅子は押し下げた形");
            Assert.AreEqual(PlayerController.StandingEyeHeight, b.player.EyeHeight, 1e-4f, "立った目の高さ");
            Assert.IsTrue(b.player.CanMove);
        }

        [Test]
        public void AfterTheCoatSheWearsTheJacketAndTheHookIsEmpty()
        {
            var a = MakeNotice();
            foreach (var id in new[] { NoticeIds.Log, NoticeIds.Jack, NoticeIds.Coat }) a.flow.Progress.Done.Add(id);
            a.player.PlaceAt(new Vector3(1.2f, 0f, -2f), 180f, 0f, 0f, 0f, PlayerController.StandingEyeHeight);
            a.player.CanMove = true;
            var memo = ThroughTheStore(a.flow.Capture());

            var b = MakeNotice();
            b.flow.Restore(memo);
            Assert.IsTrue(b.garment.Worn, "ジャケットは着た形");
            Assert.IsFalse(b.hung.activeSelf, "コートハンガーは空");
            Assert.AreSame(b.parked, b.jack.parent);
            Assert.That(Vector3.Distance(b.player.transform.position, new Vector3(1.2f, 0f, -2f)), Is.LessThan(1e-4f));
            CollectionAssert.IsSupersetOf(b.flow.Progress.Done, new[] { NoticeIds.Log, NoticeIds.Jack, NoticeIds.Coat });
        }

        // 頭の間と独白の間は残さない。独白を読み終えるまで、手動のセーブは場面の頭を書く
        [Test]
        public void TheDirectorHasNothingOfItsOwnToKeep()
        {
            var r = MakeNotice();
            Assert.IsNull(r.director.Capture(), "どこまで来たかは調べ済みの印から決まる");
            Assert.IsTrue(r.director.Settled, "再生していなければ、間の途中でも着ている途中でもない");
        }
    }
}
