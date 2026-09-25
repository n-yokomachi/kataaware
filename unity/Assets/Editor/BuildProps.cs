using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 部屋の小物のうち、既製の素材に無いものをここで組む。
    /// 手で置くと再生を抜けるたびに消えるので、手順として残しておく
    /// </summary>
    public static class BuildProps
    {
        public const string Generated = "Assets/Models/generated/";
        public const string Materials = "Assets/Materials/Room/";

        /// <summary>吸い殻の紙。灰をかぶって白は残っていない</summary>
        static readonly Color Paper = new Color(0.72f, 0.70f, 0.66f);
        /// <summary>吸い口。使い古して色が落ちている</summary>
        static readonly Color Filter = new Color(0.52f, 0.42f, 0.28f);
        /// <summary>焦げた先</summary>
        static readonly Color Burnt = new Color(0.10f, 0.09f, 0.08f);

        [MenuItem("HalfAware/Fill the ashtray")]
        public static void FillAshtrayMenu()
        {
            var tray = GameObject.Find("Room/Ashtray");
            if (tray == null) { Debug.LogError("灰皿が場面に無い"); return; }
            FillAshtray(tray.transform);
            Mark(tray);
        }

        [MenuItem("HalfAware/Build the implant jack")]
        public static void BuildJackMenu()
        {
            var arm = FindBone(HumanBodyBones.RightLowerArm);
            if (arm == null) { Debug.LogError("右の前腕の骨が見つからない"); return; }
            var jack = BuildJack(arm);
            Selection.activeGameObject = jack;
            Mark(jack);
        }

        [MenuItem("HalfAware/Build the binder")]
        public static void BuildBinderMenu()
        {
            var room = GameObject.Find("Room");
            if (room == null) { Debug.LogError("部屋が場面に無い"); return; }
            var made = BuildBinder(room.transform);
            Selection.activeGameObject = made;
            Mark(made);
        }

        // ---- 灰皿の中身 -------------------------------------------------

        /// <summary>
        /// 吸い殻を敷き詰める。吸い口・紙・焦げた先の 3 つに分けて、
        /// それぞれ 1 つの mesh へ畳む。物の数を増やさずに本数だけ増やせる
        /// </summary>
        public static void FillAshtray(Transform tray)
        {
            // 前に置いたものは片付ける
            for (var i = tray.childCount - 1; i >= 0; i--)
            {
                var c = tray.GetChild(i);
                if (c.name == "Dish") continue;
                Object.DestroyImmediate(c.gameObject);
            }

            var filters = new List<Mesh>();
            var papers = new List<Mesh>();
            var burnt = new List<Mesh>();
            var ash = new List<Mesh>();

            // 皿の凹みの底は y=0.015、そこでの内側の半径は 0.046
            const float floor = 0.015f;
            const float reach = 0.046f;
            var state = Random.state;
            Random.InitState(20661);
            for (var i = 0; i < 16; i++)
            {
                var radius = Random.Range(0.0030f, 0.0036f);
                var edge = reach - radius;
                var offAngle = Random.Range(0f, Mathf.PI * 2f);
                var offReach = Mathf.Sqrt(Random.value) * edge * 0.72f;
                var layer = i < 9 ? 0f : Random.Range(0.004f, 0.010f);   // 後の方は上に積む
                var mid = new Vector3(Mathf.Sin(offAngle) * offReach, floor + radius + 0.0008f + layer,
                    Mathf.Cos(offAngle) * offReach);
                var axis = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), 0f) * Vector3.forward;
                // 縁からはみ出さない長さに詰める。真ん中へ寄せると固まるので、長さの方を譲る
                var half = Mathf.Min(Random.Range(0.011f, 0.020f), Fits(mid, axis, edge), Fits(mid, -axis, edge));
                var a = mid - axis * half;
                var b = mid + axis * half;
                var squash = Random.value < 0.45f ? 0.55f : 1f;   // 揉み消したもの
                Butt(filters, papers, burnt, a, b, radius, squash);
            }
            // 灰の層。凹みの底を薄く覆う
            ash.Add(Disc(new Vector3(0f, floor + 0.0012f, 0f), reach * 0.95f, 0.0024f, 14));
            for (var i = 0; i < 5; i++)
            {
                var yaw = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                var d = Random.Range(0.004f, reach * 0.7f);
                ash.Add(Disc(new Vector3(Mathf.Sin(yaw) * d, floor + 0.0022f, Mathf.Cos(yaw) * d),
                    Random.Range(0.004f, 0.010f), 0.0026f, 8));
            }
            Random.state = state;

            Place(tray, "Ash", ProcMesh.Save(ProcMesh.Combine(ash, null), Generated + "AshBed.asset"), Mat("Ink"));
            Place(tray, "Filters", ProcMesh.Save(ProcMesh.Combine(filters, null), Generated + "ButtFilters.asset"),
                Tinted("ButtFilter", Filter, 0f, 0.10f));
            Place(tray, "Papers", ProcMesh.Save(ProcMesh.Combine(papers, null), Generated + "ButtPapers.asset"),
                Tinted("ButtPaper", Paper, 0f, 0.06f));
            Place(tray, "Burnt", ProcMesh.Save(ProcMesh.Combine(burnt, null), Generated + "ButtBurnt.asset"),
                Tinted("ButtBurnt", Burnt, 0f, 0.04f));
        }

        /// <summary>centre から dir へ、半径 edge の円の内側に収まる長さ</summary>
        static float Fits(Vector3 centre, Vector3 dir, float edge)
        {
            var c = new Vector2(centre.x, centre.z);
            var d = new Vector2(dir.x, dir.z);
            if (d.sqrMagnitude < 1e-8f) return edge;
            d.Normalize();
            var along = Vector2.Dot(c, d);
            var root = along * along + edge * edge - c.sqrMagnitude;
            if (root <= 0f) return 0f;
            return Mathf.Max(0f, -along + Mathf.Sqrt(root));
        }

        /// <summary>吸い殻 1 本。a が吸い口、b が焦げた先。squash は揉み消した潰れ具合</summary>
        static void Butt(List<Mesh> filters, List<Mesh> papers, List<Mesh> burnt, Vector3 a, Vector3 b, float r, float squash)
        {
            var dir = (b - a).normalized;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var len = Vector3.Distance(a, b);
            var tip = a + dir * len * 0.34f;        // 吸い口の境
            var ember = a + dir * len * 0.88f;      // 焦げの境

            filters.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(a, r * 0.94f, r * 0.94f * squash, rot),
                new ProcMesh.Ring(tip, r, r * squash, rot),
            }, 7));
            papers.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(tip, r, r * squash, rot),
                new ProcMesh.Ring(ember, r * 0.97f, r * 0.97f * squash, rot),
            }, 7));
            burnt.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(ember, r * 0.97f, r * 0.97f * squash, rot),
                new ProcMesh.Ring(b, r * 0.70f, r * 0.70f * squash, rot),
            }, 7));
        }

        /// <summary>灰の層。平たい円盤</summary>
        static Mesh Disc(Vector3 centre, float radius, float height, int segments)
        {
            return ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(centre + Vector3.down * height * 0.5f, radius * 0.9f, radius * 0.9f, Quaternion.Euler(90f, 0f, 0f)),
                new ProcMesh.Ring(centre + Vector3.up * height * 0.5f, radius, radius, Quaternion.Euler(90f, 0f, 0f)),
            }, segments);
        }

        // ---- 紙ばさみ -----------------------------------------------------

        /// <summary>
        /// 売り上げを書きつけた紙ばさみ。厚紙の台に紙束を挟んで、頭に金具の口金。
        /// ソファの毛布の上に投げてある
        /// </summary>
        public static GameObject BuildBinder(Transform room)
        {
            const float w = 0.108f;    // 幅の半分。A4 より少し小さい
            const float h = 0.150f;    // 丈の半分

            var old = GameObject.Find("Room/Binder");
            if (old != null) Object.DestroyImmediate(old);
            var oldClip = GameObject.Find("Room/HairClip");
            if (oldClip != null) Object.DestroyImmediate(oldClip);

            var go = new GameObject("Binder");
            go.transform.SetParent(room, false);
            // 毛布の襞の上。座面そのものは毛布に覆われている
            go.transform.position = new Vector3(-2.40f, 0.636f, 0.19f);
            go.transform.rotation = Quaternion.Euler(0f, 62f, 0f);

            Slab(go.transform, "Board", new Vector3(0f, 0.0025f, 0f), Vector3.zero,
                new Vector3(w * 2f, 0.005f, h * 2f), Mat("PanelDark"));
            // 紙束。台より一回り小さく、少しずれて重なっている
            Slab(go.transform, "Paper", new Vector3(0.002f, 0.0075f, -0.004f), new Vector3(0f, 1.2f, 0f),
                new Vector3(w * 1.90f, 0.005f, h * 1.90f), Mat("Steel"));
            Slab(go.transform, "Paper2", new Vector3(-0.003f, 0.0106f, 0.002f), new Vector3(0f, -0.8f, 0f),
                new Vector3(w * 1.86f, 0.002f, h * 1.86f), Mat("Steel"));
            // 口金。頭に渡した金具と、押さえの爪
            Slab(go.transform, "Clip", new Vector3(0f, 0.0128f, h * 0.80f), Vector3.zero,
                new Vector3(w * 1.05f, 0.005f, 0.030f), Mat("SteelDark"));
            Slab(go.transform, "ClipLip", new Vector3(0f, 0.0150f, h * 0.80f - 0.017f), new Vector3(-16f, 0f, 0f),
                new Vector3(w * 0.98f, 0.003f, 0.012f), Mat("SteelDark"));
            Slab(go.transform, "HingeL", new Vector3(-w * 0.52f, 0.0128f, h * 0.92f), Vector3.zero,
                new Vector3(0.012f, 0.009f, 0.012f), Mat("Steel"));
            Slab(go.transform, "HingeR", new Vector3(w * 0.52f, 0.0128f, h * 0.92f), Vector3.zero,
                new Vector3(0.012f, 0.009f, 0.012f), Mat("Steel"));
            // 走り書き。紙の上に薄い帯を並べて、字が書いてあるように見せる
            for (var i = 0; i < 5; i++)
            {
                var y = Mathf.Lerp(h * 0.42f, -h * 0.55f, i / 4f);
                var len = w * (i == 0 ? 1.30f : Random.Range(0.85f, 1.55f));
                Slab(go.transform, "Line" + i, new Vector3(-w * 0.10f + len * 0.10f, 0.0119f, y), Vector3.zero,
                    new Vector3(len, 0.001f, 0.0035f), Mat("Ink"));
            }
            return go;
        }

        // ---- 手首のジャックとケーブル -------------------------------------

        /// <summary>
        /// 手首に刺さっているジャック。右の前腕（arm）の手首の差込口（<see cref="WristPort"/>）の子にして、差込口の軸に揃えて挿す。
        /// 骨の大きさは入れ物で打ち消す。手のひらの側の、肘寄りに刺す。座った形では右の手のひらが上を向くので、下を見ると目に入る。
        ///
        /// 前は手の骨の子にしていた。刺す所は手首より 3 cm 肘寄りで、肌は前腕の骨に 8 割で付いているので、
        /// 手首をひねるとジャックだけが手と一緒に回り、肌の上を滑った
        /// </summary>
        public static GameObject BuildJack(Transform arm)
        {
            var parts = new List<Mesh>();
            // 先のピン → 差し込み口の座金 → 胴 → ケーブルの根元、と +Z へ伸ばす。
            // ピンは座金の下から手首の差込口の穴へ入る。抜くとき、穴から抜けてくるのが見える
            parts.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(new Vector3(0f, 0f, JackPinTip), JackPinRadius * 0.6f, JackPinRadius * 0.6f),
                new ProcMesh.Ring(new Vector3(0f, 0f, JackPinTip + 0.001f), JackPinRadius, JackPinRadius),
                new ProcMesh.Ring(new Vector3(0f, 0f, -0.002f), JackPinRadius, JackPinRadius),
            }, 10));
            parts.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(new Vector3(0f, 0f, -0.002f), 0.0115f, 0.0115f),
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0035f), JackWasherRadius, JackWasherRadius),
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0050f), 0.0092f, 0.0092f),
            }, 10));
            parts.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0050f), 0.0088f, 0.0088f),
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0165f), 0.0086f, 0.0086f),
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0185f), 0.0062f, 0.0062f),
            }, 10));
            parts.Add(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0185f), 0.0058f, 0.0058f),
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0300f), 0.0040f, 0.0040f),
            }, 8));
            for (var i = 0; i < parts.Count; i++) parts[i] = Outward(parts[i]);
            var mesh = ProcMesh.Save(ProcMesh.Combine(parts, null), Generated + "Jack.asset");

            // 差込口は体を組み立てたときに付けてある（BuildRocketboxProtagonist）。無い体（前の組み立て）にだけ、今の姿勢で付ける。
            // ジャックは差込口の子にする。差込口は肌に貼り付いて動く（SkinPoint）ので、挿さったジャックも肌から離れない
            var port = arm.Find(PortName);
            var an = arm.GetComponentInParent<Animator>();
            if (port == null && an != null) port = WristPort(an);
            var parent = port != null ? port : arm;
            // 作り直しても子は残す。調べる対象もケーブルの端もここにぶら下がっているので、
            // 消して作り直すと参照が切れる
            var found = parent.Find("Jack");
            if (found == null) found = arm.Find("Jack");
            var go = found != null ? found.gameObject : new GameObject("Jack");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one / parent.lossyScale.x;
            // 差込口の軸に揃え、座金の下の面を差込口の輪の上に乗せる（ピンは穴の奥へ入る）
            go.transform.localRotation = Quaternion.identity;
            go.transform.position = parent.position + parent.forward * JackSeat;
            Need<MeshFilter>(go).sharedMesh = mesh;
            Need<MeshRenderer>(go).sharedMaterial = Mat("SteelDark");

            // 光る帯。部屋が暗いので、鋼のままだと手首の影に沈んで刺さっているのが分からない。
            // 抜くところを見せるあいだ、目はこの帯を追う
            var band = ProcMesh.Save(Outward(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0072f), 0.0094f, 0.0094f),
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0090f), 0.0098f, 0.0098f),
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0122f), 0.0098f, 0.0098f),
                new ProcMesh.Ring(new Vector3(0f, 0f, 0.0140f), 0.0094f, 0.0094f),
            }, 10)), Generated + "JackBand.asset");
            var litT = go.transform.Find("Band");
            var lit = litT != null ? litT.gameObject : new GameObject("Band");
            lit.transform.SetParent(go.transform, false);
            lit.transform.localPosition = Vector3.zero;
            lit.transform.localRotation = Quaternion.identity;
            lit.transform.localScale = Vector3.one;
            Need<MeshFilter>(lit).sharedMesh = band;
            var litR = Need<MeshRenderer>(lit);
            litR.sharedMaterial = Glow("JackLight", new Color(0.36f, 0.82f, 0.86f), 2.4f);
            litR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var endT = go.transform.Find("CableEnd");
            var end = endT != null ? endT.gameObject : new GameObject("CableEnd");
            end.transform.SetParent(go.transform, false);
            // ケーブルの端は、細る尻（根元から 30 mm）の少し内側から出す。ケーブルの切り口が尻の外へはみ出さない
            end.transform.localPosition = new Vector3(0f, 0f, 0.026f);
            return go;
        }

        /// <summary>
        /// ProcMesh.Loft の筒の側の面は内へ向いている（両端の蓋だけが外を向く）。ジャックは抜くときに座金の下まで見え、
        /// 手前の側の面が消えて奥の内の面が見えた。z の軸から外へ向いていない側の三角の巡りを返して、外へ向け直す
        /// </summary>
        static Mesh Outward(Mesh loft)
        {
            var v = loft.vertices;
            var t = loft.triangles;
            for (var i = 0; i < t.Length; i += 3)
            {
                var a = v[t[i]];
                var n = Vector3.Cross(v[t[i + 1]] - a, v[t[i + 2]] - a);
                var mid = (a + v[t[i + 1]] + v[t[i + 2]]) / 3f;
                if (n.sqrMagnitude < 1e-16f || Mathf.Abs(n.normalized.z) > 0.9f || Vector3.Dot(n, new Vector3(mid.x, mid.y, 0f)) >= 0f) continue;
                var s = t[i + 1];
                t[i + 1] = t[i + 2];
                t[i + 2] = s;
            }
            loft.triangles = t;
            loft.RecalculateNormals();
            return loft;
        }

        /// <summary>
        /// 手首の肘寄りから手のひらの側へ、手首の差込口とジャックの座金が乗るところ（手の骨の付け根から肘の側へ m）。
        /// ここの肌は手の骨にも 2 割ほど付いていて、前腕の骨にも手の骨にも固く付けられない（手首をひねると肌から 6 mm ずれた）。
        /// 差込口は肌に貼り付かせる（<see cref="SkinPoint"/>）。前腕の骨だけに付いた肌（手首から 6.5 cm より肘寄り）まで離すと、
        /// 左手の掴み方もケーブルの通り道も合わなくなった
        /// </summary>
        public const float JackFromWrist = 0.030f;

        /// <summary>
        /// 差込口を置く向き。腕の芯から、手のひらの向きを親指の側へこの角（度）だけ倒した向きの肌に置く。
        /// ジャックの軸と同じ 40 度では、手のひらの側の平らな面の縁（前腕の横へ落ちる所）に掛かり、輪の外の縁の下の肌が 3 mm 落ちて輪が折れた。
        /// 20 度でも、輪の足もとの肌が平らな面から最大 0.49 mm ずれ（腕の丸みと、肌の網の折れ目）、肌に沿って曲げた輪の手前の縁が、
        /// 斜めの寄りで輪の下に垂れた三日月に見えた。0〜26 度を測ると 14 度がいちばん平ら（最大 0.17 mm）
        /// </summary>
        public const float PortAround = 14f;

        /// <summary>
        /// 挿さったジャックの根元（座金の下の面から 2 mm 上）の、差込口の置き所（肌の面）からの高さ（m）。
        /// 座金の下の面を差込口の輪の上（<see cref="PortRise"/>）から 0.3 mm 上に乗せる。ピンは輪の穴から肌の下へ入る
        /// </summary>
        public const float JackSeat = PortRise + 0.0003f + 0.002f;

        /// <summary>ジャックの先のピンの半径（m）。手首の差込口の穴（<see cref="PortHole"/>）へ入る</summary>
        public const float JackPinRadius = 0.0028f;

        /// <summary>ジャックの先のピンの先端の高さ（ジャックの根元から m、負は根元の下）</summary>
        public const float JackPinTip = -0.009f;

        // ---- 手首の差込口（インプラント） ---------------------------------

        /// <summary>手首の差込口の名前。右の前腕の骨の子で、子に穴（<see cref="PortHoleName"/>）を持つ</summary>
        public const string PortName = "WristPort";
        public const string PortHoleName = "Hole";

        /// <summary>
        /// 差込口の金属の輪の外の半径（m）。径 13 mm。ジャックの座金（径 23.6 mm）より小さく、挿さっている間は座金の下に隠れる
        /// </summary>
        public const float PortRadius = 0.0065f;
        /// <summary>差込口の穴の半径（m）。ジャックのピン（<see cref="JackPinRadius"/>）が入る</summary>
        public const float PortHole = 0.0034f;
        /// <summary>輪の内の縁（穴の口）の半径</summary>
        const float PortLip = 0.0038f;
        /// <summary>輪の平らな上の面の外の半径。ここから外の縁へ、肌へ向けて面取りする</summary>
        const float PortTop = 0.0055f;
        /// <summary>
        /// 輪が肌から立つ高さ（m）。輪は肌の丸みに沿って曲げてあるので、腕の輪郭に来る向きから見ると、この高さだけ輪郭の外へ出る。
        /// 0.4 mm では寄りで輪郭の外へ 0.4〜0.5 mm 浮き出して見えた
        /// </summary>
        const float PortRise = 0.00015f;
        /// <summary>輪の外の縁を、肌の下へ沈める深さ（m）。姿勢で肌が少し動いても、縁の下に隙間を見せない</summary>
        const float PortSkirt = 0.0015f;
        /// <summary>
        /// 穴の暗い面を肌から浮かせる高さ（m）。体の肌は穴の所で切れていないので、穴は肌の上に置いた暗い面で見せ、
        /// 輪の内の壁が口の縁からそこへ下る
        /// </summary>
        const float PortFloor = 0.00008f;
        /// <summary>輪と穴の周りの分け方。外の縁の辺の長さは 0.85 mm で、辺の間の肌の丸みから離れない</summary>
        const int PortSegments = 48;
        /// <summary>
        /// 輪と穴の断面を、半径の向きにこの幅（m）ごとに刻む。面は刻んだ頂点の間で平らなので、刻みが粗いと、面の下の肌の折れ目が
        /// 面より上に出る（輪の上の面を 1.7 mm の一枚にしたとき、斜めの寄りで輪の中に肌の筋が見えた）
        /// </summary>
        const float PortStep = 0.0006f;
        /// <summary>骨ごとの枠のずらし（<see cref="PortMesh"/>）を揃える刻み（m）。近い枠を一つにまとめ、枠の数を減らす</summary>
        const float PortShiftGrid = 0.00005f;

        /// <summary>断面の点の間を、半径の向きに <see cref="PortStep"/> ごとに刻み直す（同じ半径の上下の辺はそのまま）</summary>
        static Vector2[] Dense(Vector2[] profile)
        {
            var list = new List<Vector2> { profile[0] };
            for (var i = 1; i < profile.Length; i++)
            {
                var a = profile[i - 1];
                var b = profile[i];
                var n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(b.x - a.x) / PortStep - 1e-4f));
                for (var k = 1; k <= n; k++) list.Add(Vector2.Lerp(a, b, (float)k / n));
            }
            return list.ToArray();
        }

        /// <summary>
        /// 右の手首の差込口（インプラント）。ジャックが刺さる所（<see cref="PortPose"/>）の皮膚に埋まった小さな金属の輪と、真ん中の暗い穴。
        /// 輪は肌の面に沿った円。この物の向きはジャックの軸（差込口の穴の向き）で、ジャックはこの向きに揃えて挿す（<see cref="BuildJack"/>）。
        /// 右の前腕の骨の子に置き、その所の肌と同じ重みで前腕と手の骨の動きを混ぜて貼り付かせる（<see cref="SkinPoint"/>）。
        /// 手首を曲げてもひねっても、肌から沈まず浮かない。
        /// 輪の頂点は、今の姿勢の肌の面へ一つずつ合わせて置く（前腕の丸みに沿わせ、面一に近く、縁だけ <see cref="PortRise"/> 立つ）。
        /// 主人公にも片割れにも付ける。片割れは模型ごと裏返した鏡像なので、片割れ本人の左の手首に来る
        /// </summary>
        public static Transform WristPort(Animator an, float fromWrist = JackFromWrist)
        {
            var arm = an.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (arm == null) return null;
            Vector3 position;
            Quaternion rotation;
            var surface = BodySurface(an);
            Quaternion flat;
            PortPose(an, fromWrist, surface, out position, out rotation, out flat);
            var t = arm.Find(PortName);
            var go = t != null ? t.gameObject : new GameObject(PortName);
            go.transform.SetParent(arm, false);
            go.transform.localScale = Vector3.one / Mathf.Abs(arm.lossyScale.x);
            go.transform.SetPositionAndRotation(position, rotation);
            // 輪と穴は体の骨で肌と一緒に曲がる（SkinnedMeshRenderer）。頂点ごとに、その下の肌の三角の三つの頂点の動きをそのまま混ぜる（PortMesh）。
            // 輪を差込口の真ん中の一つの置き方で動かすと、13 mm の幅の中で肌の動きが揃わず、縁が肌から 0.5 mm 沈んだり 1 mm 立ったりした。
            // 三角の中の一点の重みだけを使っても、手首を曲げて肘掛けに置いた座り姿で、穴の上へ肌が 0.4 mm 出た
            var frame = Matrix4x4.TRS(position, flat, Vector3.one);
            var patch = Patch(an, position, 0.05f);

            // 輪と穴の頂点の、差込口の面からの肌の高さ（m）。今の姿勢の体の面へ、差込口の軸に沿って線を投げて測る
            System.Func<float, float, float> skin = (x, y) =>
            {
                var from = position + flat * new Vector3(x, y, -0.02f);
                Vector3 normal;
                var hit = Hit(surface, from, flat * Vector3.forward, 0.04f, out normal);
                return hit > 0f ? hit - 0.02f : 0f;
            };
            // 輪の断面（中心からの半径、肌からの高さ）。穴の口の内の壁 → 口の縁 → 平らな上の面 → 外の縁の面取り → 肌の下の裾
            var ring = Dense(new[]
            {
                new Vector2(PortHole, PortFloor),
                new Vector2(PortLip, PortRise * 0.8f),
                new Vector2(PortTop, PortRise),
                new Vector2(PortRadius, 0f),
                new Vector2(PortRadius, -PortSkirt),
            });
            Transform[] ringBones;
            var ringMesh = PortMesh(ring, skin, float.NaN, frame, flat * Vector3.forward, patch, out ringBones);
            // 穴の暗い面。肌に沿わせ、真ん中は平らに塞ぐ
            var hole = Dense(new[]
            {
                new Vector2(PortHole * 0.25f, PortFloor),
                new Vector2(PortHole, PortFloor),
            });
            Transform[] holeBones;
            var holeMesh = PortMesh(hole, skin, skin(0f, 0f) + PortFloor, frame, flat * Vector3.forward, patch, out holeBones);
            // 肌に貼り付かせる。差込口の真ん中の肌と同じ重みで骨を混ぜて置く
            Transform[] bones;
            float[] weights;
            Matrix4x4[] offsets;
            if (SkinBinding(an, position - flat * Vector3.forward * 0.02f, flat * Vector3.forward, go.transform.localToWorldMatrix, out bones, out weights, out offsets))
                Need<SkinPoint>(go).Set(bones, weights, offsets);
            // mesh は人ごとに残す（主人公と片割れは前腕の形が違う）
            PortPart(go.transform, PortRingName, SaveSkinned(ringMesh, Generated + "WristPort_" + an.gameObject.name + ".asset"),
                Tinted("PortMetal", PortColour, 0.85f, 0.55f), patch, ringBones);
            PortPart(go.transform, PortHoleName, SaveSkinned(holeMesh, Generated + "WristPortHole_" + an.gameObject.name + ".asset"),
                Mat("Ink"), patch, holeBones);
            // 差込口から肘の方へ伸びる銀の回路の意匠。差込口と一つの部品で、同じ肌に沿わせ方で付ける（4.6 cm 先まで届くので、肌は広めに集める）
            var reach = Patch(an, position, CircuitReach);
            Transform[] circuitBones;
            var circuitMesh = CircuitMesh(skin, frame, flat * Vector3.forward, reach, out circuitBones);
            PortPart(go.transform, CircuitName, SaveSkinned(circuitMesh, Generated + "WristCircuit_" + an.gameObject.name + ".asset"),
                Tinted("PortMetal", PortColour, 0.85f, 0.55f), reach, circuitBones);
            return go.transform;
        }

        public const string PortRingName = "Ring";
        /// <summary>回路の意匠の名前。差込口（<see cref="PortName"/>）の子</summary>
        public const string CircuitName = "Circuit";

        // ---- 差込口から肘へ伸びる銀の回路の意匠（「幹線と分岐」） --------------------------
        //
        // 差込口の枠（<see cref="PortPose"/> の flat。x は腕のまわり、y は手の先の側が正で、肘の側は負。mm で書く）の上に置く。
        // - 幹線は三本並んで差込口の輪の肘の側から伸びる。真ん中はいちばん長く伸びて四角い端子で終わる。
        //   外の二本は途中で 45 度の折れで外へずれ、一本はビア（小さな輪）、もう一本は小さな四角い端子で終わる
        // - 枝は三本。幹線の途中から 45 度で外へ分かれ、先はビアで終わる
        // - 線は肌から少しだけ立つ銀の帯（肌の曲がりに沿わせる）。縁は肌の下へ沈めて、姿勢で肌が動いても隙間を見せない。
        //   端子とビアは線より少し高くし、線の端をその下へ潜らせる

        /// <summary>幹線の道筋（mm）。始まりは差込口の輪の外の縁の下</summary>
        static readonly Vector2[][] CircuitTrunks =
        {
            new[] { new Vector2(0f, -5.8f), new Vector2(0f, -44.0f) },
            new[] { new Vector2(2.3f, -5.9f), new Vector2(2.3f, -15.0f), new Vector2(5.3f, -18.0f), new Vector2(5.3f, -35.6f) },
            new[] { new Vector2(-2.3f, -5.9f), new Vector2(-2.3f, -21.0f), new Vector2(-5.3f, -24.0f), new Vector2(-5.3f, -39.3f) },
        };
        /// <summary>枝の道筋（mm）。始まりは幹線の上</summary>
        static readonly Vector2[][] CircuitBranches =
        {
            new[] { new Vector2(5.3f, -24.0f), new Vector2(8.1f, -26.8f), new Vector2(8.1f, -29.1f) },
            new[] { new Vector2(0f, -27.0f), new Vector2(2.6f, -29.6f), new Vector2(2.6f, -31.6f) },
            new[] { new Vector2(-5.3f, -30.0f), new Vector2(-8.1f, -32.8f), new Vector2(-8.1f, -35.1f) },
        };
        /// <summary>ビア（mm）: 真ん中、外の半径、内の半径。幹線の先に一つ、枝の先に三つ</summary>
        static readonly Vector4[] CircuitVias =
        {
            new Vector4(5.3f, -36.75f, 1.15f, 0.55f),
            new Vector4(8.1f, -30.1f, 1.0f, 0.48f),
            new Vector4(2.6f, -32.6f, 1.0f, 0.48f),
            new Vector4(-8.1f, -36.1f, 1.0f, 0.48f),
        };
        /// <summary>四角い端子（mm）: 真ん中と、一辺の長さ。真ん中の幹線の先と、外の一本の先</summary>
        static readonly Vector3[] CircuitPads =
        {
            new Vector3(0f, -45.2f, 2.4f),
            new Vector3(-5.3f, -40.2f, 1.8f),
        };
        /// <summary>幹線と枝の太さ（m）</summary>
        const float TrunkWidth = 0.0010f, BranchWidth = 0.0008f;
        /// <summary>線が肌から立つ高さと、端子とビアの高さ（m）。差込口の輪（<see cref="PortRise"/>）より低い</summary>
        const float CircuitRise = 0.00008f, CircuitPadRise = 0.0001f;
        /// <summary>線と端子とビアの縁を、肌の下へ沈める深さ（m）</summary>
        const float CircuitSkirt = 0.0004f;
        /// <summary>線の長さの向きを刻む間（m）。肌の網の折れ目が線の面より上に出ないよう、差込口の輪（<see cref="PortStep"/>）より細かく</summary>
        const float CircuitStep = 0.0005f;
        /// <summary>回路の下の肌を集める半径（m）。差込口の真ん中から端子の先まで 4.6 cm</summary>
        const float CircuitReach = 0.07f;

        /// <summary>
        /// 回路の意匠の mesh（体の骨で曲がる形）。線・ビア・端子を差込口の枠で組み、頂点ごとに skin（その所の肌の高さ）へ重ねてから、
        /// その下の肌の三角の動きで付ける（<see cref="SkinMesh"/>）
        /// </summary>
        static Mesh CircuitMesh(System.Func<float, float, float> skin, Matrix4x4 frame, Vector3 normal, SkinPatch patch, out Transform[] bones)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            System.Func<float, float, float, int> add = (x, y, h) =>
            {
                verts.Add(new Vector3(x, y, skin(x, y) + h));
                return verts.Count - 1;
            };
            // 三角を、want（枠の向き）の側を表にして加える
            System.Action<int, int, int, Vector3> tri = (a, b, c, want) =>
            {
                var n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                if (Vector3.Dot(n, want) < 0f) tris.AddRange(new[] { a, c, b });
                else tris.AddRange(new[] { a, b, c });
            };
            foreach (var line in CircuitTrunks) CircuitLine(line, TrunkWidth, add, tri);
            foreach (var line in CircuitBranches) CircuitLine(line, BranchWidth, add, tri);
            foreach (var v in CircuitVias) CircuitVia(new Vector2(v.x, v.y) * 0.001f, v.z * 0.001f, v.w * 0.001f, add, tri);
            foreach (var pad in CircuitPads) CircuitPad(new Vector2(pad.x, pad.y) * 0.001f, pad.z * 0.001f, add, tri);
            return SkinMesh(verts, tris, frame, normal, patch, out bones);
        }

        /// <summary>
        /// 一本の線（mm の折れ線、太さ width m）。長さの向きに刻み、断面は上の面（縁・真ん中・縁）と、両の縁から肌の下へ下る壁。
        /// 折れの所は、両の辺の向きの間の向きへ、太さが保たれる分だけ張り出す
        /// </summary>
        static void CircuitLine(Vector2[] mm, float width, System.Func<float, float, float, int> add, System.Action<int, int, int, Vector3> tri)
        {
            var pts = new List<Vector2>();
            foreach (var q in mm) pts.Add(q * 0.001f);
            // 刻んだ点と、そこの横の向き（左へ。長さは張り出しの分を含む）
            var at = new List<Vector2>();
            var side = new List<Vector2>();
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[i + 1];
                var n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / CircuitStep));
                for (var k = i == 0 ? 0 : 1; k <= n; k++)
                {
                    var p = Vector2.Lerp(a, b, (float)k / n);
                    var dir = (b - a).normalized;
                    var left = new Vector2(-dir.y, dir.x);
                    if (k == n && i + 2 < pts.Count)
                    {
                        var next = (pts[i + 2] - b).normalized;
                        var mid = (left + new Vector2(-next.y, next.x)).normalized;
                        left = mid / Mathf.Max(0.5f, Vector2.Dot(mid, left));
                    }
                    at.Add(p);
                    side.Add(left);
                }
            }
            var half = width * 0.5f;
            var up = Vector3.forward;
            int pl = -1, pc = -1, pr = -1, wlt = -1, wlb = -1, wrt = -1, wrb = -1;
            for (var i = 0; i < at.Count; i++)
            {
                var l = at[i] + side[i] * half;
                var r = at[i] - side[i] * half;
                var cl = add(l.x, l.y, CircuitRise);
                var cc = add(at[i].x, at[i].y, CircuitRise);
                var cr = add(r.x, r.y, CircuitRise);
                var lt = add(l.x, l.y, CircuitRise);
                var lb = add(l.x, l.y, -CircuitSkirt);
                var rt = add(r.x, r.y, CircuitRise);
                var rb = add(r.x, r.y, -CircuitSkirt);
                var outL = new Vector3(side[i].x, side[i].y, 0f);
                if (i > 0)
                {
                    tri(pl, pc, cc, up);
                    tri(pl, cc, cl, up);
                    tri(pc, pr, cr, up);
                    tri(pc, cr, cc, up);
                    tri(wlt, wlb, lb, outL);
                    tri(wlt, lb, lt, outL);
                    tri(wrt, wrb, rb, -outL);
                    tri(wrt, rb, rt, -outL);
                }
                if (i == 0 || i == at.Count - 1)
                {
                    // 端のふた
                    var along = i == 0 ? at[0] - at[1] : at[i] - at[i - 1];
                    var o = new Vector3(along.x, along.y, 0f).normalized;
                    var a = add(l.x, l.y, CircuitRise);
                    var b = add(r.x, r.y, CircuitRise);
                    var c = add(r.x, r.y, -CircuitSkirt);
                    var d = add(l.x, l.y, -CircuitSkirt);
                    tri(a, b, c, o);
                    tri(a, c, d, o);
                }
                pl = cl; pc = cc; pr = cr; wlt = lt; wlb = lb; wrt = rt; wrb = rb;
            }
        }

        /// <summary>ビア（小さな輪）。真ん中 c、外の半径 outer、内の半径 inner（m）。上の面と、外と内の壁。穴の中は肌が見える</summary>
        static void CircuitVia(Vector2 c, float outer, float inner, System.Func<float, float, float, int> add, System.Action<int, int, int, Vector3> tri)
        {
            const int seg = 24;
            var mid = (outer + inner) * 0.5f;
            var radii = new[] { inner, mid, outer };
            var top = new int[3, seg];
            var wallOut = new int[2, seg];
            var wallIn = new int[2, seg];
            for (var k = 0; k < seg; k++)
            {
                var a = k * Mathf.PI * 2f / seg;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                for (var j = 0; j < 3; j++)
                {
                    var p = c + d * radii[j];
                    top[j, k] = add(p.x, p.y, CircuitPadRise);
                }
                var po = c + d * outer;
                var pi = c + d * inner;
                wallOut[0, k] = add(po.x, po.y, CircuitPadRise);
                wallOut[1, k] = add(po.x, po.y, -CircuitSkirt);
                wallIn[0, k] = add(pi.x, pi.y, CircuitPadRise);
                wallIn[1, k] = add(pi.x, pi.y, -CircuitSkirt);
            }
            for (var k = 0; k < seg; k++)
            {
                var k1 = (k + 1) % seg;
                var a = k * Mathf.PI * 2f / seg;
                var o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                for (var j = 0; j < 2; j++)
                {
                    tri(top[j, k], top[j + 1, k], top[j + 1, k1], Vector3.forward);
                    tri(top[j, k], top[j + 1, k1], top[j, k1], Vector3.forward);
                }
                tri(wallOut[0, k], wallOut[1, k], wallOut[1, k1], o);
                tri(wallOut[0, k], wallOut[1, k1], wallOut[0, k1], o);
                tri(wallIn[0, k], wallIn[1, k], wallIn[1, k1], -o);
                tri(wallIn[0, k], wallIn[1, k1], wallIn[0, k1], -o);
            }
        }

        /// <summary>四角い端子。真ん中 c、一辺 size（m）。上の面は升目に刻み（肌の曲がりに沿わせる）、四辺に壁</summary>
        static void CircuitPad(Vector2 c, float size, System.Func<float, float, float, int> add, System.Action<int, int, int, Vector3> tri)
        {
            var n = Mathf.Max(2, Mathf.CeilToInt(size / CircuitStep));
            var grid = new int[n + 1, n + 1];
            var h = size * 0.5f;
            for (var i = 0; i <= n; i++)
                for (var j = 0; j <= n; j++)
                    grid[i, j] = add(c.x - h + size * i / n, c.y - h + size * j / n, CircuitPadRise);
            for (var i = 0; i < n; i++)
                for (var j = 0; j < n; j++)
                {
                    tri(grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], Vector3.forward);
                    tri(grid[i, j], grid[i + 1, j + 1], grid[i, j + 1], Vector3.forward);
                }
            // 四辺の壁（辺ごとに刻む）
            var corners = new[] { new Vector2(-h, -h), new Vector2(h, -h), new Vector2(h, h), new Vector2(-h, h) };
            for (var e = 0; e < 4; e++)
            {
                var a = corners[e];
                var b = corners[(e + 1) % 4];
                var o2 = (a + b).normalized;
                var o = new Vector3(o2.x, o2.y, 0f);
                for (var k = 0; k < n; k++)
                {
                    var p0 = c + Vector2.Lerp(a, b, (float)k / n);
                    var p1 = c + Vector2.Lerp(a, b, (float)(k + 1) / n);
                    var t0 = add(p0.x, p0.y, CircuitPadRise);
                    var t1 = add(p1.x, p1.y, CircuitPadRise);
                    var b0 = add(p0.x, p0.y, -CircuitSkirt);
                    var b1 = add(p1.x, p1.y, -CircuitSkirt);
                    tri(t0, b0, b1, o);
                    tri(t0, b1, t1, o);
                }
            }
        }

        /// <summary>差込口の輪か穴を、体の骨で曲がる形として parent の子 name に置く。bones は mesh の骨の枠ごとの骨（<see cref="PortMesh"/>）</summary>
        static void PortPart(Transform parent, string name, Mesh mesh, Material material, SkinPatch patch, Transform[] bones)
        {
            var t = parent.Find(name);
            var go = t != null ? t.gameObject : new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var r = Need<SkinnedMeshRenderer>(go);
            r.sharedMesh = mesh;
            r.bones = bones;
            r.rootBone = patch.smr.rootBone;
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // 小さいので、画面の外でも曲げ続けて境の箱を気にしない
            r.updateWhenOffscreen = true;
        }

        /// <summary>体の肌の、差込口のまわりの三角と、頂点の骨の重み（今の姿勢）</summary>
        sealed class SkinPatch
        {
            public SkinnedMeshRenderer smr;
            public Vector3[] world;
            public List<int> tris = new List<int>();
            public BoneWeight[] weights;
            public Matrix4x4[] bind;
        }

        /// <summary>体の肌（頭の影だけを落とすものを除いた、いちばん大きな肌）の、centre から reach までの三角を集める</summary>
        static SkinPatch Patch(Animator an, Vector3 centre, float reach)
        {
            SkinnedMeshRenderer body = null;
            foreach (var smr in an.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (smr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly && !SkinPoint.Rides(smr) && smr.sharedMesh != null
                    && (body == null || smr.sharedMesh.vertexCount > body.sharedMesh.vertexCount))
                    body = smr;
            var patch = new SkinPatch { smr = body, weights = body.sharedMesh.boneWeights, bind = body.sharedMesh.bindposes };
            var baked = new Mesh();
            body.BakeMesh(baked, true);
            var v = baked.vertices;
            patch.world = new Vector3[v.Length];
            for (var i = 0; i < v.Length; i++) patch.world[i] = body.transform.TransformPoint(v[i]);
            var tris = baked.triangles;
            var r2 = reach * reach;
            for (var i = 0; i < tris.Length; i += 3)
            {
                if ((patch.world[tris[i]] - centre).sqrMagnitude > r2 && (patch.world[tris[i + 1]] - centre).sqrMagnitude > r2
                    && (patch.world[tris[i + 2]] - centre).sqrMagnitude > r2) continue;
                patch.tris.Add(tris[i]);
                patch.tris.Add(tris[i + 1]);
                patch.tris.Add(tris[i + 2]);
            }
            Object.DestroyImmediate(baked);
            return patch;
        }

        /// <summary>
        /// from から dir へ伸ばした線が抜ける patch の肌の三角（いちばん遠い所）。corner はその三角の三つの頂点の番号、
        /// share はその点での三つの頂点の分け前（和が 1）。当たらなければ false
        /// </summary>
        static bool SkinAt(SkinPatch patch, Vector3 from, Vector3 dir, int[] corner, float[] share)
        {
            float best = 0f, bu = 0f, bv = 0f;
            var at = -1;
            for (var i = 0; i + 2 < patch.tris.Count; i += 3)
            {
                float t, u, w;
                if (!Ray(from, dir, patch.world[patch.tris[i]], patch.world[patch.tris[i + 1]], patch.world[patch.tris[i + 2]], out t, out u, out w)) continue;
                if (t <= 0f || t <= best) continue;
                best = t;
                at = i;
                bu = u;
                bv = w;
            }
            if (at < 0) return false;
            corner[0] = patch.tris[at];
            corner[1] = patch.tris[at + 1];
            corner[2] = patch.tris[at + 2];
            share[0] = 1f - bu - bv;
            share[1] = bu;
            share[2] = bv;
            return true;
        }

        /// <summary>骨の枠 i の、束ねた姿勢の点を今の姿勢の世界へ移す置き方（骨の今の置き方 × 束ねた置き方）</summary>
        static Matrix4x4 BoneMatrix(SkinPatch patch, int i)
        {
            return patch.smr.bones[i].localToWorldMatrix * patch.bind[i];
        }

        /// <summary>骨で曲がる mesh を path に残す。在れば中身だけ入れ替えて、場面からの参照を切らない</summary>
        static Mesh SaveSkinned(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.SetVertices(new List<Vector3>(mesh.vertices));
            existing.SetNormals(new List<Vector3>(mesh.normals));
            existing.boneWeights = mesh.boneWeights;
            existing.bindposes = mesh.bindposes;
            existing.SetTriangles(mesh.triangles, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        /// <summary>差込口の輪の色。肌の上で分かる、少し明るい鋼</summary>
        public static readonly Color PortColour = new Color(0.62f, 0.64f, 0.67f);

        /// <summary>
        /// 手首の差込口の置き方（世界の位置と向き）。手の骨の付け根から肘の側へ fromWrist（ふつうは <see cref="JackFromWrist"/>）戻った所の、
        /// 手のひらの側で親指へ寄った皮膚の上（脈を取る所）。腕の芯から、手のひらの向きを親指の側へ <see cref="PortAround"/> 度倒した向きに伸ばした所。
        /// flat は輪の枠（前 +Z をそこの肌の面に直交させる。上 +Y は手の先の側）。rotation はジャックの軸で、輪の枠と同じ
        /// （ジャックを肌から 40 度倒して挿すと、径 23.6 mm の座金の半分が肌に埋まり、埋まり残りの縁が肌から欠片のように突き出た）。
        /// 手のひらの向きは、手の骨と指の付け根の骨から求める（<see cref="BodyPoser.PalmDir"/>）。surface は今の姿勢の体の面（<see cref="BodySurface"/>）。
        /// 片割れ（模型ごと裏返した鏡像）では、片割れ本人の左の手首に来る
        /// </summary>
        public static void PortPose(Animator an, float fromWrist, List<Vector3> surface, out Vector3 position, out Quaternion rotation, out Quaternion flat)
        {
            var wrist = an.GetBoneTransform(HumanBodyBones.RightHand);
            var lower = an.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var along = (wrist.position - lower.position).normalized;
            // 裏返した模型（片割れ）の右の手は、形の上では左の手なので、手のひらの向きの求め方も左右を入れ替える
            var palm = BodyPoser.PalmDir(an, false) * (an.transform.localToWorldMatrix.determinant < 0f ? -1f : 1f);
            palm = Vector3.ProjectOnPlane(palm, along).normalized;
            var thumb = Vector3.ProjectOnPlane(an.GetBoneTransform(HumanBodyBones.RightThumbProximal).position - wrist.position, along);
            thumb = Vector3.ProjectOnPlane(thumb, palm).normalized;
            var side = Vector3.Cross(palm, thumb);
            var put = Quaternion.AngleAxis(PortAround, side) * palm;
            var at = wrist.position - along * fromWrist;
            Vector3 normal;
            var reach = Hit(surface, at, put, 0.08f, out normal);
            position = at + put * reach;
            // 肌が見つからなければ、置く向きのまま
            if (reach <= 0f || normal.sqrMagnitude < 1e-8f) normal = put;
            flat = Quaternion.LookRotation(normal, Vector3.ProjectOnPlane(along, normal));
            rotation = flat;
        }

        /// <summary>
        /// 差込口の輪か穴の mesh（体の骨で曲がる形）。profile は断面（中心からの半径、肌からの高さ）の並びで、隣どうしを帯で結ぶ。
        /// 断面は内から外へ並べる。高さは頂点ごとに skin（その所の肌の高さ）へ重ねる。floor が数なら、最初の（いちばん内の）輪をその高さの平らな面で塞ぐ。
        /// 頂点は輪の枠 frame（肌の面、m）で作って世界へ置き、その真下の肌の三角（patch）の動きをそのまま混ぜて付ける。
        /// 肌の三角の中の点は、三つの頂点がそれぞれの重みで骨に付いて動いた後の、三つの頂点の間の点になる。これは骨ごとに違う点
        /// （骨 i の重みで三つの頂点を混ぜた所）を骨ごとに動かして合わせた所なので、頂点ごとに骨ごとの枠を持たせ（ずらしの近い枠はまとめる）、
        /// その枠の束ねた置き方を、骨 i の点の所へずらす。こうすると輪の頂点は、どの姿勢でもその下の肌の点から同じだけ離れる
        /// （一つの重みで一つの点を動かすと、手首を曲げたときに三角の中で重みの違う分だけ肌と離れる）。
        /// bones は mesh の骨の枠ごとの骨（同じ骨が何度も出る）。
        /// 片割れ（裏返した模型）では束ねた枠が裏返るので、面の向き（三角の巡り）も戻す
        /// </summary>
        static Mesh PortMesh(Vector2[] profile, System.Func<float, float, float> skin, float floor, Matrix4x4 frame, Vector3 normal, SkinPatch patch,
            out Transform[] bones)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (var p = 0; p < profile.Length; p++)
                for (var k = 0; k < PortSegments; k++)
                {
                    var a = k * Mathf.PI * 2f / PortSegments;
                    var x = Mathf.Cos(a) * profile[p].x;
                    var y = Mathf.Sin(a) * profile[p].x;
                    verts.Add(new Vector3(x, y, skin(x, y) + profile[p].y));
                }
            for (var p = 0; p + 1 < profile.Length; p++)
                for (var k = 0; k < PortSegments; k++)
                {
                    var k1 = (k + 1) % PortSegments;
                    int a = p * PortSegments + k, b = p * PortSegments + k1, c = (p + 1) * PortSegments + k, d = (p + 1) * PortSegments + k1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            if (!float.IsNaN(floor))
            {
                var mid = verts.Count;
                verts.Add(new Vector3(0f, 0f, floor));
                for (var k = 0; k < PortSegments; k++) tris.AddRange(new[] { mid, k, (k + 1) % PortSegments });
            }
            return SkinMesh(verts, tris, frame, normal, patch, out bones);
        }

        /// <summary>
        /// 輪の枠 frame（肌の面、m）で作った頂点と三角を、その真下の肌の三角（patch）の動きをそのまま混ぜて付けた、体の骨で曲がる mesh にする。
        /// 付け方は <see cref="PortMesh"/> の説明のとおり（頂点ごとに骨ごとの枠を持たせ、枠の束ねた置き方を骨 i の点の所へずらす）。
        /// 三角は、今の姿勢の世界で外（肌から離れる側）を向く巡りで渡す
        /// </summary>
        static Mesh SkinMesh(List<Vector3> verts, List<int> tris, Matrix4x4 frame, Vector3 normal, SkinPatch patch, out Transform[] bones)
        {
            var rest = patch.smr.sharedMesh.vertices;
            var slotBones = new List<Transform>();
            var slotBind = new List<Matrix4x4>();
            var slots = new Dictionary<string, int>();
            var weights = new BoneWeight[verts.Count];
            var corner = new int[3];
            var share = new float[3];
            var flip = false;
            // 当たらなかった頂点は、一つ前の頂点の骨の混ぜ方で、ずらさずに付ける
            Dictionary<int, float> last = null;
            for (var i = 0; i < verts.Count; i++)
            {
                var world = frame.MultiplyPoint3x4(verts[i]);
                // 骨ごとの、三つの頂点を骨の重みで混ぜた量（重みの和と、束ねた姿勢の位置の和）
                var mass = new Dictionary<int, float>();
                var sum = new Dictionary<int, Vector3>();
                var hit = SkinAt(patch, world - normal * 0.02f, normal, corner, share);
                var under = Vector3.zero;
                var skinWorld = Vector3.zero;
                if (hit)
                {
                    for (var c = 0; c < 3; c++)
                    {
                        var j = corner[c];
                        var part = share[c];
                        under += rest[j] * part;
                        skinWorld += patch.world[j] * part;
                        var bw = patch.weights[j];
                        System.Action<int, float> add = (b, k) =>
                        {
                            if (k <= 0f) return;
                            float m;
                            Vector3 s;
                            mass.TryGetValue(b, out m);
                            sum.TryGetValue(b, out s);
                            mass[b] = m + k * part;
                            sum[b] = s + rest[j] * (k * part);
                        };
                        add(bw.boneIndex0, bw.weight0);
                        add(bw.boneIndex1, bw.weight1);
                        add(bw.boneIndex2, bw.weight2);
                        add(bw.boneIndex3, bw.weight3);
                    }
                    last = mass;
                }
                else if (last != null) mass = new Dictionary<int, float>(last);
                else mass[0] = 1f;
                // 重い方から四つの骨。和を 1 にする
                var order = new List<KeyValuePair<int, float>>(mass);
                order.Sort((x, y) => y.Value.CompareTo(x.Value));
                var n = Mathf.Min(4, order.Count);
                var total = 0f;
                for (var k = 0; k < n; k++) total += order[k].Value;
                // 今の姿勢の、混ぜた置き方（向きの分で、肌から頂点までの離れを束ねた姿勢へ戻す）
                var blend = new Matrix4x4();
                for (var k = 0; k < n; k++)
                {
                    var b = BoneMatrix(patch, order[k].Key);
                    for (var e = 0; e < 16; e++) blend[e] += b[e] * (order[k].Value / total);
                }
                if (i == 0) flip = blend.determinant < 0f;
                var idx = new int[4];
                var wt = new float[4];
                // 骨ごとの枠のずらしで動く分（今の姿勢の世界）。頂点はこの分を除いた所へ置く
                var moved = Vector3.zero;
                for (var k = 0; k < n; k++)
                {
                    var bone = order[k].Key;
                    // 骨 i の点（三つの頂点を骨 i の重みで混ぜた所）へ、束ねた置き方をずらす。ずらしは PortShiftGrid に揃え、同じ枠は使い回す
                    var shift = hit ? sum[bone] / order[k].Value - under : Vector3.zero;
                    shift = new Vector3(Mathf.Round(shift.x / PortShiftGrid), Mathf.Round(shift.y / PortShiftGrid), Mathf.Round(shift.z / PortShiftGrid)) * PortShiftGrid;
                    var key = bone + ":" + shift.x.ToString("R") + ":" + shift.y.ToString("R") + ":" + shift.z.ToString("R");
                    int slot;
                    if (!slots.TryGetValue(key, out slot))
                    {
                        slot = slotBones.Count;
                        slots[key] = slot;
                        slotBones.Add(patch.smr.bones[bone]);
                        slotBind.Add(patch.bind[bone] * Matrix4x4.Translate(shift));
                    }
                    idx[k] = slot;
                    wt[k] = order[k].Value / total;
                    moved += BoneMatrix(patch, bone).MultiplyVector(shift) * wt[k];
                }
                // 頂点の束ねた姿勢の位置。どの骨の枠で動かしても、今の姿勢でちょうど world に来る所
                var at = blend.inverse.MultiplyPoint3x4(world - moved);
                weights[i] = new BoneWeight
                {
                    boneIndex0 = idx[0], weight0 = wt[0],
                    boneIndex1 = idx[1], weight1 = wt[1],
                    boneIndex2 = idx[2], weight2 = wt[2],
                    boneIndex3 = idx[3], weight3 = wt[3],
                };
                verts[i] = at;
            }
            if (flip)
                for (var i = 0; i < tris.Count; i += 3) { var s = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = s; }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.boneWeights = weights;
            mesh.bindposes = slotBind.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            bones = slotBones.ToArray();
            return mesh;
        }

        /// <summary>体の面の三角（今の姿勢、世界の位置）。頭の影だけを落とすものは除く</summary>
        static List<Vector3> BodySurface(Animator an)
        {
            var list = new List<Vector3>();
            foreach (var smr in an.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly || SkinPoint.Rides(smr)) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var v = baked.vertices;
                for (var s = 0; s < baked.subMeshCount; s++)
                    foreach (var i in baked.GetTriangles(s)) list.Add(smr.transform.TransformPoint(v[i]));
                Object.DestroyImmediate(baked);
            }
            return list;
        }

        /// <summary>
        /// from から dir へ伸ばした線が、三角の並び surface を抜けるいちばん遠い所までの距離（reach まで。無ければ 0）と、
        /// そこの三角の面の向き（dir の側へ向けたもの）
        /// </summary>
        static float Hit(List<Vector3> surface, Vector3 from, Vector3 dir, float reach, out Vector3 normal)
        {
            var best = 0f;
            normal = Vector3.zero;
            for (var i = 0; i + 2 < surface.Count; i += 3)
            {
                float t;
                if (!Ray(from, dir, surface[i], surface[i + 1], surface[i + 2], out t)) continue;
                if (t <= 0f || t > reach || t <= best) continue;
                best = t;
                normal = Vector3.Cross(surface[i + 1] - surface[i], surface[i + 2] - surface[i]).normalized;
                if (Vector3.Dot(normal, dir) < 0f) normal = -normal;
            }
            return best;
        }

        /// <summary>
        /// from から dir へ伸ばした線が抜ける肌の三角（いちばん遠い所、8 cm まで）の、その点の骨の重み（三つの頂点の重みを点の場所で混ぜ、
        /// 大きい方から四つ）と、骨ごとの frame（世界の置き方）の置き方を求める。<see cref="SkinPoint"/> がこれで肌に貼り付く。
        /// 置き方は、肌の頂点と同じ骨の混ぜ方（骨の今の置き方と束ねた置き方）で frame を表したもの。今の姿勢で混ぜると frame に戻る
        /// </summary>
        static bool SkinBinding(Animator an, Vector3 from, Vector3 dir, Matrix4x4 frame, out Transform[] bones, out float[] weights, out Matrix4x4[] offsets)
        {
            bones = null;
            weights = null;
            offsets = null;
            dir = dir.normalized;
            SkinnedMeshRenderer at = null;
            int ia = 0, ib = 0, ic = 0;
            float best = 0f, bu = 0f, bv = 0f;
            foreach (var smr in an.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly || SkinPoint.Rides(smr)) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var v = baked.vertices;
                for (var k = 0; k < v.Length; k++) v[k] = smr.transform.TransformPoint(v[k]);
                var tris = baked.triangles;
                for (var i = 0; i < tris.Length; i += 3)
                {
                    float t, u, w;
                    if (!Ray(from, dir, v[tris[i]], v[tris[i + 1]], v[tris[i + 2]], out t, out u, out w)) continue;
                    if (t <= 0f || t > 0.08f || t <= best) continue;
                    best = t;
                    at = smr;
                    ia = tris[i];
                    ib = tris[i + 1];
                    ic = tris[i + 2];
                    bu = u;
                    bv = w;
                }
                Object.DestroyImmediate(baked);
            }
            if (at == null) return false;
            var bw = at.sharedMesh.boneWeights;
            var bind = at.sharedMesh.bindposes;
            var all = at.bones;
            // 点の場所での三つの頂点の割合（a + u(b - a) + w(c - a)）
            var mix = new Dictionary<int, float>();
            System.Action<BoneWeight, float> add = (b, k) =>
            {
                float s;
                if (b.weight0 > 0f) { mix.TryGetValue(b.boneIndex0, out s); mix[b.boneIndex0] = s + b.weight0 * k; }
                if (b.weight1 > 0f) { mix.TryGetValue(b.boneIndex1, out s); mix[b.boneIndex1] = s + b.weight1 * k; }
                if (b.weight2 > 0f) { mix.TryGetValue(b.boneIndex2, out s); mix[b.boneIndex2] = s + b.weight2 * k; }
                if (b.weight3 > 0f) { mix.TryGetValue(b.boneIndex3, out s); mix[b.boneIndex3] = s + b.weight3 * k; }
            };
            add(bw[ia], 1f - bu - bv);
            add(bw[ib], bu);
            add(bw[ic], bv);
            var order = new List<KeyValuePair<int, float>>(mix);
            order.Sort((x, y) => y.Value.CompareTo(x.Value));
            var n = Mathf.Min(4, order.Count);
            var total = 0f;
            for (var i = 0; i < n; i++) total += order[i].Value;
            if (total <= 0f) return false;
            bones = new Transform[n];
            weights = new float[n];
            offsets = new Matrix4x4[n];
            // 今の姿勢で肌の頂点を置く混ぜ方（骨の今の置き方 × 束ねた置き方の重みつきの和）
            var skin = new Matrix4x4();
            for (var i = 0; i < n; i++)
            {
                var j = order[i].Key;
                bones[i] = all[j];
                weights[i] = order[i].Value / total;
                var m = all[j].localToWorldMatrix * bind[j];
                for (var k = 0; k < 16; k++) skin[k] += m[k] * weights[i];
            }
            var local = skin.inverse * frame;
            for (var i = 0; i < n; i++) offsets[i] = bind[order[i].Key] * local;
            return true;
        }

        /// <summary>
        /// from（腕の芯）から dir の向きに、体の面の皮膚までの距離。体の面を今の姿勢で焼き、dir へ伸ばした線が面の三角を抜ける所のうち
        /// いちばん遠い所（8 cm まで）で測る。体の面は粗い（前腕の頂点の間が 2 cm ほどある）ので、頂点ではなく三角で測る。
        /// within は使わない（互換のため残す）
        /// </summary>
        public static float Skin(Animator an, Vector3 from, Vector3 dir, float within)
        {
            var best = 0f;
            dir = dir.normalized;
            foreach (var smr in an.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly || SkinPoint.Rides(smr)) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var v = baked.vertices;
                for (var k = 0; k < v.Length; k++) v[k] = smr.transform.TransformPoint(v[k]);
                for (var s = 0; s < baked.subMeshCount; s++)
                {
                    var tris = baked.GetTriangles(s);
                    for (var i = 0; i < tris.Length; i += 3)
                    {
                        float t;
                        if (!Ray(from, dir, v[tris[i]], v[tris[i + 1]], v[tris[i + 2]], out t)) continue;
                        if (t > 0f && t <= 0.08f) best = Mathf.Max(best, t);
                    }
                }
                Object.DestroyImmediate(baked);
            }
            return best;
        }

        /// <summary>線と三角の交わり（Möller–Trumbore）。裏表は問わない</summary>
        static bool Ray(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            float u, w;
            return Ray(o, d, a, b, c, out t, out u, out w);
        }

        /// <summary>線と三角の交わり。交わる点は a + u(b - a) + w(c - a)</summary>
        static bool Ray(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t, out float u, out float w)
        {
            t = 0f;
            u = 0f;
            w = 0f;
            var e1 = b - a;
            var e2 = c - a;
            var p = Vector3.Cross(d, e2);
            var det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-10f) return false;
            var inv = 1f / det;
            var s = o - a;
            u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f) return false;
            var q = Vector3.Cross(s, e1);
            w = Vector3.Dot(d, q) * inv;
            if (w < 0f || u + w > 1f) return false;
            t = Vector3.Dot(e2, q) * inv;
            return true;
        }

        /// <summary>
        /// ケーブルの出方。肘掛けの中に巻き取られていて、端の間に合わせて出し入れされる（<see cref="CableSlack"/> だけ余る）。
        /// 両端は、差込口からは上へ、ジャックからは尻の向きへまっすぐ出す（<see cref="CableStiffness"/>）。
        ///
        /// **長さを決めたままにしない。** 抜いて目の前へ出すには 0.55 m 要るが、刺さっている間は端の間が 12 cm しかない。
        /// 余った 40 cm の輪が手首の下へ垂れて、手と前腕に潜った。
        /// 端をまっすぐ出さずに両端を結ぶだけだと、ケーブルが前腕に沿って寄り、やはり皮膚に潜った
        /// </summary>
        public static void Tune(Cable cable)
        {
            if (cable == null) return;
            var pro = GameObject.Find("Player/Protagonist");
            Tune(cable, pro != null ? pro.GetComponent<Animator>() : null);
        }

        /// <summary>
        /// <see cref="Tune(Cable)"/> に加えて、ケーブルが避ける体の一部を体から測って書く。
        /// 腕（上腕・前腕・手のひら・指・親指）の左右、腿の左右、胴。太さは、その骨に一番重く付いた頂点の、芯からのいちばん遠い距離
        /// </summary>
        public static void Tune(Cable cable, Animator an)
        {
            if (cable == null) return;
            var so = new SerializedObject(cable);
            if (an != null)
            {
                var parts = Avoids(an);
                var list = so.FindProperty("avoid");
                list.arraySize = parts.Count;
                for (var i = 0; i < parts.Count; i++)
                {
                    var e = list.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("from").objectReferenceValue = parts[i].from;
                    e.FindPropertyRelative("to").objectReferenceValue = parts[i].to;
                    e.FindPropertyRelative("radius").floatValue = parts[i].radius;
                }
            }
            so.FindProperty("length").floatValue = CableLength;
            so.FindProperty("radius").floatValue = CableRadius;
            so.FindProperty("clearance").floatValue = CableClearance;
            so.FindProperty("reel").boolValue = true;
            so.FindProperty("reelSlack").floatValue = CableSlack;
            so.FindProperty("stiffness").floatValue = CableStiffness;
            // 差込口からは、上へ、少し外（肘掛けの外）へ向けて出す。前腕の外の側を上って越える
            so.FindProperty("fromAxis").vector3Value = new Vector3(0.35f, 1f, 0f).normalized;
            so.FindProperty("toAxis").vector3Value = Vector3.forward;
            so.ApplyModifiedPropertiesWithoutUndo();
            // 被膜は明るい灰の樹脂にする。前は机の墨（Ink）と同じ黒で、暗い部屋と机に溶けて、抜いたジャックから伸びる線が見えなかった
            var r = cable.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = Tinted("Cable", CableColour, 0f, 0.35f);
        }

        /// <summary>ケーブルの被膜の色。暗い部屋でも机と床から分かれる明るさの灰</summary>
        public static readonly Color CableColour = new Color(0.36f, 0.37f, 0.39f);

        /// <summary>巻き取りを切ったときのケーブルの長さ（m）。抜いて目の前へ出したとき、差込口から張って見える長さ</summary>
        public const float CableLength = 0.55f;

        /// <summary>ケーブルが避ける体の一部を、今の姿勢の体の面から測る</summary>
        public static List<Cable.Avoid> Avoids(Animator an)
        {
            System.Func<HumanBodyBones, Transform> B = an.GetBoneTransform;
            var spec = new List<KeyValuePair<HumanBodyBones[], HumanBodyBones[]>>();
            System.Action<HumanBodyBones, HumanBodyBones, HumanBodyBones[]> add = (a, b, own) =>
                spec.Add(new KeyValuePair<HumanBodyBones[], HumanBodyBones[]>(new[] { a, b }, own));
            foreach (var left in new[] { true, false })
            {
                HumanBodyBones F(HumanBodyBones l, HumanBodyBones r) { return left ? l : r; }
                add(F(HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm), F(HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm), new[] { F(HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm) });
                add(F(HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm), F(HumanBodyBones.LeftHand, HumanBodyBones.RightHand), new[] { F(HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm) });
                add(F(HumanBodyBones.LeftHand, HumanBodyBones.RightHand), F(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal), new[] { F(HumanBodyBones.LeftHand, HumanBodyBones.RightHand) });
                add(F(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal), F(HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal),
                    new[] { F(HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal), F(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate), F(HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal),
                        F(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal), F(HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.RightMiddleIntermediate), F(HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal),
                        F(HumanBodyBones.LeftRingProximal, HumanBodyBones.RightRingProximal), F(HumanBodyBones.LeftRingIntermediate, HumanBodyBones.RightRingIntermediate), F(HumanBodyBones.LeftRingDistal, HumanBodyBones.RightRingDistal),
                        F(HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal), F(HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.RightLittleIntermediate), F(HumanBodyBones.LeftLittleDistal, HumanBodyBones.RightLittleDistal) });
                add(F(HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal), F(HumanBodyBones.LeftThumbDistal, HumanBodyBones.RightThumbDistal),
                    new[] { F(HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal), F(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.RightThumbIntermediate), F(HumanBodyBones.LeftThumbDistal, HumanBodyBones.RightThumbDistal) });
                add(F(HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg), F(HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg), new[] { F(HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg) });
            }
            add(HumanBodyBones.Spine, HumanBodyBones.UpperChest, new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest });

            // 体の面を焼き、頂点ごとの一番重い骨をその場で調べて数える
            var smr = SkinPoint.BodyOf(an);
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            var verts = baked.vertices;
            var weights = smr.sharedMesh.boneWeights;
            var bones = smr.bones;
            var list = new List<Cable.Avoid>();
            foreach (var part in spec)
            {
                var a = B(part.Key[0]);
                var b = B(part.Key[1]);
                if (a == null || b == null) continue;
                var own = new HashSet<Transform>();
                foreach (var o in part.Value) { var t = B(o); if (t != null) own.Add(t); }
                var r = 0f;
                for (var i = 0; i < verts.Length && i < weights.Length; i++)
                {
                    var bone = bones[weights[i].boneIndex0];
                    if (!own.Contains(bone)) continue;
                    var p = smr.transform.TransformPoint(verts[i]);
                    r = Mathf.Max(r, Vector3.Distance(p, Cable.Closest(a.position, b.position, p)));
                }
                list.Add(new Cable.Avoid { from = a, to = b, radius = r });
            }
            Object.DestroyImmediate(baked);
            return list;
        }

        /// <summary>ケーブルが体から取るゆとり（m）。体を芯と太さの円柱で見積もるので、親指の付け根のように円柱からはみ出す所のぶん</summary>
        public const float CableClearance = 0.008f;

        /// <summary>ケーブルの太さ（半径 m）。ジャックの尻（半径 4〜6 mm）に揃える</summary>
        public const float CableRadius = 0.006f;

        /// <summary>巻き取り式のケーブルが、端の間の道筋より余る長さ（m）</summary>
        public const float CableSlack = 0.03f;

        /// <summary>ケーブルが両端から軸に沿ってまっすぐ出る長さ（m）</summary>
        public const float CableStiffness = 0.05f;

        /// <summary>座金（ジャックのいちばん太い所）の半径（m）。寝かせて置くときの軸の高さ</summary>
        public const float JackWasherRadius = 0.0118f;

        /// <summary>置き場に寝かせたジャックの軸（尻の向き）。椅子から見た向き。体の左後ろ</summary>
        public static readonly Vector3 RestAxis = new Vector3(-0.71f, 0f, -0.71f).normalized;

        /// <summary>
        /// 抜いた後にジャックを置く場所。右の肘掛けの上の、内の縁の後ろ寄り（右の肘より 10 cm 後ろ）に、横に寝かせて置く（<see cref="RestAxis"/>）。
        /// ケーブルは尻から出て、差込口へ弧を描く。
        ///
        /// **左手が届く所に置く。** 左の肩から右の肘掛けの真ん中の差込口の脇までは 0.60 m あり、腕（0.49 m）と指先までの 12 cm を足しても
        /// 手の向きによっては届かない。内の縁の後ろ寄りなら 0.58 m で届く。
        /// 座面の縁（腿と肘掛けの間）は、肩から下へ遠く（0.64 m）、上体を寄せても届かなかった。
        /// reachFrom は使わなくなった（立てて置いていた頃に、指先の向きを揃えるのに使った）
        /// </summary>
        public static Transform JackRest(Transform chair, Vector3? reachFrom = null)
        {
            var rest = chair.Find("JackRest");
            if (rest == null)
            {
                rest = new GameObject("JackRest").transform;
                rest.SetParent(chair, false);
            }
            // 肘掛けの上面（0.676）に横に寝かせて置く。軸の高さは座金の半径（11.8 mm）だけ上。
            // 尻（ケーブルの出る側）は体の左後ろへ向ける。
            // **立てて置かない。** 立てると、置く左手は手のひらを真下へ向けて体の右後ろへ回り込むことになり、
            // 手のひらのひねりが 180 度近く、手首の曲げが 140 度になって腕が極端に細く見えた。
            // 寝かせると、左手は手のひらを右へ向けた楽な形（ひねり 50 度、曲げ 40 度ほど）で置ける
            rest.localPosition = new Vector3(0.255f, 0.676f + JackWasherRadius, -0.07f);
            rest.localRotation = Quaternion.LookRotation(RestAxis, Vector3.up);
            rest.localScale = Vector3.one;
            return rest;
        }

        /// <summary>主人公の体の骨（Humanoid）</summary>
        static Transform FindBone(HumanBodyBones bone)
        {
            var pro = GameObject.Find("Player/Protagonist");
            var an = pro != null ? pro.GetComponent<Animator>() : null;
            return an != null ? an.GetBoneTransform(bone) : null;
        }

        // ---- 調べられる物のピン --------------------------------------------

        /// <summary>
        /// 地図のピン。丸い頭に穴が開いていて、下が尖っている。
        /// 高さ 1 で、尖った先が原点。置いた点をそのまま指す。
        /// こちらを向かせて距離で伸ばすのは PinMarkers の仕事
        /// </summary>
        [MenuItem("HalfAware/Build the interaction pins")]
        public static void BuildPinsMenu()
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>();
            if (flow == null) { Debug.LogError("SceneFlow が見つからない"); return; }
            var made = BuildPins(flow);
            Selection.activeGameObject = made;
            Mark(made);
        }

        public static GameObject BuildPins(SceneFlow flow)
        {
            var body = new List<Mesh>();
            // 頭。球
            var head = new List<ProcMesh.Ring>();
            for (var i = 0; i <= 12; i++)
            {
                var a = Mathf.PI * i / 12f;
                var r = Mathf.Sin(a) * 0.30f;
                head.Add(new ProcMesh.Ring(new Vector3(0f, 0.70f - Mathf.Cos(a) * 0.30f, 0f), r, r,
                    Quaternion.LookRotation(Vector3.up, Vector3.forward)));
            }
            body.Add(ProcMesh.Loft(head, 12));
            // 首から先端まで
            var spike = new List<ProcMesh.Ring>();
            for (var i = 0; i <= 8; i++)
            {
                var k = i / 8f;
                var y = Mathf.Lerp(0.62f, 0f, k);
                var r = Mathf.Lerp(0.20f, 0.0f, Mathf.Pow(k, 0.65f));
                spike.Add(new ProcMesh.Ring(new Vector3(0f, y, 0f), r, r,
                    Quaternion.LookRotation(Vector3.up, Vector3.forward)));
            }
            body.Add(ProcMesh.Loft(spike, 12, false, false));
            var mesh = ProcMesh.Save(ProcMesh.Combine(body, null), Generated + "Pin.asset");

            // 穴。手前側にだけ見えればよいので、薄い円盤を前へ出す
            var hole = ProcMesh.Save(ProcMesh.Loft(new List<ProcMesh.Ring>
            {
                new ProcMesh.Ring(new Vector3(0f, 0.70f, -0.255f), 0.125f, 0.125f, Quaternion.identity),
                new ProcMesh.Ring(new Vector3(0f, 0.70f, -0.300f), 0.125f, 0.125f, Quaternion.identity),
            }, 10), Generated + "PinHole.asset");

            var old = GameObject.Find("Pins");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Pins");
            var source = new GameObject("PinSource");
            source.transform.SetParent(root.transform, false);
            source.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r0 = source.AddComponent<MeshRenderer>();
            r0.sharedMaterial = Glow("PinHead", new Color(0.96f, 0.74f, 0.22f), 2.2f);
            r0.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var eye = Place(source.transform, "Hole", hole, Tinted("PinHole", new Color(0.06f, 0.05f, 0.05f), 0f, 0.1f));
            eye.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            source.SetActive(false);

            var markers = root.AddComponent<PinMarkers>();
            var so = new SerializedObject(markers);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("pin").objectReferenceValue = source;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>自分で光るマテリアル。暗い部屋でもピンが沈まないように</summary>
        static Material Glow(string name, Color col, float strength)
        {
            var m = Tinted(name, col, 0f, 0.2f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", col * strength);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        // ---- 立ち上がる場所 ---------------------------------------------------

        /// <summary>
        /// 立ち上がって足を下ろす場所を、椅子と机のあいだへ置く。
        /// 椅子の当たりと机に触れない z を、椅子の前から机へ向かって探す
        /// </summary>
        [MenuItem("HalfAware/Fit the standing spot")]
        public static void FitStandSpotMenu()
        {
            var player = GameObject.Find("Player");
            var chair = GameObject.Find("Room/Chair");
            var spot = GameObject.Find("Room/StandSpot");
            if (player == null || chair == null || spot == null) { Debug.LogError("Player / Chair / StandSpot のどれかが無い"); return; }
            var cc = player.GetComponent<CharacterController>();
            var blocker = chair.transform.Find("Blocker");
            var wasOn = blocker != null && blocker.gameObject.activeSelf;
            if (blocker != null) blocker.gameObject.SetActive(true);

            var found = false;
            var at = spot.transform.position;
            // 椅子の前（机寄り）から探す。見つからなければ椅子の後ろへ下がる
            for (var z = 1.72f; z <= 2.10f; z += 0.02f)
            {
                var p = new Vector3(1.5f, 0.05f, z);
                if (!Clear(cc, player.transform, p)) continue;
                at = p;
                found = true;
                break;
            }
            if (!found)
            {
                for (var z = 0.60f; z >= 0.10f; z -= 0.02f)
                {
                    var p = new Vector3(1.5f, 0.05f, z);
                    if (!Clear(cc, player.transform, p)) continue;
                    at = p;
                    found = true;
                    Debug.LogWarning("椅子と机のあいだに立てる幅が無いので、椅子の後ろへ回した");
                    break;
                }
            }
            if (blocker != null) blocker.gameObject.SetActive(wasOn);
            if (!found) { Debug.LogError("立てる場所が見つからない"); return; }
            spot.transform.position = at;
            Debug.Log("立ち上がる場所を " + at.ToString("F3") + " にした");
            Mark(spot);
        }

        /// <summary>その場所に体の当たりが収まるか</summary>
        static bool Clear(CharacterController cc, Transform player, Vector3 at)
        {
            var bottom = at + cc.center + Vector3.up * (-cc.height * 0.5f + cc.radius);
            var top = at + cc.center + Vector3.up * (cc.height * 0.5f - cc.radius);
            foreach (var col in Physics.OverlapCapsule(bottom, top, cc.radius))
            {
                if (col.transform.IsChildOf(player)) continue;
                return false;
            }
            return true;
        }

        // ---- 部屋の埃 -------------------------------------------------------

        /// <summary>
        /// 空中を漂う埃。ごく小さい粒を部屋いっぱいに撒いて、ほとんど動かさない。
        /// 明かりの中を横切ったときにだけ見える程度に留める
        /// </summary>
        [MenuItem("HalfAware/Build the dust")]
        public static void BuildDustMenu()
        {
            var room = GameObject.Find("Room");
            if (room == null) { Debug.LogError("部屋がシーンに無い"); return; }
            var made = BuildDust(room.transform);
            Selection.activeGameObject = made;
            Mark(made);
        }

        public static GameObject BuildDust(Transform room)
        {
            var old = room.Find("Dust");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("Dust");
            go.transform.SetParent(room, false);
            go.transform.position = new Vector3(0f, 1.45f, 0f);
            go.transform.rotation = Quaternion.identity;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 12f;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;                       // 入った瞬間から漂っている
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.004f, 0.016f);
            // 1/3 の解像度で描くので、小さすぎると点にもならない
            main.startSize = new ParticleSystem.MinMaxCurve(0.016f, 0.038f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.88f, 0.87f, 0.84f, 0.34f), new Color(0.80f, 0.80f, 0.78f, 0.62f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.0012f);   // ゆっくり沈む
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 180;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 9f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(5.6f, 2.6f, 5.6f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.006f, 0.010f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);

            // ちらつき。粒が回って光を拾ったり落としたりする
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]{ new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(0.75f, 0.70f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.010f);
            noise.frequency = 0.15f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.03f);
            noise.damping = true;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat("Smoke");            // 煙と同じ柔らかい粒の絵を使い回す
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.None;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.alignment = ParticleSystemRenderSpace.View;
            return go;
        }

        // ---- 煙草の煙 -----------------------------------------------------

        /// <summary>
        /// 口元から立ちのぼる煙。粒は世界の座標で動かすので、首を振っても置き去りになる。
        /// 一人称なので、目の少し下・少し前から出して視界を横切らせる
        /// </summary>
        [MenuItem("HalfAware/Build the cigarette smoke")]
        public static void BuildSmokeMenu()
        {
            var cam = GameObject.Find("Player/Main Camera");
            if (cam == null) { Debug.LogError("カメラが見つからない"); return; }
            var made = BuildSmoke(cam.transform);
            Selection.activeGameObject = made;
            Mark(made);
        }

        public static GameObject BuildSmoke(Transform cam)
        {
            var old = cam.Find("SmokePuffs");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("SmokePuffs");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0.05f, -0.19f, 0.23f);
            go.transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.16f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // 1 粒ずつが丸く見えないよう、薄いものを数多く重ねる
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.82f, 0.81f, 0.78f, 0.17f), new Color(0.74f, 0.73f, 0.71f, 0.26f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.010f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;

            var em = ps.emission;
            em.enabled = true;
            // 吸っているあいだは細く燻るだけ。濃くなるのは吐いたとき
            em.rateOverTime = 5f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.015f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            // 横へは漂う程度。上へ立ちのぼるのが主
            vel.x = new ParticleSystem.MinMaxCurve(-0.018f, 0.018f);
            vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.018f, 0.018f);

            // 吐いた勢いは 1 秒ほどで抜ける。そこから先は上への流れだけが残る
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(0.12f);
            limit.dampen = 0.30f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var grow = new AnimationCurve();
            grow.AddKey(0f, 0.34f);
            grow.AddKey(0.4f, 1.5f);
            grow.AddKey(1f, 3.0f);
            size.size = new ParticleSystem.MinMaxCurve(1f, grow);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]{ new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0.62f, 0.55f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.055f);
            noise.frequency = 0.40f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.10f);
            noise.damping = true;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat("Smoke");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.alignment = ParticleSystemRenderSpace.View;

            var puffs = go.AddComponent<SmokePuffs>();
            var so = new SerializedObject(puffs);
            so.FindProperty("puffs").objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        // ---- 下ごしらえ -------------------------------------------------

        /// <summary>箱 1 つ。板や金具のように、曲面の要らないところに使う</summary>
        static GameObject Slab(Transform parent, string name, Vector3 pos, Vector3 rot, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(rot);
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        /// <summary>付いていれば使い、無ければ足す。作り直しで参照を切らないために</summary>
        static T Need<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static GameObject Place(Transform parent, string name, Mesh mesh, Material mat)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }

        static Material Mat(string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
            if (m == null) Debug.LogWarning("マテリアルが無い: " + name);
            return m;
        }

        /// <summary>Steel を下地に色だけ変えたマテリアルを用意する。無ければ作って残す</summary>
        static Material Tinted(string name, Color col, float metal, float smooth)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Mat("Steel"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        static void Mark(GameObject go)
        {
            EditorUtility.SetDirty(go);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
