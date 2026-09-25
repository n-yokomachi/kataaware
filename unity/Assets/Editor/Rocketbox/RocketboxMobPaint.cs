using System;
using UnityEngine;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 路地裏の人の肌に描くインプラント（設計メモ 5 節）と、服の色を暗く・彩度を落とす塗り替え。
    ///
    /// インプラントは銀の地に、細い線と端子だけがうっすら光る（ジャックの帯と同じ系統の色）。光は自己発光のテクスチャで持たせ、灯りは置かない。
    /// 形は模型の束ねた姿勢の上で決め（骨と顔の骨からの位置、m）、テクスチャの画素ごとに、その画素が載る模型の上の位置（<see cref="RocketboxPaint.Surface"/>）で塗る。
    /// 肌の色から離れた画素（髪・眉・襟・袖）には塗らない
    /// </summary>
    public static class RocketboxMobPaint
    {
        /// <summary>インプラントの形。その人の肌の出ている所に合わせて選ぶ</summary>
        public enum Kind
        {
            /// <summary>うなじの差込口</summary>
            NapePort,
            /// <summary>こめかみの線</summary>
            TempleLines,
            /// <summary>前腕の板</summary>
            ForearmPlate,
            /// <summary>手の甲の光</summary>
            HandGlow,
            /// <summary>首を巻く輪</summary>
            NeckRing,
            /// <summary>目のまわりの輪</summary>
            EyeRing,
        }

        /// <summary>一つのインプラント。left は本人の左（左右のあるものだけ）</summary>
        public struct Implant
        {
            public Kind kind;
            public bool left;

            public Implant(Kind kind, bool left)
            {
                this.kind = kind;
                this.left = left;
            }

            public override string ToString()
            {
                var side = kind == Kind.NapePort || kind == Kind.NeckRing ? "" : left ? "（左）" : "（右）";
                switch (kind)
                {
                    case Kind.NapePort: return "うなじの差込口";
                    case Kind.TempleLines: return "こめかみの線" + side;
                    case Kind.ForearmPlate: return "前腕の板" + side;
                    case Kind.HandGlow: return "手の甲の光" + side;
                    case Kind.NeckRing: return "首を巻く輪";
                    default: return "目のまわりの輪" + side;
                }
            }
        }

        /// <summary>光の色。ジャックの帯（BuildProps の JackLight）と同じ</summary>
        public static readonly Color GlowColor = new Color(0.36f, 0.82f, 0.86f);
        /// <summary>銀の地（sRGB）と、縁の暗い線</summary>
        static readonly Color Silver = new Color(0.60f, 0.63f, 0.67f), SilverEdge = new Color(0.30f, 0.32f, 0.35f);
        /// <summary>差込口の穴</summary>
        static readonly Color Socket = new Color(0.05f, 0.05f, 0.06f);

        /// <summary>インプラントを決める骨の位置と向き（模型の根の中、束ねた姿勢）。模型は +z を向き、本人の右が +x</summary>
        public sealed class Frame
        {
            public Vector3 neck, head, eyeL, eyeR, cheekL, cheekR;
            public Vector3 elbowL, elbowR, wristL, wristR, knuckleL, knuckleR;
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
                    knuckleL = B(HumanBodyBones.LeftMiddleProximal), knuckleR = B(HumanBodyBones.RightMiddleProximal),
                    backL = -root.InverseTransformDirection(BodyPoser.PalmDir(an, true)),
                    backR = -root.InverseTransformDirection(BodyPoser.PalmDir(an, false)),
                };
            }
        }

        /// <summary>一つの画素の塗り: 銀の地・縁・穴・光（どれも 0〜1）</summary>
        struct Cover
        {
            public float silver, edge, socket, glow;

            public void Max(Cover o)
            {
                silver = Mathf.Max(silver, o.silver);
                edge = Mathf.Max(edge, o.edge);
                socket = Mathf.Max(socket, o.socket);
                glow = Mathf.Max(glow, o.glow);
            }
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
        /// 画素が肌らしいか（0〜1）。色み（明るさで割った色）が見本に近く、明るさが見本の 0.45〜2 倍の所。
        /// 髪・眉・服の画素に描かないため
        /// </summary>
        static float Skinness(Color c, Color skin)
        {
            var a = c.linear;
            var s = skin.linear;
            var la = (a.r + a.g + a.b) / 3f;
            var ls = (s.r + s.g + s.b) / 3f;
            if (la < 1e-4f) return 0f;
            var d = Mathf.Abs(a.r / la - s.r / ls) + Mathf.Abs(a.g / la - s.g / ls) + Mathf.Abs(a.b / la - s.b / ls);
            var ratio = la / ls;
            var tone = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.45f, d));
            var light = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.45f, ratio)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.0f, 2.6f, ratio)));
            return tone * light;
        }

        // ---- 塗る -------------------------------------------------------------------

        /// <summary>
        /// 一枚のテクスチャ（頭か体）へインプラントを塗る。px は塗る地の色（塗り替えた後でもよい）で、書き換える。
        /// skinPx は肌の見分けに使う元の色。戻り値は自己発光のテクスチャ（光の色 × 強さ、地は黒）
        /// </summary>
        public static Color[] Paint(RocketboxMob who, Frame f, RocketboxPaint.Surface s, Color[] px, Color[] skinPx, Color skin, out int painted)
        {
            var n = s.N;
            var glow = new Color[px.Length];
            for (var i = 0; i < glow.Length; i++) glow[i] = Color.black;
            painted = 0;
            if (who.Implants == null || who.Implants.Length == 0) return glow;
            var size = TexelSize(s);
            for (var k = 0; k < px.Length; k++)
            {
                if (!s.On[k]) continue;
                var p = s.P[k];
                var ts = Mathf.Max(size[k], 0.0006f);
                var c = new Cover();
                foreach (var im in who.Implants) c.Max(Shape(im, f, p, ts));
                if (c.silver <= 0f && c.glow <= 0f && c.socket <= 0f && c.edge <= 0f) continue;
                var w = Skinness(skinPx[k], skin);
                if (w <= 0f) continue;
                var col = px[k];
                col = Color.Lerp(col, Silver, c.silver * w);
                col = Color.Lerp(col, SilverEdge, c.edge * w);
                col = Color.Lerp(col, Socket, c.socket * w);
                // 光る所は地の色も明るい光の色へ寄せる（ブルームが無くても線が読めるように）
                col = Color.Lerp(col, Color.Lerp(GlowColor, Color.white, 0.25f), c.glow * w * 0.7f);
                col.a = px[k].a;
                px[k] = col;
                var g = GlowColor * (c.glow * w);
                g.a = 1f;
                glow[k] = g;
                painted++;
            }
            return glow;
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

        /// <summary>
        /// 骨の間の筒の上の位置。t は a から b へ進んだ比（0〜1）、s は ref の向きからの周の長さ（m、軸のまわりに右手で正）、r は軸からの離れ
        /// </summary>
        static void Cyl(Vector3 a, Vector3 b, Vector3 refDir, Vector3 p, out float t, out float s, out float r, out float along)
        {
            var axis = b - a;
            var len = axis.magnitude;
            axis /= len;
            var d = p - a;
            along = Vector3.Dot(d, axis);
            t = along / len;
            var radial = d - axis * along;
            r = radial.magnitude;
            var refp = (refDir - axis * Vector3.Dot(refDir, axis)).normalized;
            var ang = Vector3.SignedAngle(refp, radial, axis) * Mathf.Deg2Rad;
            s = ang * r;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>角を丸めた長方形（中心 0、半幅 hx・hy、角の半径 rc）までの符号付きの距離</summary>
        static float RoundRect(Vector2 p, float hx, float hy, float rc)
        {
            var q = new Vector2(Mathf.Abs(p.x) - (hx - rc), Mathf.Abs(p.y) - (hy - rc));
            var outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - rc;
        }

        static Cover Shape(Implant im, Frame f, Vector3 p, float ts)
        {
            var c = new Cover();
            switch (im.kind)
            {
                case Kind.NapePort:
                    {
                        // 首の筒の後ろ。首の骨から頭の骨までの 6 割の高さ
                        float t, s, r, along;
                        Cyl(f.neck, f.head, Vector3.back, p, out t, out s, out r, out along);
                        if (t < 0.2f || t > 1.0f || Mathf.Abs(s) > 0.03f) break;
                        var len = (f.head - f.neck).magnitude;
                        var q = new Vector2(s, along - 0.6f * len);
                        var d = q.magnitude;
                        // 丸い座金 8 mm、縁の暗い線、光る輪（4.2〜5.4 mm）、奥の穴 3 mm、左右に小さな端子
                        c.silver = In(d - 0.008f, ts);
                        c.edge = Line(Mathf.Abs(d - 0.0075f), 0.0006f, ts);
                        c.glow = Line(Mathf.Abs(d - 0.0048f), 0.0006f, ts);
                        c.socket = In(d - 0.003f, ts);
                        var pin = Mathf.Min((q - new Vector2(0.0125f, 0f)).magnitude, (q - new Vector2(-0.0125f, 0f)).magnitude);
                        c.silver = Mathf.Max(c.silver, In(pin - 0.0028f, ts));
                        c.glow = Mathf.Max(c.glow, In(pin - 0.0012f, ts));
                        break;
                    }
                case Kind.NeckRing:
                    {
                        // 首を一周する輪。首の骨から頭の骨までの 4 割 5 分の高さ。幅 7 mm の銀に、光る線と 1.8 cm ごとの端子
                        float t, s, r, along;
                        Cyl(f.neck, f.head, Vector3.back, p, out t, out s, out r, out along);
                        if (t < 0.1f || t > 0.9f || r > 0.09f) break;
                        var len = (f.head - f.neck).magnitude;
                        var h = along - 0.45f * len;
                        c.silver = In(Mathf.Abs(h) - 0.0035f, ts);
                        c.edge = Line(Mathf.Abs(Mathf.Abs(h) - 0.0032f), 0.0005f, ts);
                        c.glow = Line(Mathf.Abs(h), 0.0006f, ts);
                        var arc = s - Mathf.Round(s / 0.018f) * 0.018f;
                        c.glow = Mathf.Max(c.glow, In(new Vector2(arc, h).magnitude - 0.0016f, ts));
                        break;
                    }
                case Kind.TempleLines:
                    {
                        // 目の横から耳の上へ、二本の細い線。目の骨より外の頭の横の面だけ
                        var eye = im.left ? f.eyeL : f.eyeR;
                        var side = im.left ? -1f : 1f;
                        if ((p.x - eye.x) * side < 0.022f || Mathf.Abs(p.y - eye.y) > 0.05f) break;
                        var q = new Vector2(eye.z - p.z, p.y - eye.y);
                        if (q.x < 0.01f || q.x > 0.095f) break;
                        // もみあげと髪の生え際を避け、目の高さのすぐ上を耳の前まで
                        var a1 = new Vector2(0.022f, 0.006f);
                        var b1 = new Vector2(0.066f, 0.012f);
                        var a2 = new Vector2(0.028f, -0.002f);
                        var b2 = new Vector2(0.058f, 0.002f);
                        var d1 = Segment(q, a1, b1);
                        var d2 = Segment(q, a2, b2);
                        var d = Mathf.Min(d1, d2);
                        c.silver = In(d - 0.0018f, ts);
                        c.glow = Line(d, 0.0006f, ts);
                        var node = Mathf.Min(Mathf.Min((q - a1).magnitude, (q - b1).magnitude), Mathf.Min((q - a2).magnitude, (q - b2).magnitude));
                        c.silver = Mathf.Max(c.silver, In(node - 0.0028f, ts));
                        c.glow = Mathf.Max(c.glow, In(node - 0.0014f, ts));
                        break;
                    }
                case Kind.EyeRing:
                    {
                        // 目の下から外を回って斜め上へ抜ける弧（眉にはかけない）と、弧の上の三つの端子。顔の前の面だけ
                        var eye = im.left ? f.eyeL : f.eyeR;
                        var side = im.left ? -1f : 1f;
                        if (p.z < eye.z - 0.025f) break;
                        var q = new Vector2((p.x - eye.x) * side, p.y - eye.y);
                        var rr = q.magnitude;
                        if (rr > 0.035f) break;
                        // 角度は外（こめかみの側）を 0、上を正
                        var ang = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg;
                        var inArc = ang > -150f && ang < 40f;
                        const float R = 0.021f;
                        if (inArc)
                        {
                            c.silver = In(Mathf.Abs(rr - R) - 0.0016f, ts);
                            c.edge = Line(Mathf.Abs(Mathf.Abs(rr - R) - 0.0015f), 0.0004f, ts);
                            c.glow = Line(Mathf.Abs(rr - R), 0.0006f, ts);
                        }
                        foreach (var a in new[] { -110f, -35f, 25f })
                        {
                            var nodeAt = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * R;
                            var dn = (q - nodeAt).magnitude;
                            c.silver = Mathf.Max(c.silver, In(dn - 0.0026f, ts));
                            c.glow = Mathf.Max(c.glow, In(dn - 0.0013f, ts));
                        }
                        break;
                    }
                case Kind.ForearmPlate:
                    {
                        // 前腕の外（手の甲の側）の角を丸めた板。肘から手首までの 3 割から 7 割 8 分、幅 2.6 cm。板に沿う二本の光る線と三つの端子
                        var elbow = im.left ? f.elbowL : f.elbowR;
                        var wrist = im.left ? f.wristL : f.wristR;
                        var back = im.left ? f.backL : f.backR;
                        float t, s, r, along;
                        Cyl(elbow, wrist, back, p, out t, out s, out r, out along);
                        if (t < 0.2f || t > 0.9f || r > 0.06f) break;
                        var len = (wrist - elbow).magnitude;
                        var q = new Vector2(s, along - 0.54f * len);
                        var hy = 0.24f * len;
                        var d = RoundRect(q, 0.013f, hy, 0.003f);
                        c.silver = In(d, ts);
                        c.edge = Line(Mathf.Abs(d + 0.0008f), 0.0006f, ts);
                        var lines = Mathf.Min(Mathf.Abs(q.x - 0.0055f), Mathf.Abs(q.x + 0.0055f));
                        if (Mathf.Abs(q.y) < hy - 0.006f) c.glow = Line(lines, 0.0006f, ts);
                        foreach (var y in new[] { -0.12f, 0f, 0.12f })
                        {
                            var dn = (q - new Vector2(0f, y * len)).magnitude;
                            c.glow = Mathf.Max(c.glow, In(dn - 0.0016f, ts));
                        }
                        break;
                    }
                case Kind.HandGlow:
                    {
                        // 手の甲の真ん中の小さな輪と光る点、手首へ向かう短い線
                        var wrist = im.left ? f.wristL : f.wristR;
                        var knuckle = im.left ? f.knuckleL : f.knuckleR;
                        var back = im.left ? f.backL : f.backR;
                        float t, s, r, along;
                        Cyl(wrist, knuckle, back, p, out t, out s, out r, out along);
                        if (t < 0f || t > 1.1f || Mathf.Abs(s) > 0.02f) break;
                        var len = (knuckle - wrist).magnitude;
                        var q = new Vector2(s, along - 0.55f * len);
                        var d = q.magnitude;
                        // 手の甲の画素は粗い（512 で 2〜3 mm）ので、輪は 9 mm、光る点は 3.5 mm
                        c.silver = In(d - 0.009f, ts);
                        c.edge = Line(Mathf.Abs(d - 0.0086f), 0.0006f, ts);
                        c.glow = Mathf.Max(In(d - 0.0035f, ts), Line(Mathf.Abs(d - 0.0065f), 0.0007f, ts));
                        if (q.y < 0f && q.y > -0.45f * len)
                        {
                            c.silver = Mathf.Max(c.silver, In(Mathf.Abs(q.x) - 0.002f, ts));
                            c.glow = Mathf.Max(c.glow, Line(Mathf.Abs(q.x), 0.0007f, ts));
                        }
                        break;
                    }
            }
            return c;
        }

        // ---- 暗く・彩度を落とす -------------------------------------------------------

        /// <summary>暗く・彩度を落とす強さ（HSV の S と V に掛ける値。体＝服、頭＝顔と髪の殻、髪の房）</summary>
        public struct Dim
        {
            public float saturation, value;

            public Dim(float saturation, float value)
            {
                this.saturation = saturation;
                this.value = value;
            }
        }

        /// <summary>服は彩度を 4 割 5 分、明るさを 6 割 2 分に。顔は血の気が抜けないよう弱く</summary>
        public static readonly Dim DimBody = new Dim(0.45f, 0.62f), DimHead = new Dim(0.80f, 0.85f), DimHair = new Dim(0.75f, 0.80f);

        /// <summary>色を暗く・彩度を落とす（HSV の S と V に掛ける）。透けはそのまま</summary>
        public static Color[] Dimmed(Color[] px, Dim dim)
        {
            var o = new Color[px.Length];
            for (var i = 0; i < px.Length; i++)
            {
                float h, s, v;
                Color.RGBToHSV(px[i], out h, out s, out v);
                var c = Color.HSVToRGB(h, s * dim.saturation, v * dim.value);
                c.a = px[i].a;
                o[i] = c;
            }
            return o;
        }
    }
}
