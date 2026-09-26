using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfAware.Tests
{
    /// <summary>
    /// 記憶する（手動のセーブ）が場面の中の状態を残し、思い出すとそこから続ける（設計書 5 節）。
    /// 場面ごとに、取り出した状態をセーブの形（JSON）に通してから、組み直した場面へ当て、戻った形を見る。
    ///
    /// シーンは開かない。場面の部品を手元で組み、<see cref="SceneFlow.Use"/> で調べる対象と演出の口を渡す
    /// （開いているシーンを探しに行かない）
    /// </summary>
    public class SceneMemoryTests
    {
        readonly List<Object> made = new List<Object>();
        MemoryBox box;

        [SetUp]
        public void Swap()
        {
            box = new MemoryBox();
            SaveStore.Box = box;
        }

        [TearDown]
        public void Clean()
        {
            SaveStore.Box = null;
            for (var i = made.Count - 1; i >= 0; i--)
                if (made[i] != null) Object.DestroyImmediate(made[i]);
            made.Clear();
        }

        // ---- 組み立ての手伝い -------------------------------------------------

        GameObject Make(string name, Transform parent = null)
        {
            var go = new GameObject("SceneMemoryTests." + name);
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

        static void WireAll(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Text(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        PlayerController Player()
        {
            return Make("Player").AddComponent<PlayerController>();
        }

        SceneFlow Flow(PlayerController player)
        {
            var flow = Make("Flow").AddComponent<SceneFlow>();
            Wire(flow, "player", player);
            return flow;
        }

        static IInteractable[] Items(params string[] ids)
        {
            var items = new IInteractable[ids.Length];
            for (var i = 0; i < ids.Length; i++) items[i] = new FakeItem(ids[i], Vector3.zero) { Required = i == ids.Length - 1 };
            return items;
        }

        /// <summary>セーブの形（JSON）に通して読み戻す</summary>
        static SceneMemo ThroughTheStore(SceneMemo memo, int stage, string scene)
        {
            SaveStore.Write(SaveSlot.First, SaveFlow.Within(new SaveData { stage = stage, scene = scene }, memo), new DateTime(2026, 9, 27, 5, 0, 0, DateTimeKind.Utc));
            var back = SaveStore.Read(SaveSlot.First);
            Assert.IsNotNull(back);
            Assert.IsTrue(back.within, "手動のセーブは場面の中の状態を持つ");
            return back.memo;
        }

        // ---- セーブの形 --------------------------------------------------------

        [Test]
        public void AnOldSaveHasNoStateWithinAndStartsAtTheHead()
        {
            box.Set(SaveStore.KeyOf(SaveSlot.Second), "{\"version\":1,\"stage\":2,\"scene\":\"Alley\",\"hour\":\"\",\"written\":\"2026/09/26 10:00\",\"writtenTicks\":5}");
            var d = SaveStore.Read(SaveSlot.Second);
            Assert.IsNotNull(d, "前の形のセーブも読める");
            Assert.AreEqual(2, d.stage);
            Assert.IsFalse(d.within, "場面の頭から始まる");
        }

        [Test]
        public void WithoutAFreeMomentYetTheManualSaveIsTheHead()
        {
            var d = SaveFlow.Within(new SaveData { stage = 4, scene = "Dive" }, null);
            Assert.IsFalse(d.within);
            SaveStore.Write(SaveSlot.Third, d);
            Assert.IsFalse(SaveStore.Read(SaveSlot.Third).within);
            Assert.AreEqual(2, SaveStore.Read(SaveSlot.Third).version, "形の版を上げた");
        }

        [Test]
        public void ThePoseComesBackSeatedOrStanding()
        {
            var a = Player();
            a.PlaceAt(new Vector3(1f, 0.2f, -3f), 40f, HeadTurn.DefaultLimit, -35f, 12f, 1.1f);
            a.CanMove = false;
            var memo = new SceneMemo();
            SceneMemory.Hold(a, memo);
            memo = ThroughTheStore(memo, 1, "Room");

            var b = Player();
            SceneMemory.Place(b, memo);
            Assert.That(Vector3.Distance(b.transform.position, new Vector3(1f, 0.2f, -3f)), Is.LessThan(1e-4f));
            Assert.AreEqual(40f, b.transform.eulerAngles.y, 1e-3f, "体の向き");
            Assert.AreEqual(HeadTurn.DefaultLimit, b.HeadYawLimit, "座っていれば首だけ振れる");
            Assert.AreEqual(-35f, b.HeadYaw, 1e-3f, "首の向き");
            Assert.AreEqual(12f, b.Pitch, 1e-3f);
            Assert.AreEqual(1.1f, b.EyeHeight, 1e-4f, "座った目の高さ");
            Assert.IsFalse(b.CanMove);
            Assert.IsTrue(b.CanLook);

            // 立った形へ置き直すと、首に溜めた向きは残らない
            SceneMemory.Place(b, new SceneMemo { at = Vector3.zero, turn = 10f, headLimit = 0f, head = 0f, pitch = 0f, eye = 1.6f });
            Assert.AreEqual(0f, b.HeadYaw, 1e-4f);
            Assert.AreEqual(10f, b.Yaw, 1e-3f);
            Assert.IsTrue(b.CanMove);
        }

        // ---- 場面 1（自室） ----------------------------------------------------

        sealed class Room
        {
            public PlayerController player;
            public SceneFlow flow;
            public Transform jack, wrist, parked, chair;
            public GameObject chip, blocker, folded;
            public Garment garment;
        }

        Room MakeRoom()
        {
            var r = new Room();
            r.player = Player();
            r.flow = Flow(r.player);
            Text(r.flow, "standAfter", RoomIds.Jacket);
            r.chair = Make("Chair").transform;
            r.chair.position = new Vector3(0f, 0f, 1f);
            r.blocker = Make("ChairBlocker");
            // 座っている間は椅子のコライダーを切っておく（SceneFlow.Awake と同じ）
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

            r.chip = Make("Chip");
            var taken = Make("Taken").AddComponent<Taken>();
            Wire(taken, "flow", r.flow);
            WireAll(taken, "removed", new Object[] { r.chip });

            r.garment = Make("Garment").AddComponent<Garment>();
            r.garment.Worn = false;
            r.folded = Make("Folded");
            var intro = Make("Intro").AddComponent<RoomIntroDirector>();
            Wire(intro, "flow", r.flow);
            Wire(intro, "garment", r.garment);
            Wire(intro, "folded", r.folded);

            r.flow.Use(Items(RoomIds.Cigarette, RoomIds.Jack, RoomIds.Chips, RoomIds.Jacket, RoomIds.Door),
                new ISceneMemory[] { pull, taken, intro });
            // 場面の頭は座っている
            r.player.PlaceAt(Vector3.zero, 0f, HeadTurn.DefaultLimit, 0f, 0f, 1.26f);
            r.player.CanMove = false;
            return r;
        }

        [Test]
        public void TheRoomComesBackWithTheJackParkedTheChipsTakenAndTheJacketOn()
        {
            var a = MakeRoom();
            foreach (var id in new[] { RoomIds.Jack, RoomIds.Cigarette, RoomIds.Jacket, RoomIds.Chips }) a.flow.Progress.Done.Add(id);
            a.player.PlaceAt(new Vector3(1.5f, 0f, 2f), 200f, 0f, 0f, 15f, PlayerController.StandingEyeHeight);
            a.player.CanMove = true;
            var memo = ThroughTheStore(a.flow.Capture(), 1, "Room");

            var b = MakeRoom();
            b.flow.Restore(memo);
            Assert.AreSame(b.parked, b.jack.parent, "ジャックは抜いて肘掛けの置き場に");
            Assert.IsFalse(b.chip.activeSelf, "抜き差し台のチップは持っていった後");
            Assert.IsTrue(b.garment.Worn, "ジャケットは着た形");
            Assert.IsFalse(b.folded.activeSelf, "卓のジャケットは消える");
            Assert.IsTrue(b.blocker.activeSelf, "立った後は椅子のコライダーが効く");
            Assert.That(b.chair.position.z, Is.LessThan(1f), "椅子は押し下げた形");
            Assert.That(Vector3.Distance(b.player.transform.position, new Vector3(1.5f, 0f, 2f)), Is.LessThan(1e-4f));
            Assert.AreEqual(PlayerController.StandingEyeHeight, b.player.EyeHeight, 1e-4f, "立った目の高さ");
            Assert.AreEqual(0f, b.player.HeadYawLimit, "立てば体ごと回る");
            Assert.AreEqual(15f, b.player.Pitch, 1e-3f);
            Assert.IsTrue(b.player.CanMove);
            CollectionAssert.IsSupersetOf(b.flow.Progress.Done, new[] { RoomIds.Jack, RoomIds.Cigarette, RoomIds.Jacket, RoomIds.Chips });
        }

        [Test]
        public void BeforeTheJacketTheRoomStaysSeatedWithTheJackIn()
        {
            var a = MakeRoom();
            a.flow.Progress.Done.Add(RoomIds.Cigarette);
            a.player.PlaceAt(Vector3.zero, 0f, HeadTurn.DefaultLimit, 50f, 5f, 1.26f);
            var memo = ThroughTheStore(a.flow.Capture(), 1, "Room");

            var b = MakeRoom();
            b.flow.Restore(memo);
            Assert.AreSame(b.wrist, b.jack.parent, "まだ手首に刺さっている");
            Assert.IsTrue(b.chip.activeSelf);
            Assert.IsFalse(b.garment.Worn);
            Assert.IsFalse(b.blocker.activeSelf, "座っている間は椅子のコライダーを切ったまま");
            Assert.AreEqual(1f, b.chair.position.z, 1e-4f);
            Assert.AreEqual(50f, b.player.HeadYaw, 1e-3f, "首の向き");
            Assert.IsFalse(b.player.CanMove, "座ったまま");
        }

        [Test]
        public void MidTalkTheLastFreeMomentIsKept()
        {
            var a = MakeRoom();
            a.flow.Progress.Done.Add(RoomIds.Cigarette);
            a.player.PlaceAt(Vector3.zero, 0f, HeadTurn.DefaultLimit, 20f, 0f, 1.26f);
            a.flow.Checkpoint();
            // 調べて台詞が出ている間に首を振った。書くのは台詞の前の形
            a.flow.Progress.Done.Add(RoomIds.Jack);
            a.flow.Say(new[] { "台詞" });
            a.player.PlaceAt(Vector3.zero, 0f, HeadTurn.DefaultLimit, -60f, 0f, 1.26f);
            Assert.IsFalse(a.flow.Calm);
            var kept = a.flow.Kept();
            Assert.IsNotNull(kept);
            Assert.AreEqual(20f, kept.head, 1e-3f);
            Assert.IsFalse(kept.Did(RoomIds.Jack), "台詞の元の物は、まだ調べる前");
            Assert.IsTrue(kept.Did(RoomIds.Cigarette));
        }

        // ---- 場面 2（路地裏） --------------------------------------------------

        sealed class Alley
        {
            public PlayerController player;
            public SceneFlow flow;
            public AlleyDirector director;
            public GameObject[] chips, smokes, buyers;
            public Transform spot;
        }

        Alley MakeAlley()
        {
            var r = new Alley();
            r.player = Player();
            r.flow = Flow(r.player);
            r.director = Make("AlleyDirector").AddComponent<AlleyDirector>();
            Wire(r.director, "flow", r.flow);
            Wire(r.director, "player", r.player);
            r.spot = Make("SellSpot").transform;
            r.spot.SetPositionAndRotation(new Vector3(4f, 0f, 9f), Quaternion.Euler(0f, 270f, 0f));
            Wire(r.director, "sellSpot", r.spot);
            r.chips = Several("Chip", 6);
            r.smokes = Several("Smoke", 6);
            r.buyers = Several("Buyer", 3);
            WireAll(r.director, "chips", r.chips);
            WireAll(r.director, "smokes", r.smokes);
            WireAll(r.director, "buyers", r.buyers);
            r.flow.Use(Items(AlleyIds.Sign(0), AlleyIds.Table), new ISceneMemory[] { r.director });
            return r;
        }

        /// <summary>卓の上の物と買い手。場面の頭は伏せておく（AlleyDirector.Awake と同じ）</summary>
        GameObject[] Several(string name, int n)
        {
            var list = new GameObject[n];
            for (var i = 0; i < n; i++)
            {
                list[i] = Make(name + i);
                list[i].SetActive(false);
            }
            return list;
        }

        static int Shown(GameObject[] row)
        {
            var n = 0;
            foreach (var g in row) if (g.activeSelf) n++;
            return n;
        }

        [Test]
        public void TheSaleResumesAtTheNextBuyerWithTheTableAsTheyLeftIt()
        {
            var a = MakeAlley();
            a.flow.Progress.Done.Add(AlleyIds.Table);
            a.director.Restore(JsonUtility.ToJson(new AlleyDirector.Memo { buyer = 2 }));
            var memo = ThroughTheStore(a.flow.Capture(), 2, "Alley");
            Assert.IsNotNull(memo.Part("alley.sale"), "売り買いの途中は、次に来る買い手を残す");

            var b = MakeAlley();
            b.flow.Restore(memo);
            Assert.AreEqual(MarketSale.Left(1), Shown(b.chips), "二人目が去った後の枚数");
            Assert.AreEqual(6, Shown(b.smokes), "二人目が置いていった煙草は卓に残る");
            Assert.AreEqual(0, Shown(b.buyers), "買い手は明けてから出す");
            Assert.That(Vector3.Distance(b.player.transform.position, b.spot.position), Is.LessThan(1e-4f), "露店の内側");
            Assert.IsFalse(b.player.CanMove);
            Assert.IsTrue(b.flow.Held, "売り切れるまで場面を閉じない");
            Assert.IsFalse(b.director.Settled);
            Assert.IsTrue(b.flow.Progress.Done.Contains(AlleyIds.Table));
        }

        [Test]
        public void TheTableBeforeEachBuyer()
        {
            Assert.AreEqual(MarketSale.Chips, MarketSale.Left(-1));
            Assert.AreEqual(0, MarketSale.SmokesBefore(0));
            Assert.AreEqual(0, MarketSale.SmokesBefore(1), "煙草を置くのは二人目");
            Assert.AreEqual(6, MarketSale.SmokesBefore(2));
        }

        [Test]
        public void OutsideTheSaleOnlyTheSignsAreKept()
        {
            var a = MakeAlley();
            Assert.IsNull(a.director.Capture());
            Assert.IsTrue(a.director.Settled);
            a.flow.Progress.Done.Add(AlleyIds.Sign(0));
            var memo = ThroughTheStore(a.flow.Capture(), 2, "Alley");
            var b = MakeAlley();
            b.flow.Restore(memo);
            Assert.IsTrue(b.flow.Progress.Done.Contains(AlleyIds.Sign(0)));
            Assert.AreEqual(0, Shown(b.chips), "露店の卓はまだ空");
            Assert.IsFalse(b.flow.Held);
        }

        // ---- 場面 3（自室・接続） ----------------------------------------------

        sealed class Connect
        {
            public PlayerController player;
            public SceneFlow flow;
            public Garment garment;
            public GameObject hung, jackItem, monitorItem, blocker;
            public Transform jack, rest, socket;
        }

        Connect MakeConnect()
        {
            var r = new Connect();
            r.player = Player();
            r.flow = Flow(r.player);
            var d = Make("ConnectDirector").AddComponent<ConnectDirector>();
            Wire(d, "flow", r.flow);
            r.garment = Make("Garment").AddComponent<Garment>();
            r.garment.Worn = true;
            r.hung = Make("Hung");
            r.hung.SetActive(false);
            r.jackItem = Make("JackItem");
            r.jackItem.SetActive(false);
            r.monitorItem = Make("MonitorItem");
            r.monitorItem.SetActive(false);
            r.blocker = Make("ChairBlocker");
            Wire(d, "garment", r.garment);
            Wire(d, "hung", r.hung);
            Wire(d, "jackItem", r.jackItem);
            Wire(d, "monitorItem", r.monitorItem);
            Wire(d, "chairBlocker", r.blocker);
            Wire(d, "screen", Make("Screen").AddComponent<TerminalScreen>());

            r.rest = Make("ArmRest").transform;
            r.jack = Make("Jack", r.rest).transform;
            r.socket = Make("Socket").transform;
            var plug = Make("JackPlug").AddComponent<JackPlug>();
            Wire(plug, "flow", r.flow);
            Wire(plug, "jack", r.jack);
            Wire(plug, "socket", r.socket);

            r.flow.Use(Items(ConnectIds.Order), new ISceneMemory[] { d, plug });
            return r;
        }

        [Test]
        public void ConnectComesBackSeatedWithTheJacketHungAndTheJackIn()
        {
            var a = MakeConnect();
            foreach (var id in new[] { ConnectIds.Note, ConnectIds.Coat, ConnectIds.Chair, ConnectIds.Jack }) a.flow.Progress.Done.Add(id);
            a.player.PlaceAt(new Vector3(1.5f, 0.05f, 1.2f), 0f, HeadTurn.DefaultLimit, 30f, 8f, 1.1f);
            a.player.CanMove = false;
            var memo = ThroughTheStore(a.flow.Capture(), 3, "Connect");

            var b = MakeConnect();
            b.flow.Restore(memo);
            Assert.IsFalse(b.garment.Worn, "ジャケットは脱いだ");
            Assert.IsTrue(b.hung.activeSelf, "コートハンガーに掛かっている");
            Assert.IsFalse(b.blocker.activeSelf, "座っている間は椅子のコライダーを切る");
            Assert.IsTrue(b.jackItem.activeSelf, "座った後はジャックを選べる");
            Assert.IsTrue(b.monitorItem.activeSelf, "挿した後はモニターを選べる");
            Assert.AreSame(b.socket, b.jack.parent, "ジャックは手首に挿さった形");
            Assert.AreEqual(1.1f, b.player.EyeHeight, 1e-4f);
            Assert.AreEqual(30f, b.player.HeadYaw, 1e-3f);
            Assert.IsFalse(b.player.CanMove, "座ったまま");
        }

        [Test]
        public void ConnectBeforeTheChairStillWearsTheJacketUntilTheCoat()
        {
            var a = MakeConnect();
            a.flow.Progress.Done.Add(ConnectIds.Note);
            a.player.PlaceAt(new Vector3(-2f, 0f, 3f), 90f, 0f, 0f, 0f, PlayerController.StandingEyeHeight);
            var memo = ThroughTheStore(a.flow.Capture(), 3, "Connect");
            var b = MakeConnect();
            b.flow.Restore(memo);
            Assert.IsTrue(b.garment.Worn);
            Assert.IsFalse(b.hung.activeSelf);
            Assert.IsFalse(b.jackItem.activeSelf);
            Assert.AreSame(b.rest, b.jack.parent, "ジャックは肘掛けに置いたまま");
            Assert.IsTrue(b.player.CanMove);
        }

        // ---- 場面 4（潜る） ----------------------------------------------------

        [Test]
        public void TheDiveRetracesThePathSoTheCutGrowsTheSame()
        {
            var trail = new List<int>();
            var chain = DiveDirector.Retrace(16, 8, new[] { DiveIds.Listed[0], 4, DiveIds.Listed[0], 7, 4 }, trail);
            Assert.AreEqual(4, chain.Current, "今いる記憶は道筋の最後");
            Assert.AreEqual(2, chain.Hops, "同じ人へ戻っても人数は増えない");
            CollectionAssert.AreEqual(new[] { DiveIds.Listed[0], 4, DiveIds.Listed[0], 7, 4 }, trail);

            var memo = new DiveDirector.Memo { path = trail.ToArray(), clock = 31.5f, spoken = 7, talked = 2, cued = new[] { -1f, 12f }, cued2 = new[] { -1f, -1f } };
            var scene = new SceneMemo { parts = new[] { new MemoPart(DiveDirector.MemoryKey, JsonUtility.ToJson(memo)) } };
            var back = JsonUtility.FromJson<DiveDirector.Memo>(ThroughTheStore(scene, 4, "Dive").Part(DiveDirector.MemoryKey));
            CollectionAssert.AreEqual(memo.path, back.path);
            Assert.AreEqual(31.5f, back.clock, 1e-4f);
            Assert.AreEqual(7, back.spoken);
            Assert.AreEqual(2, back.talked);
            CollectionAssert.AreEqual(memo.cued, back.cued);
            Assert.AreEqual(DiveDirector.Retrace(16, 8, back.path, null).Hops, chain.Hops);
        }

        // ---- 場面 5（小休止） --------------------------------------------------

        [Test]
        public void RestComesBackAfterTheSmokeWithTheMonitorOpen()
        {
            var player = Player();
            var flow = Flow(player);
            var d = Make("RestDirector").AddComponent<RestDirector>();
            Wire(d, "flow", flow);
            var dive = Make("DiveItem");
            dive.SetActive(false);
            Wire(d, "diveItem", dive);
            d.Restore(JsonUtility.ToJson(new RestDirector.Memo { smoked = true }));
            Assert.IsTrue(d.Smoked);
            Assert.IsTrue(dive.activeSelf, "吸い終わった後はモニターを選べる");
            StringAssert.Contains("true", d.Capture());
        }

        // ---- 場面 8（車内） ----------------------------------------------------

        [Test]
        public void TheDriveKeepsTheBandAndTheSeatedLook()
        {
            var m = JsonUtility.FromJson<DriveDirector.Memo>(JsonUtility.ToJson(new DriveDirector.Memo { aboard = true, band = 3 }));
            Assert.IsTrue(m.aboard);
            Assert.AreEqual(3, m.band);
            Assert.IsFalse(JsonUtility.FromJson<DriveDirector.Memo>("{}").aboard, "ガレージの形");

            // 運転席の目。足元そのものが目で（目の高さ 0）、首だけ 90 度まで振れる
            var a = Player();
            a.PlaceAt(new Vector3(4.2f, 1.1f, -5f), 350f, 90f, 72f, -6f, 0f);
            var memo = new SceneMemo();
            SceneMemory.Hold(a, memo);
            memo.parts = new[] { new MemoPart("drive.band", JsonUtility.ToJson(new DriveDirector.Memo { aboard = true, band = 3 })) };
            memo = ThroughTheStore(memo, 8, "Drive");
            var b = Player();
            SceneMemory.Place(b, memo);
            Assert.AreEqual(0f, b.EyeHeight, 1e-5f);
            Assert.AreEqual(90f, b.HeadYawLimit);
            Assert.AreEqual(72f, b.HeadYaw, 1e-3f);
            Assert.AreEqual(350f, b.transform.eulerAngles.y, 1e-3f, "体は運転席の正面");
            Assert.AreEqual(3, JsonUtility.FromJson<DriveDirector.Memo>(memo.Part("drive.band")).band);
        }

        [Test]
        public void TheBandClockRestartsRunning()
        {
            var clock = new BandClock();
            clock.Trigger();
            clock.Spoken();
            clock.Reset();
            Assert.AreEqual(DriveBeat.Running, clock.Beat, "思い出した帯は、きっかけを調べる前から");
        }

        // ---- 場面 9（村） ------------------------------------------------------

        SwingGate MakeGate(out Transform leaf, out BoxCollider shut)
        {
            var gate = Make("Gate").AddComponent<SwingGate>();
            leaf = Make("Leaf", gate.transform).transform;
            shut = gate.gameObject.AddComponent<BoxCollider>();
            Wire(gate, "leaf", leaf);
            Wire(gate, "shut", shut);
            return gate;
        }

        [Test]
        public void TheGateComesBackOpen()
        {
            Transform leafA, leafB;
            BoxCollider shutA, shutB;
            var a = MakeGate(out leafA, out shutA);
            Assert.IsNull(a.Capture(), "閉じていれば残す物は無い");
            a.Set(true);
            var memo = new SceneMemo { parts = SceneMemory.Capture(new List<ISceneMemory> { a }) };
            memo = ThroughTheStore(memo, 9, "Village");

            var b = MakeGate(out leafB, out shutB);
            SceneMemory.Restore(new List<ISceneMemory> { b }, memo);
            Assert.IsTrue(b.IsOpen);
            Assert.IsFalse(shutB.enabled, "開いた戸は当たらない");
            Assert.That(Quaternion.Angle(leafA.localRotation, leafB.localRotation), Is.LessThan(0.01f), "開き切った形");
            Assert.That(Quaternion.Angle(Quaternion.identity, leafB.localRotation), Is.GreaterThan(90f));
        }
    }
}
