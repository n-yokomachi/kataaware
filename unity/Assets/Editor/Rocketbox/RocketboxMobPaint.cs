using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 路地裏の人の肌に描くインプラント（設計メモ 5 節）。
    ///
    /// 銀の地に、光る線と端子。光は自己発光のテクスチャで持たせ、灯りは置かない。
    /// 形は、模型の束ねた姿勢の上に描いた線（<see cref="Stroke"/>）で決める。線の案内の点は骨と顔の骨から置き、
    /// いちばん近い肌の画素の位置へ吸い付けてから、テクスチャの画素ごとに、その画素が載る模型の上の位置から線までの距離で塗る（<see cref="RocketboxPaint.Surface"/>）。
    /// 肌の色から離れた画素（髪・眉・襟・袖）には塗らない。髪の塊や髪の房に覆われた画素（色が肌に近い明るい髪）にも塗らない（<see cref="Shelter"/>）。
    ///
    /// 大胆な形（<see cref="Implant.bold"/>）は、ゲームの見え方（320×180）の 2〜3 m で「光る物が付いている」と一目で読め、5 m でも光の点や線として拾える大きさにしてある。
    /// その距離では画素一つが体の上の 2〜4 cm に当たるので、線は太く、光は強い
    /// </summary>
    public static class RocketboxMobPaint
    {
        /// <summary>インプラントの形。その人の肌の出ている所に合わせて選ぶ</summary>
        public enum Kind
        {
            /// <summary>うなじの差込口。大胆な形は、差込口から首の両脇を回って首の付け根の前へ下りる二本の線を伴う</summary>
            NapePort,
            /// <summary>こめかみの線。大胆な形は、こめかみから頬骨を通って顎の角まで</summary>
            TempleLines,
            /// <summary>前腕の板。大胆な形は、手首から肘まで、光る線が三本</summary>
            ForearmPlate,
            /// <summary>手の甲の光。大胆な形は、甲の真ん中の端子から指の付け根四つと手首へ伸びる線</summary>
            HandGlow,
            /// <summary>首を巻く輪。大胆な形は、うなじの高さから前へ下がって首の付け根（鎖骨の間）を回る太い帯</summary>
            NeckRing,
            /// <summary>目のまわりの輪。大胆な形は、片目を囲む輪と、耳の前へ伸びる線</summary>
            EyeRing,
        }

        /// <summary>
        /// 光の色。この通りの看板のネオンの色（BuildAlley.NeonTint）から拾った。人ごと・部位ごとに色を変え、並んだ人どうしで被らないよう散らす
        /// </summary>
        public static readonly Color
            Pink = new Color(1.00f, 0.18f, 0.66f),      // NeonNerve
            Cyan = new Color(0.22f, 0.93f, 1.00f),      // NeonOpen
            Purple = new Color(0.69f, 0.45f, 1.00f),    // NeonBar
            Red = new Color(1.00f, 0.24f, 0.24f),       // NeonHotel
            Orange = new Color(1.00f, 0.62f, 0.12f),    // NeonArrow
            Teal = new Color(0.22f, 0.89f, 0.84f),      // NeonEye
            Yellow = new Color(1.00f, 0.89f, 0.25f),    // NeonLive
            Green = new Color(0.34f, 1.00f, 0.54f),     // NeonCross
            Rose = new Color(1.00f, 0.29f, 0.54f),      // NeonClub
            Blue = new Color(0.36f, 0.66f, 1.00f),      // NeonRings
            Blush = new Color(1.00f, 0.47f, 0.82f),     // NeonSleep
            Amber = new Color(1.00f, 0.78f, 0.28f);     // NeonNoodle

        /// <summary>ネオン管の芯で、光の色へ混ぜる白の量（0〜1）。芯は白っぽく明るく、縁へ向かって色が濃くなる</summary>
        const float CoreWhite = 0.35f;
        /// <summary>光のにじみの広がり（光る芯の半幅に掛ける）</summary>
        const float Halo = 2.8f;

        /// <summary>一つのインプラント。left は本人の左（左右のあるものだけ）。bold は大きく光る形（通りの人）、そうでなければ小さく目立たない形（売り手）</summary>
        public struct Implant
        {
            public Kind kind;
            public bool left;
            public bool bold;
            public Color color;

            public Implant(Kind kind, bool left, Color color, bool bold = true)
            {
                this.kind = kind;
                this.left = left;
                this.color = color;
                this.bold = bold;
            }

            public override string ToString()
            {
                var side = kind == Kind.NapePort || kind == Kind.NeckRing ? "" : left ? "（左）" : "（右）";
                string name;
                switch (kind)
                {
                    case Kind.NapePort: name = "うなじの差込口"; break;
                    case Kind.TempleLines: name = "こめかみの線"; break;
                    case Kind.ForearmPlate: name = "前腕の板"; break;
                    case Kind.HandGlow: name = "手の甲の光"; break;
                    case Kind.NeckRing: name = "首を巻く帯"; break;
                    default: name = "目のまわりの輪"; break;
                }
                return name + side + (bold ? "" : "（小さめ）");
            }
        }

        /// <summary>銀の地（sRGB）と、縁の暗い線</summary>
        static readonly Color Silver = new Color(0.62f, 0.65f, 0.69f), SilverEdge = new Color(0.28f, 0.30f, 0.33f);
        /// <summary>差込口の穴</summary>
        static readonly Color Socket = new Color(0.05f, 0.05f, 0.06f);

        /// <summary>インプラントを決める骨の位置と向き（模型の根の中、束ねた姿勢）。模型は +z を向き、本人の右が +x</summary>
        public sealed class Frame
        {
            public Vector3 neck, head, eyeL, eyeR, cheekL, cheekR;
            public Vector3 elbowL, elbowR, wristL, wristR;
            /// <summary>指の付け根（人差し指・中指・薬指・小指）</summary>
            public Vector3[] knucklesL, knucklesR;
            /// <summary>手の甲の向き（手のひらの逆）</summary>
            public Vector3 backL, backR;

            public static Frame Of(Animator an)
            {
                var root = an.transform;
                Func<HumanBodyBones, Vector3> B = b => root.InverseTransformPoint(an.GetBoneTransform(b).position);
                var a = RocketboxPaint.Anchors.Of(root);
                return new Frame
                {
                    neck = B(HumanBodyBones.Neck), head = B(HumanBodyBones.Head),
                    eyeL = a.eyeL, eyeR = a.eyeR, cheekL = a.cheekL, cheekR = a.cheekR,
                    elbowL = B(HumanBodyBones.LeftLowerArm), elbowR = B(HumanBodyBones.RightLowerArm),
                    wristL = B(HumanBodyBones.LeftHand), wristR = B(HumanBodyBones.RightHand),
                    knucklesL = new[] { B(HumanBodyBones.LeftIndexProximal), B(HumanBodyBones.LeftMiddleProximal), B(HumanBodyBones.LeftRingProximal), B(HumanBodyBones.LeftLittleProximal) },
                    knucklesR = new[] { B(HumanBodyBones.RightIndexProximal), B(HumanBodyBones.RightMiddleProximal), B(HumanBodyBones.RightRingProximal), B(HumanBodyBones.RightLittleProximal) },
                    backL = -root.InverseTransformDirection(BodyPoser.PalmDir(an, true)),
                    backR = -root.InverseTransformDirection(BodyPoser.PalmDir(an, false)),
                };
            }
        }

        /// <summary>
        /// 肌の上に描く一本の線。案内の点（guide）を細かく刻んで肌へ吸い付けた点の並びにし、そこからの距離で塗る。
        /// silver は銀の地の半幅、glow は光る芯の半幅、node は案内の点に置く端子の半径（0 なら置かない）、socket は端子の奥の暗い穴の半径（m）
        /// </summary>
        public sealed class Stroke
        {
            public readonly List<Vector3> guide = new List<Vector3>();
            public bool closed;
            public float silver, glow, node, socket;
            /// <summary>端子の光る輪の半径（0 なら端子は光る点）</summary>
            public float nodeRing;
            public Color color;
            /// <summary>端子を両端だけに置く（途中の案内の点には置かない）</summary>
            public bool nodeOnlyEnds;
            /// <summary>差込口（案内の点一つに、座金・縁・光る輪・穴）</summary>
            public bool isPort;
            /// <summary>端子だけ（線は無い）</summary>
            public bool nodesOnly;
            /// <summary>
            /// 案内の点から体の外へ向かう向き。案内の点を肌へ落とすとき、この向きの外から体へ向けて見て、最初に当たる面の点へ落とす
            /// （いちばん近い点へ吸い付けると、目のくぼみや顎の下の陰で線が眉や襟へ逃げた）。null なら、いちばん近い点
            /// </summary>
            public Func<Vector3, Vector3> outward;
            /// <summary>吸い付けた点の並び</summary>
            internal Vector3[] path;
            internal Vector3[] nodes;
            internal Bounds bounds;
        }

        // ---- 形 ---------------------------------------------------------------------

        /// <summary>インプラント一つを線の組にする</summary>
        public static List<Stroke> Strokes(Implant im, Frame f)
        {
            var o = new List<Stroke>();
            var side = im.left ? -1f : 1f;
            var eye = im.left ? f.eyeL : f.eyeR;
            Func<float, float, float, Vector3> E = (x, y, z) => eye + new Vector3(x * side, y, z);
            // 頭の横の面は横から、顔の前の面は前から見て落とす
            var sideways = new Vector3(side, 0f, 0.35f).normalized;
            Func<Vector3, Vector3> fromSide = g => sideways;
            Func<Vector3, Vector3> fromFront = g => new Vector3(0.15f * side, 0f, 1f);
            Func<Vector3, Vector3> fromNeck = g => Vector3.ProjectOnPlane(g - f.neck, (f.head - f.neck).normalized);
            switch (im.kind)
            {
                case Kind.TempleLines:
                    if (im.bold)
                    {
                        // こめかみ → 頬骨 → 頬の下 → 顎の角。もう一本、こめかみから頬骨の上へ
                        o.Add(Line(im.color, 0.0065f, 0.0036f, 0.0072f, fromSide,
                            E(0.050f, 0.030f, -0.045f), E(0.052f, 0.012f, -0.022f), E(0.047f, -0.018f, -0.004f),
                            E(0.045f, -0.052f, -0.018f), E(0.044f, -0.082f, -0.048f)));
                        o.Add(Line(im.color, 0.0048f, 0.0028f, 0.0058f, fromSide, E(0.056f, 0.004f, -0.045f), E(0.040f, -0.010f, 0.004f)));
                    }
                    else o.Add(Line(im.color, 0.0030f, 0.0016f, 0.0038f, fromSide, E(0.044f, 0.012f, -0.028f), E(0.046f, 0.004f, -0.070f)));
                    break;
                case Kind.EyeRing:
                    {
                        // 片目を囲む輪（横 2.4 cm・縦 2.2 cm）と、輪の外の端から耳の前へ伸びる線。輪は前から見て顔に落とす
                        var ring = new Stroke { color = im.color, silver = 0.0055f, glow = 0.0032f, closed = true, outward = fromFront };
                        var dots = new Stroke { color = im.color, node = 0.0068f, outward = fromFront };
                        for (var i = 0; i < 24; i++)
                        {
                            var a = i * Mathf.PI * 2f / 24f;
                            var q = E(0.024f * Mathf.Cos(a), 0.022f * Mathf.Sin(a), 0f);
                            ring.guide.Add(q);
                            if (i % 6 == 3) dots.guide.Add(q);
                        }
                        o.Add(ring);
                        o.Add(dots.Nodes());
                        o.Add(Line(im.color, 0.0055f, 0.0032f, 0.0068f, fromSide, E(0.024f, 0f, -0.004f), E(0.050f, -0.004f, -0.025f), E(0.066f, -0.010f, -0.058f)));
                        break;
                    }
                case Kind.NapePort:
                    {
                        // うなじの差込口（半径 1.5 cm）。差込口から首の両脇を回って首の付け根の前へ下りる二本の線
                        var port = Neck(f, 0.62f, 0f, 0.07f);
                        o.Add(new Stroke { color = im.color, silver = 0.015f, socket = 0.0055f, nodeRing = 0.0095f, outward = fromNeck, guide = { port } }.Port());
                        if (im.bold)
                            foreach (var s in new[] { 1f, -1f })
                            {
                                var line = new Stroke { color = im.color, silver = 0.0060f, glow = 0.0036f, node = 0.0070f, outward = fromNeck };
                                for (var k = 1; k <= 8; k++)
                                {
                                    var u = k / 8f;
                                    line.guide.Add(Neck(f, Mathf.Lerp(0.60f, 0.30f, u * u), s * Mathf.Lerp(0.35f, 2.5f, u), 0.07f));
                                }
                                o.Add(line);
                            }
                        break;
                    }
                case Kind.NeckRing:
                    {
                        // うなじの高さから前へ下がって喉の下を回る帯（銀の半幅 1 cm）に、上下二本の光る線と 60 度ごとの端子。襟に隠れない高さ
                        Func<float, float> height = a => Mathf.Lerp(0.66f, 0.40f, (1f - Mathf.Cos(a)) * 0.5f);
                        if (im.bold)
                        {
                            foreach (var dh in new[] { 0f, -0.11f, 0.11f })
                            {
                                var band = new Stroke { color = im.color, closed = true, outward = fromNeck };
                                if (dh == 0f) band.silver = 0.011f;
                                else { band.glow = 0.0036f; band.silver = 0.0045f; }
                                for (var i = 0; i < 32; i++)
                                {
                                    var a = i * Mathf.PI * 2f / 32f;
                                    band.guide.Add(Neck(f, height(a) + dh * 0.5f, a, 0.075f));
                                }
                                o.Add(band);
                            }
                            var nodes = new Stroke { color = im.color, node = 0.0072f, outward = fromNeck };
                            for (var i = 0; i < 6; i++)
                            {
                                var a = i * Mathf.PI * 2f / 6f;
                                nodes.guide.Add(Neck(f, height(a), a, 0.075f));
                            }
                            o.Add(nodes.Nodes());
                        }
                        else
                        {
                            var band = new Stroke { color = im.color, closed = true, silver = 0.0040f, glow = 0.0016f, outward = fromNeck };
                            for (var i = 0; i < 32; i++) band.guide.Add(Neck(f, 0.5f, i * Mathf.PI * 2f / 32f, 0.075f));
                            o.Add(band);
                        }
                        break;
                    }
                case Kind.ForearmPlate:
                    {
                        // 手首から肘まで、前腕の外（手の甲の側）の銀の板（半幅 1.8 cm）に、三本の光る線と両端の端子
                        var elbow = im.left ? f.elbowL : f.elbowR;
                        var wrist = im.left ? f.wristL : f.wristR;
                        var back = im.left ? f.backL : f.backR;
                        var t0 = im.bold ? 0.10f : 0.30f;
                        var t1 = im.bold ? 0.90f : 0.78f;
                        o.Add(Along(im.color, elbow, wrist, back, 0f, t0, t1, im.bold ? 0.018f : 0.012f, 0f, 0f));
                        foreach (var ang in im.bold ? new[] { -26f, 0f, 26f } : new[] { 0f })
                            o.Add(Along(im.color, elbow, wrist, back, ang, t0 + 0.04f, t1 - 0.04f, 0.0055f, 0.0042f, 0.0080f));
                        break;
                    }
                case Kind.HandGlow:
                    {
                        // 甲の真ん中の端子（半径 1.1 cm）から、指の付け根四つと手首へ線。
                        // 甲は狭いので、にじみを顔の線より細くする（太いと線が溶け合って甲がまるごと光る）
                        var wrist = im.left ? f.wristL : f.wristR;
                        var knuckles = im.left ? f.knucklesL : f.knucklesR;
                        var back = (im.left ? f.backL : f.backR).normalized;
                        Func<Vector3, Vector3> fromBack = g => back;
                        var mid = Vector3.Lerp(wrist, knuckles[1], 0.45f);
                        o.Add(new Stroke { color = im.color, silver = im.bold ? 0.011f : 0.006f, nodeRing = im.bold ? 0.0068f : 0.004f, outward = fromBack, guide = { mid } }.Port());
                        if (im.bold)
                        {
                            foreach (var k in knuckles)
                                o.Add(Line(im.color, 0.0045f, 0.0026f, 0.0058f, fromBack, mid, k));
                            o.Add(Line(im.color, 0.0045f, 0.0026f, 0.0058f, fromBack, mid, wrist + (knuckles[1] - wrist) * 0.02f));
                        }
                        break;
                    }
            }
            return o;
        }

        static Stroke Line(Color color, float silver, float glow, float node, Func<Vector3, Vector3> outward, params Vector3[] points)
        {
            var s = new Stroke { color = color, silver = silver, glow = glow, node = node, outward = outward };
            s.guide.AddRange(points);
            return s;
        }

        /// <summary>首の筒の上の点。t は首の骨から頭の骨へ進んだ比（0〜1）、a はうなじから本人の右へ回る角（ラジアン）、r は軸からの離れの見込み</summary>
        static Vector3 Neck(Frame f, float t, float a, float r)
        {
            var axis = (f.head - f.neck).normalized;
            var back = Vector3.ProjectOnPlane(Vector3.back, axis).normalized;
            var right = Vector3.Cross(axis, back).normalized;
            // うなじ（back）から本人の右（+x）へ回る
            if (Vector3.Dot(right, Vector3.right) < 0f) right = -right;
            var dir = back * Mathf.Cos(a) + right * Mathf.Sin(a);
            return Vector3.Lerp(f.neck, f.head, t) + dir * r;
        }

        /// <summary>骨の間（a から b）を、ref の向きから ang 度回った筋に沿って t0 から t1 までの線（その向きの外から見て腕に落とす）</summary>
        static Stroke Along(Color color, Vector3 a, Vector3 b, Vector3 refDir, float ang, float t0, float t1, float silver, float glow, float node)
        {
            var axis = (b - a).normalized;
            var dir = Quaternion.AngleAxis(ang, axis) * Vector3.ProjectOnPlane(refDir, axis).normalized;
            var s = new Stroke { color = color, silver = silver, glow = glow, outward = g => dir };
            for (var i = 0; i <= 8; i++) s.guide.Add(Vector3.Lerp(a, b, Mathf.Lerp(t0, t1, i / 8f)));
            if (node > 0f)
            {
                s.node = node;
                s.nodeOnlyEnds = true;
            }
            return s;
        }

        static Stroke Port(this Stroke s)
        {
            s.isPort = true;
            return s;
        }

        static Stroke Nodes(this Stroke s)
        {
            s.nodesOnly = true;
            return s;
        }

        // ---- 肌の色 --------------------------------------------------------------

        /// <summary>頭のテクスチャの頬のまわり（頬の骨から 1.5 cm 以内）の色の平均。肌の見本にする</summary>
        public static Color SkinOf(RocketboxPaint.Surface head, Color[] px, Frame f)
        {
            var sum = Vector3.zero;
            var n = 0;
            for (var k = 0; k < px.Length; k++)
            {
                if (!head.On[k]) continue;
                var p = head.P[k];
                if ((p - f.cheekL).sqrMagnitude > 0.015f * 0.015f && (p - f.cheekR).sqrMagnitude > 0.015f * 0.015f) continue;
                var c = px[k].linear;
                sum += new Vector3(c.r, c.g, c.b);
                n++;
            }
            if (n == 0) throw new InvalidOperationException("頬の画素が見つからない");
            sum /= n;
            return new Color(sum.x, sum.y, sum.z).gamma;
        }

        /// <summary>
        /// 画素が肌らしいか（0〜1）。色み（明るさを 1 に揃えた色）が見本に近く、明るさが見本の 0.36〜2 倍の所。
        /// 髪・眉・服の画素に描かないため
        /// </summary>
        public static float Skinness(Color c, Color skin)
        {
            var a = c.linear;
            var s = skin.linear;
            var la = (a.r + a.g + a.b) / 3f;
            var ls = (s.r + s.g + s.b) / 3f;
            if (la < 1e-4f) return 0f;
            var d = Mathf.Abs(a.r / la - s.r / ls) + Mathf.Abs(a.g / la - s.g / ls) + Mathf.Abs(a.b / la - s.b / ls);
            var ratio = la / ls;
            var tone = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.45f, d));
            // 目のくぼみや顎の下の陰（見本の頬より暗い肌）も肌に入れる。髪と眉は色みで外れる
            var light = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.36f, ratio)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.0f, 2.6f, ratio)));
            return tone * light;
        }

        // ---- 吸い付けと塗り -----------------------------------------------------------

        /// <summary>
        /// 模型の面の画素の位置（頭と体の二枚）。線の案内の点をここへ落とす（塗るのは肌の画素だけ）。
        /// 点は 1 cm の升目に分けて持つ（群衆の数十人ぶんを落とすので、全部の点を毎回なめない）
        /// </summary>
        public sealed class Skin
        {
            const float Cell = 0.01f;
            readonly Dictionary<Vector3Int, List<Vector3>> grid = new Dictionary<Vector3Int, List<Vector3>>();
            readonly List<Vector3> points = new List<Vector3>();

            static Vector3Int Key(Vector3 p)
            {
                return new Vector3Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell), Mathf.FloorToInt(p.z / Cell));
            }

            public void Add(RocketboxPaint.Surface s)
            {
                for (var k = 0; k < s.P.Length; k++)
                {
                    if (!s.On[k]) continue;
                    var q = s.P[k];
                    points.Add(q);
                    List<Vector3> cell;
                    var key = Key(q);
                    if (!grid.TryGetValue(key, out cell)) grid[key] = cell = new List<Vector3>();
                    cell.Add(q);
                }
            }

            /// <summary>いちばん近い面の点。まわりの升目から広げて探し、10 cm で見つからなければ全部をなめる</summary>
            public Vector3 Nearest(Vector3 p)
            {
                var c = Key(p);
                var best = p;
                var bd = float.MaxValue;
                for (var r = 0; r <= 10; r++)
                {
                    // 升目 r の殻だけを見る（内側は見終えている）
                    for (var x = -r; x <= r; x++)
                        for (var y = -r; y <= r; y++)
                            for (var z = -r; z <= r; z++)
                            {
                                if (Mathf.Max(Mathf.Abs(x), Mathf.Max(Mathf.Abs(y), Mathf.Abs(z))) != r) continue;
                                List<Vector3> cell;
                                if (!grid.TryGetValue(new Vector3Int(c.x + x, c.y + y, c.z + z), out cell)) continue;
                                foreach (var q in cell)
                                {
                                    var d = (q - p).sqrMagnitude;
                                    if (d < bd) { bd = d; best = q; }
                                }
                            }
                    // 殻 r まで見れば、r 升ぶんより近い点は見落とさない
                    if (bd <= r * Cell * r * Cell) return best;
                }
                if (bd < float.MaxValue) return best;
                foreach (var q in points)
                {
                    var d = (q - p).sqrMagnitude;
                    if (d < bd) { bd = d; best = q; }
                }
                return best;
            }

            /// <summary>
            /// out の向きの外（15 cm）から体へ向けて、案内の点を通る筋を見て、筋から 3 mm 以内でいちばん外にある面の点。
            /// 見つからなければいちばん近い点
            /// </summary>
            public Vector3 Drop(Vector3 p, Vector3 dir)
            {
                dir = dir.normalized;
                var best = Vector3.zero;
                var bestT = float.MinValue;
                const float r2 = 0.003f * 0.003f;
                var seen = new HashSet<Vector3Int>();
                // 筋に沿って 5 mm ごとに、まわりの升目を見る
                for (var t0 = -0.15f; t0 <= 0.15f + 1e-4f; t0 += 0.005f)
                {
                    var c = Key(p + dir * t0);
                    for (var x = -1; x <= 1; x++)
                        for (var y = -1; y <= 1; y++)
                            for (var z = -1; z <= 1; z++)
                            {
                                var key = new Vector3Int(c.x + x, c.y + y, c.z + z);
                                if (!seen.Add(key)) continue;
                                List<Vector3> cell;
                                if (!grid.TryGetValue(key, out cell)) continue;
                                foreach (var q in cell)
                                {
                                    var d = q - p;
                                    var t = Vector3.Dot(d, dir);
                                    if (t > 0.15f || t < -0.15f) continue;
                                    if ((d - dir * t).sqrMagnitude > r2) continue;
                                    if (t > bestT) { bestT = t; best = q; }
                                }
                            }
                }
                return bestT > float.MinValue ? best : Nearest(p);
            }
        }

        /// <summary>案内の点を 4 mm ごとに刻んで肌へ吸い付け、線の点の並びを作る</summary>
        public static void Prepare(List<Stroke> strokes, Skin skin)
        {
            foreach (var s in strokes)
            {
                var st = s;
                Func<Vector3, Vector3> snap = g => st.outward != null ? skin.Drop(g, st.outward(g)) : skin.Nearest(g);
                var path = new List<Vector3>();
                var count = s.guide.Count;
                var segs = s.closed ? count : count - 1;
                if (count == 1) path.Add(snap(s.guide[0]));
                for (var i = 0; i < segs; i++)
                {
                    var a = s.guide[i];
                    var b = s.guide[(i + 1) % count];
                    var steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 0.004f));
                    for (var k = 0; k < steps; k++) path.Add(snap(Vector3.Lerp(a, b, k / (float)steps)));
                    if (!s.closed && i == segs - 1) path.Add(snap(b));
                }
                s.path = path.ToArray();
                var nodes = new List<Vector3>();
                if (s.node > 0f || s.isPort)
                {
                    if (s.nodeOnlyEnds && count > 1) { nodes.Add(snap(s.guide[0])); nodes.Add(snap(s.guide[count - 1])); }
                    else foreach (var g in s.guide) nodes.Add(snap(g));
                }
                s.nodes = nodes.ToArray();
                var b0 = new Bounds(s.path[0], Vector3.zero);
                foreach (var p in s.path) b0.Encapsulate(p);
                foreach (var p in s.nodes) b0.Encapsulate(p);
                b0.Expand(2f * Mathf.Max(Mathf.Max(s.silver, s.node), Mathf.Max(s.glow * Halo, 0.004f)) + 0.01f);
                s.bounds = b0;
            }
        }

        /// <summary>一つの画素の塗り: 銀の地・縁・穴（0〜1）と、ネオン管の芯と、にじみ（色つきの光）</summary>
        struct Cover
        {
            public float silver, edge, socket;
            public Color core, halo;

            public void Max(float si, float ed, float so)
            {
                silver = Mathf.Max(silver, si);
                edge = Mathf.Max(edge, ed);
                socket = Mathf.Max(socket, so);
            }

            /// <summary>芯（覆い c）とにじみ（覆い h）を、色 col で重ねる。明るい方を残す</summary>
            public void Light(float c, float h, Color col)
            {
                var coreCol = Color.Lerp(col, Color.white, CoreWhite) * c;
                if (coreCol.maxColorComponent > core.maxColorComponent) core = coreCol;
                var haloCol = col * h;
                if (haloCol.maxColorComponent > halo.maxColorComponent) halo = haloCol;
            }
        }

        /// <summary>中心からの距離 d のにじみ（0〜1）。芯の半幅 half の Halo 倍まで、なだらかに消える</summary>
        static float Glow(float d, float half)
        {
            var x = Mathf.Clamp01(d / (half * Halo));
            return Mathf.Pow(1f - x, 1.4f);
        }

        /// <summary>模型の上の位置 p（画素の幅 ts）での、線の組の覆い</summary>
        static Cover CoverAt(List<Stroke> strokes, Vector3 p, float ts)
        {
            var c = new Cover();
            foreach (var st in strokes)
            {
                if (!st.bounds.Contains(p)) continue;
                var d = st.isPort || st.nodesOnly ? float.MaxValue : Distance(st.path, st.closed, p);
                var dn = float.MaxValue;
                foreach (var q in st.nodes) dn = Mathf.Min(dn, (q - p).magnitude);
                if (st.isPort)
                {
                    // 丸い座金、縁の暗い線、奥の穴、光る輪（穴の無い差込口は、真ん中が光る点）
                    c.Max(In(dn - st.silver, ts), Line(Mathf.Abs(dn - st.silver * 0.93f), 0.0007f, ts), st.socket > 0f ? In(dn - st.socket, ts) : 0f);
                    var ring = Mathf.Abs(dn - st.nodeRing);
                    c.Light(Line(ring, 0.0015f, ts), Glow(ring, 0.0026f), st.color);
                    if (st.socket <= 0f) c.Light(In(dn - st.nodeRing * 0.35f, ts), Glow(dn, st.nodeRing * 0.35f), st.color);
                    continue;
                }
                c.Max(st.silver > 0f ? In(d - st.silver, ts) : 0f, st.silver > 0.005f ? Line(Mathf.Abs(d - st.silver * 0.92f), 0.0007f, ts) : 0f, 0f);
                if (st.glow > 0f) c.Light(Line(d, st.glow * 0.6f, ts), Glow(d, st.glow), st.color);
                if (st.node > 0f)
                {
                    c.Max(In(dn - st.node, ts), 0f, 0f);
                    c.Light(In(dn - st.node * 0.45f, ts), Glow(dn, st.node * 0.5f), st.color);
                }
            }
            return c;
        }

        // ---- 肌の出ている所 -----------------------------------------------------------

        /// <summary>画素の外向きへ伸ばして覆いを探す筋の長さ（m）。ポニーテールの塊は、うなじの肌から 5〜15 cm 後ろに垂れている</summary>
        public const float BareReach = 0.10f;
        /// <summary>筋を面から浮かせて始める距離（m）。自分の面に当たらないように</summary>
        const float BareLift = 0.003f;
        /// <summary>
        /// 髪の房の不透明な所からこの距離（m）より近い画素は、覆われているとみなす。
        /// ポニーテールの塊の面は房に包まれているが、房の隙間を抜ける筋では覆いに当たらない所が残る（女大 01 の塊の面は房から 2 cm 以内）
        /// </summary>
        public const float HairNear = 0.02f;

        /// <summary>
        /// 肌を覆う物。頭と体の面（髪の塊・襟・フード）と、髪の房（透けの絵の α が半分より上の所）。
        ///
        /// 明るい茶や赤の髪は色みが肌に近く、肌らしさ（<see cref="Skinness"/>）だけでは見分けられない。
        /// 女大 01 のポニーテールの塊は頭の面の一部（頭のテクスチャで塗られている）で、肌らしさが 0.5〜0.7 あった。
        /// 首の帯とうなじの差込口の線は、外から見て最初に当たる面へ落とすので、その塊の上に載り、髪がネオンの色に光った。
        /// 髪の塊は髪の房に包まれていて、塊の下のうなじは塊に覆われている。
        /// どちらも、画素の面の外向き（法線）へ筋を伸ばせば何かに当たる（<see cref="Covered"/>）
        /// </summary>
        public sealed class Shelter
        {
            const float Cell = 0.02f;
            readonly Vector3[] v;
            readonly Vector2[] uv;
            /// <summary>三角の頂点の番号（3 つずつ）と、髪の房か</summary>
            readonly List<int> tri = new List<int>();
            readonly List<bool> hair = new List<bool>();
            readonly Color32[] alpha;
            readonly int aw, ah;
            readonly Dictionary<Vector3Int, List<int>> grid = new Dictionary<Vector3Int, List<int>>();
            /// <summary>髪の房の不透明な所に散らした点（升目ごと）</summary>
            readonly Dictionary<Vector3Int, List<Vector3>> strands = new Dictionary<Vector3Int, List<Vector3>>();
            int[] stamp;
            int ray;

            /// <summary>髪の房の不透明な所からこの距離（m）より近い画素は、覆われているとみなす。0 なら見ない</summary>
            public float near = HairNear;

            /// <summary>
            /// verts・uv は模型の根の中の頂点（束ねた姿勢）。solid は頭と体の面の三角、hairTris は髪の房の三角、
            /// hairPx（w×h）は透けの絵。eyes のそばの小さい房（まつ毛）は覆いにしない（目のまわりの輪が削れる）
            /// </summary>
            public Shelter(Vector3[] verts, Vector2[] uvs, List<int[]> solid, int[] hairTris, Color32[] hairPx, int w, int h, Vector3[] eyes)
            {
                v = verts;
                uv = uvs;
                alpha = hairPx;
                aw = w;
                ah = h;
                foreach (var s in solid) Add(s, false, null);
                if (hairTris != null && hairPx != null)
                {
                    var lashes = Lashes(hairTris, eyes);
                    Add(hairTris, true, lashes);
                    Sample(hairTris, lashes);
                }
                stamp = new int[tri.Count / 3];
            }

            /// <summary>髪の房の三角に 4 mm おきに点を打ち、透けの絵の不透明な所だけを残す</summary>
            void Sample(int[] tris, HashSet<int> skip)
            {
                const float step = 0.004f;
                for (var t = 0; t < tris.Length; t += 3)
                {
                    if (skip.Contains(t)) continue;
                    int ia = tris[t], ib = tris[t + 1], ic = tris[t + 2];
                    var n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max((v[ib] - v[ia]).magnitude, (v[ic] - v[ia]).magnitude) / step));
                    for (var i = 0; i <= n; i++)
                        for (var j = 0; i + j <= n; j++)
                        {
                            float bu = i / (float)n, bv = j / (float)n;
                            if (!Opaque(ia, ib, ic, bu, bv)) continue;
                            var p = v[ia] * (1f - bu - bv) + v[ib] * bu + v[ic] * bv;
                            List<Vector3> cell;
                            var k = Key(p);
                            if (!strands.TryGetValue(k, out cell)) strands[k] = cell = new List<Vector3>();
                            cell.Add(p);
                        }
                }
            }

            /// <summary>髪の房の三角 (a, b, c) の重心の座標 (bu, bv) が、透けの絵で塗ってある所か</summary>
            bool Opaque(int ia, int ib, int ic, float bu, float bv)
            {
                var st = uv[ia] * (1f - bu - bv) + uv[ib] * bu + uv[ic] * bv;
                var x = Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(st.x, 1f) * aw), 0, aw - 1);
                var y = Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(st.y, 1f) * ah), 0, ah - 1);
                return alpha[y * aw + x].a >= 128;
            }

            /// <summary>p から <see cref="near"/> 以内に髪の房の不透明な所があるか</summary>
            bool NearStrand(Vector3 p)
            {
                if (near <= 0f || strands.Count == 0) return false;
                var c = Key(p);
                var r2 = near * near;
                for (var x = -1; x <= 1; x++)
                    for (var y = -1; y <= 1; y++)
                        for (var z = -1; z <= 1; z++)
                        {
                            List<Vector3> cell;
                            if (!strands.TryGetValue(new Vector3Int(c.x + x, c.y + y, c.z + z), out cell)) continue;
                            foreach (var q in cell) if ((q - p).sqrMagnitude < r2) return true;
                        }
                return false;
            }

            void Add(int[] tris, bool isHair, HashSet<int> skip)
            {
                for (var t = 0; t < tris.Length; t += 3)
                {
                    if (skip != null && skip.Contains(t)) continue;
                    var id = tri.Count / 3;
                    tri.Add(tris[t]);
                    tri.Add(tris[t + 1]);
                    tri.Add(tris[t + 2]);
                    hair.Add(isHair);
                    Vector3 a = v[tris[t]], b = v[tris[t + 1]], c = v[tris[t + 2]];
                    var lo = Key(Vector3.Min(a, Vector3.Min(b, c)));
                    var hi = Key(Vector3.Max(a, Vector3.Max(b, c)));
                    for (var x = lo.x; x <= hi.x; x++)
                        for (var y = lo.y; y <= hi.y; y++)
                            for (var z = lo.z; z <= hi.z; z++)
                            {
                                List<int> cell;
                                var k = new Vector3Int(x, y, z);
                                if (!grid.TryGetValue(k, out cell)) grid[k] = cell = new List<int>();
                                cell.Add(id);
                            }
                }
            }

            /// <summary>
            /// まつ毛の房の三角（先頭の番号）。房の塊（頂点で繋がった三角）のうち、差し渡し 5 cm に満たず、目から 3 cm 以内にある物。
            /// 前髪のような大きな房は残す
            /// </summary>
            HashSet<int> Lashes(int[] tris, Vector3[] eyes)
            {
                var parent = new Dictionary<int, int>();
                System.Func<int, int> find = null;
                find = x =>
                {
                    int p;
                    if (!parent.TryGetValue(x, out p)) { parent[x] = x; return x; }
                    if (p == x) return x;
                    var r = find(p);
                    parent[x] = r;
                    return r;
                };
                for (var t = 0; t < tris.Length; t += 3)
                {
                    int a = find(tris[t]), b = find(tris[t + 1]), c = find(tris[t + 2]);
                    parent[b] = a;
                    parent[find(c)] = a;
                }
                var bounds = new Dictionary<int, Bounds>();
                for (var t = 0; t < tris.Length; t += 3)
                {
                    var r = find(tris[t]);
                    Bounds bb;
                    if (!bounds.TryGetValue(r, out bb)) bb = new Bounds(v[tris[t]], Vector3.zero);
                    bb.Encapsulate(v[tris[t]]);
                    bb.Encapsulate(v[tris[t + 1]]);
                    bb.Encapsulate(v[tris[t + 2]]);
                    bounds[r] = bb;
                }
                var skip = new HashSet<int>();
                for (var t = 0; t < tris.Length; t += 3)
                {
                    var bb = bounds[find(tris[t])];
                    if (bb.size.magnitude >= 0.05f) continue;
                    foreach (var e in eyes)
                        if ((bb.center - e).magnitude < 0.03f) { skip.Add(t); break; }
                }
                return skip;
            }

            static Vector3Int Key(Vector3 p)
            {
                return new Vector3Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell), Mathf.FloorToInt(p.z / Cell));
            }

            /// <summary>
            /// p が覆われているか。髪の房の不透明な所のすぐそば（<see cref="near"/> 以内）か、
            /// 面から外向き n へ筋を伸ばして <see cref="BareReach"/> までに覆いに当たるか
            /// </summary>
            public bool Covered(Vector3 p, Vector3 n)
            {
                if (NearStrand(p)) return true;
                n = n.normalized;
                if (n == Vector3.zero) return false;
                var o = p + n * BareLift;
                ray++;
                for (var s = 0f; s <= BareReach + Cell * 0.5f; s += Cell * 0.5f)
                {
                    List<int> cell;
                    if (!grid.TryGetValue(Key(o + n * s), out cell)) continue;
                    foreach (var id in cell)
                    {
                        if (stamp[id] == ray) continue;
                        stamp[id] = ray;
                        if (Hit(id, o, n)) return true;
                    }
                }
                return false;
            }

            bool Hit(int id, Vector3 o, Vector3 d)
            {
                int ia = tri[id * 3], ib = tri[id * 3 + 1], ic = tri[id * 3 + 2];
                Vector3 a = v[ia], e1 = v[ib] - a, e2 = v[ic] - a;
                var q = Vector3.Cross(d, e2);
                var det = Vector3.Dot(e1, q);
                if (Mathf.Abs(det) < 1e-12f) return false;
                var inv = 1f / det;
                var sv = o - a;
                var bu = Vector3.Dot(sv, q) * inv;
                if (bu < 0f || bu > 1f) return false;
                var r = Vector3.Cross(sv, e1);
                var bv = Vector3.Dot(d, r) * inv;
                if (bv < 0f || bu + bv > 1f) return false;
                var t = Vector3.Dot(e2, r) * inv;
                if (t < 0f || t > BareReach) return false;
                // 髪の房は、透けの絵で塗ってある所だけが覆う
                return !hair[id] || Opaque(ia, ib, ic, bu, bv);
            }
        }

        /// <summary>
        /// 面の画素ごとの、肌が出ているか。pos は画素の位置、nrm は同じ並びの法線（<see cref="RocketboxPaint.Surface.Of"/> へ頂点の代わりに法線を渡した物）。
        /// 肌らしさが 0 の画素はどうせ塗らないので、見ずに false にする
        /// </summary>
        public static bool[] Bare(RocketboxPaint.Surface pos, RocketboxPaint.Surface nrm, Shelter shelter, Color[] skinPx, Color skin)
        {
            var o = new bool[pos.P.Length];
            for (var k = 0; k < o.Length; k++)
            {
                if (!pos.On[k] || Skinness(skinPx[k], skin) <= 0f) continue;
                o[k] = !shelter.Covered(pos.P[k], nrm.P[k]);
            }
            return o;
        }

        /// <summary>
        /// 一枚のテクスチャ（頭か体）へ線の組を塗る。px は塗る地の色で、書き換える。skinPx は肌の見分けに使う元の色。
        /// bare は肌の出ている画素（<see cref="Bare"/>）。覆われた画素には塗らない。null なら色だけで見分ける。
        /// 戻り値は自己発光のテクスチャ（ネオン管のように、芯は白っぽく明るく、まわりへ色がにじむ。地は黒）
        /// </summary>
        public static Color[] Paint(List<Stroke> strokes, RocketboxPaint.Surface s, Color[] px, Color[] skinPx, Color skin, bool[] bare, out int painted)
        {
            var glow = new Color[px.Length];
            for (var i = 0; i < glow.Length; i++) glow[i] = Color.black;
            painted = 0;
            if (strokes.Count == 0) return glow;
            var size = TexelSize(s);
            for (var k = 0; k < px.Length; k++)
            {
                if (!s.On[k]) continue;
                var c = CoverAt(strokes, s.P[k], Mathf.Max(size[k], 0.0006f));
                var lit = Mathf.Max(c.core.maxColorComponent, c.halo.maxColorComponent);
                if (c.silver <= 0f && c.socket <= 0f && c.edge <= 0f && lit <= 0.002f) continue;
                if (bare != null && !bare[k]) continue;
                var w = Skinness(skinPx[k], skin);
                if (w <= 0f) continue;
                var col = px[k];
                col = Color.Lerp(col, Silver, c.silver * w);
                col = Color.Lerp(col, SilverEdge, c.edge * w);
                col = Color.Lerp(col, Socket, c.socket * w);
                // 地の色も光の色へ寄せる（ブルームが無くても線が読めるように）。にじみは弱く、芯は強く
                var haloW = c.halo.maxColorComponent;
                if (haloW > 0f) col = Color.Lerp(col, c.halo / haloW, haloW * w * 0.5f);
                var coreW = c.core.maxColorComponent;
                if (coreW > 0f) col = Color.Lerp(col, c.core / coreW, coreW * w * 0.85f);
                col.a = px[k].a;
                px[k] = col;
                var e = new Color(Mathf.Max(c.core.r, c.halo.r), Mathf.Max(c.core.g, c.halo.g), Mathf.Max(c.core.b, c.halo.b)) * w;
                e.a = 1f;
                glow[k] = e;
                painted++;
            }
            return glow;
        }

        /// <summary>
        /// 線の組のうち、肌の上に載る分を onSkin と total へ加える（onSkin は肌らしさで重みを付けた覆い、total は覆いの全部）。
        /// 襟や袖や髪に隠れる所は塗らないので、肌に載る比が小さい形はその人には選ばない。
        /// bare は肌の出ている画素（<see cref="Bare"/>）。覆われた画素は肌に数えない。null なら色だけで見分ける
        /// </summary>
        public static void Coverage(List<Stroke> strokes, RocketboxPaint.Surface s, Color[] skinPx, Color skin, bool[] bare, ref float onSkin, ref float total)
        {
            if (strokes.Count == 0) return;
            var bounds = strokes[0].bounds;
            foreach (var st in strokes) bounds.Encapsulate(st.bounds);
            for (var k = 0; k < s.P.Length; k++)
            {
                if (!s.On[k] || !bounds.Contains(s.P[k])) continue;
                var c = CoverAt(strokes, s.P[k], 0.002f);
                var a = Mathf.Max(c.silver, c.core.maxColorComponent > 0f ? 1f : 0f);
                if (a <= 0f) continue;
                total += a;
                if (bare != null && !bare[k]) continue;
                onSkin += a * Skinness(skinPx[k], skin);
            }
        }

        static float Distance(Vector3[] path, bool closed, Vector3 p)
        {
            var best = float.MaxValue;
            var n = path.Length;
            if (n == 1) return (path[0] - p).magnitude;
            var segs = closed ? n : n - 1;
            for (var i = 0; i < segs; i++)
            {
                var a = path[i];
                var b = path[(i + 1) % n];
                var ab = b - a;
                var t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-10f));
                best = Mathf.Min(best, (p - (a + ab * t)).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>画素一つが模型の上で占める幅（m）。となりの画素との距離（UV の継ぎ目をまたぐ所は除く）</summary>
        static float[] TexelSize(RocketboxPaint.Surface s)
        {
            var n = s.N;
            var o = new float[n * n];
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var k = y * n + x;
                    if (!s.On[k]) continue;
                    var sum = 0f;
                    var cnt = 0;
                    if (x + 1 < n && s.On[k + 1])
                    {
                        var d = (s.P[k + 1] - s.P[k]).magnitude;
                        if (d < 0.02f) { sum += d; cnt++; }
                    }
                    if (y + 1 < n && s.On[k + n])
                    {
                        var d = (s.P[k + n] - s.P[k]).magnitude;
                        if (d < 0.02f) { sum += d; cnt++; }
                    }
                    o[k] = cnt > 0 ? sum / cnt : 0.002f;
                }
            return o;
        }

        /// <summary>符号付きの距離 d（内側が負）から、画素の覆い（0〜1）</summary>
        static float In(float d, float ts)
        {
            return Mathf.Clamp01(0.5f - d / ts);
        }

        /// <summary>細い線（中心線からの距離 d、半幅 half）。画素より細い線は、画素一つ分の幅で薄く描く</summary>
        static float Line(float d, float half, float ts)
        {
            var h = Mathf.Max(half, ts * 0.5f);
            return In(d - h, ts) * Mathf.Min(1f, half / h);
        }
    }
}
