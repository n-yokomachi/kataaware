using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools.Study
{
    /// <summary>
    /// 顔の絵と印の絵を、組み立てのときにコードで描く（手で描いた絵は使っていない）。
    ///
    /// 座標は顔の面の入れ物の中（m、+x が本人の右、+y が上）で書き、絵の上の位置へは
    /// <see cref="Canvas.Area"/> の矩形で直す。描いた部品には印の種類（ほくろ・虹彩・他）を持たせ、
    /// 同じ絵の大きさで印の絵も一緒に起こす
    /// </summary>
    public static class FacePaint
    {
        // ---- 印の絵の色 -------------------------------------------------------
        // 地は灰 0.15（本人の体）。ほくろ = R、虹彩 = G、他の部品 = B。α は元の絵と同じ

        const byte Ground = 38;

        public static Color32 MaskGround(byte a) { return new Color32(Ground, Ground, Ground, a); }
        public static Color32 MaskMole(byte a) { return new Color32(255, Ground, Ground, a); }
        public static Color32 MaskIris(byte a) { return new Color32(Ground, 255, Ground, a); }
        public static Color32 MaskOther(byte a) { return new Color32(Ground, Ground, 255, a); }

        public enum Mark : byte { None = 0, Mole = 1, Iris = 2, Other = 3 }

        // ---- 色（sRGB） -------------------------------------------------------

        /// <summary>頭の Skin マテリアルの色そのもの。面の地をこれで塗ると、面の縁が頭の肌に溶ける</summary>
        public static readonly Color Skin = new Color(0.808f, 0.678f, 0.525f);
        static readonly Color Shade = new Color(0.52f, 0.37f, 0.28f);
        static readonly Color Blush = new Color(0.86f, 0.50f, 0.44f);
        static readonly Color Brow = new Color(0.07f, 0.06f, 0.07f);
        static readonly Color Lash = new Color(0.045f, 0.035f, 0.04f);
        static readonly Color Crease = new Color(0.50f, 0.34f, 0.27f);
        static readonly Color LowerLid = new Color(0.60f, 0.42f, 0.36f);
        static readonly Color Sclera = new Color(0.93f, 0.90f, 0.86f);
        static readonly Color IrisRim = new Color(0.28f, 0.14f, 0.04f);
        static readonly Color IrisMid = new Color(0.82f, 0.50f, 0.10f);
        static readonly Color IrisIn = new Color(0.98f, 0.76f, 0.28f);
        static readonly Color Pupil = new Color(0.035f, 0.025f, 0.025f);
        static readonly Color LipUp = new Color(0.64f, 0.34f, 0.32f);
        static readonly Color LipLow = new Color(0.74f, 0.43f, 0.40f);
        static readonly Color LipLine = new Color(0.34f, 0.16f, 0.15f);
        static readonly Color MoleCol = new Color(0.24f, 0.14f, 0.10f);
        static readonly Color Nostril = new Color(0.36f, 0.21f, 0.17f);

        // ---- 顔の配置 --------------------------------------------------------

        /// <summary>
        /// 顔の部品の位置。本人の右（+x）側の値で持ち、左は x を裏返す。
        /// 模型の元の目（Suit_Head の 3 番）は x ±46 mm・y 0 にあり、眉の塊は y 5〜22 mm
        /// </summary>
        public sealed class Layout
        {
            public float eyeX = 0.036f, eyeY = 0.0015f;
            public float eyeIn = 0.014f, eyeOut = 0.0155f;
            public float eyeUp = 0.0068f, eyeDown = 0.0040f;
            public float tiltIn = -0.0008f, tiltOut = 0.0024f;
            public float irisR = 0.0056f, pupilR = 0.0021f;
            public float irisDX = -0.0005f, irisDY = -0.0002f;
            public float browIn = 0.013f, browPeak = 0.040f, browOut = 0.058f;
            public float browYIn = 0.0145f, browYPeak = 0.0215f, browYOut = 0.0160f;
            public float browW = 0.0032f;
            public float moleX = 0.041f, moleY = -0.0195f, moleR = 0.0022f;
            public float nostrilX = 0.0065f, nostrilY = -0.056f;
            public float mouthY = -0.067f, mouthHalf = 0.012f, lipUp = 0.0026f, lipLow = 0.0030f;
            /// <summary>目を面から抜く（段 3: 目は別の面で、面の目の所は穴）</summary>
            public bool eyeHoles;
            /// <summary>地を肌で塗る（段 2・3）。塗らなければ地は透明（段 1）</summary>
            public bool opaque;

            public static Layout Stage1()
            {
                return new Layout();
            }

            /// <summary>段 2: 面が顎まで届くので、鼻と口を正しい高さへ下ろす</summary>
            public static Layout Stage2()
            {
                return new Layout
                {
                    opaque = true,
                    nostrilX = 0.0072f, nostrilY = -0.0450f,
                    mouthY = -0.0720f, mouthHalf = 0.0150f, lipUp = 0.0034f, lipLow = 0.0042f,
                };
            }
        }

        // ---- 絵 ---------------------------------------------------------------

        public sealed class Canvas
        {
            public readonly int W, H;
            /// <summary>面の入れ物の中で、絵の全体が覆う矩形（m）</summary>
            public readonly Rect Area;
            public readonly Color[] Px;
            public readonly Mark[] Marks;
            readonly float pixel;

            public Canvas(int w, int h, Rect area, Color ground)
            {
                W = w; H = h; Area = area;
                Px = new Color[w * h];
                Marks = new Mark[w * h];
                for (var i = 0; i < Px.Length; i++) Px[i] = ground;
                pixel = Mathf.Max(area.width / w, area.height / h);
            }

            /// <summary>
            /// 一つの部品を塗る。f は位置（m）から、色と不透明度（α、縁の被りを含む）を返す。
            /// 被りが半分以上で、印の種類が None でなければ印を上書きする
            /// </summary>
            public void Paint(Rect bounds, Func<float, float, Color> f, Mark mark)
            {
                var i0 = Mathf.Max(0, Mathf.FloorToInt((bounds.xMin - Area.xMin) / Area.width * W) - 1);
                var i1 = Mathf.Min(W - 1, Mathf.CeilToInt((bounds.xMax - Area.xMin) / Area.width * W) + 1);
                var j0 = Mathf.Max(0, Mathf.FloorToInt((bounds.yMin - Area.yMin) / Area.height * H) - 1);
                var j1 = Mathf.Min(H - 1, Mathf.CeilToInt((bounds.yMax - Area.yMin) / Area.height * H) + 1);
                for (var j = j0; j <= j1; j++)
                    for (var i = i0; i <= i1; i++)
                    {
                        var x = Area.xMin + (i + 0.5f) / W * Area.width;
                        var y = Area.yMin + (j + 0.5f) / H * Area.height;
                        var c = f(x, y);
                        if (c.a <= 0.001f) continue;
                        var k = j * W + i;
                        var d = Px[k];
                        var a = Mathf.Clamp01(c.a);
                        var oa = a + d.a * (1f - a);
                        var rgb = oa > 1e-5f ? (new Color(c.r, c.g, c.b) * a + new Color(d.r, d.g, d.b) * (d.a * (1f - a))) / oa : new Color(c.r, c.g, c.b);
                        Px[k] = new Color(rgb.r, rgb.g, rgb.b, oa);
                        if (mark != Mark.None && a >= 0.5f) Marks[k] = mark;
                    }
            }

            /// <summary>縁を 1 画素で溶かした被り。sdf は内側が負（m）</summary>
            public float Cover(float sdf)
            {
                return Mathf.Clamp01(0.5f - sdf / pixel);
            }

            public float Pixel { get { return pixel; } }

            /// <summary>柔らかい影や頬の赤み。楕円のガウス</summary>
            public void Soft(float cx, float cy, float rx, float ry, Color col, float alpha)
            {
                Paint(new Rect(cx - rx * 2f, cy - ry * 2f, rx * 4f, ry * 4f), (x, y) =>
                {
                    var dx = (x - cx) / rx;
                    var dy = (y - cy) / ry;
                    return new Color(col.r, col.g, col.b, alpha * Mathf.Exp(-(dx * dx + dy * dy) * 1.5f));
                }, Mark.None);
            }

            public void Disc(float cx, float cy, float r, Color col, float alpha, Mark mark)
            {
                Paint(new Rect(cx - r, cy - r, r * 2f, r * 2f), (x, y) =>
                {
                    var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
                    return new Color(col.r, col.g, col.b, alpha * Cover(d));
                }, mark);
            }

            /// <summary>
            /// 折れ線に沿った線。幅は始めから終わりへ w0 → w1（m、全幅）。点は曲線を細かく刻んだもの
            /// </summary>
            public void Stroke(IList<Vector2> pts, float w0, float w1, Color col, float alpha, Mark mark)
            {
                var b = new Rect(pts[0], Vector2.zero);
                foreach (var p in pts) { b.xMin = Mathf.Min(b.xMin, p.x); b.xMax = Mathf.Max(b.xMax, p.x); b.yMin = Mathf.Min(b.yMin, p.y); b.yMax = Mathf.Max(b.yMax, p.y); }
                var pad = Mathf.Max(w0, w1);
                b = new Rect(b.xMin - pad, b.yMin - pad, b.width + pad * 2f, b.height + pad * 2f);
                var len = new float[pts.Count];
                for (var i = 1; i < pts.Count; i++) len[i] = len[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
                var total = Mathf.Max(1e-6f, len[pts.Count - 1]);
                Paint(b, (x, y) =>
                {
                    var q = new Vector2(x, y);
                    var best = float.MaxValue;
                    for (var i = 1; i < pts.Count; i++)
                    {
                        var a = pts[i - 1]; var e = pts[i];
                        var ab = e - a;
                        var t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-12f, ab.sqrMagnitude));
                        var s = (len[i - 1] + t * ab.magnitude) / total;
                        var w = Mathf.Lerp(w0, w1, s) * 0.5f;
                        var d = Vector2.Distance(q, a + ab * t) - w;
                        if (d < best) best = d;
                    }
                    return new Color(col.r, col.g, col.b, alpha * Cover(best));
                }, mark);
            }

            /// <summary>印の絵。α は顔の絵と同じ（段 1 の透明な地は透明のまま）</summary>
            public Color32[] MaskPixels()
            {
                var o = new Color32[Px.Length];
                for (var i = 0; i < o.Length; i++)
                {
                    var a = (byte)Mathf.RoundToInt(Mathf.Clamp01(Px[i].a) * 255f);
                    switch (Marks[i])
                    {
                        case Mark.Mole: o[i] = MaskMole(a); break;
                        case Mark.Iris: o[i] = MaskIris(a); break;
                        case Mark.Other: o[i] = MaskOther(a); break;
                        default: o[i] = MaskGround(a); break;
                    }
                }
                return o;
            }

            public Color32[] Pixels()
            {
                var o = new Color32[Px.Length];
                for (var i = 0; i < o.Length; i++) o[i] = Px[i];
                return o;
            }
        }

        // ---- 部品 -------------------------------------------------------------

        /// <summary>二次ベジェを n 等分した点</summary>
        static List<Vector2> Bezier(Vector2 a, Vector2 c, Vector2 b, int n)
        {
            var o = new List<Vector2>();
            for (var i = 0; i <= n; i++)
            {
                var t = (float)i / n;
                o.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b);
            }
            return o;
        }

        /// <summary>目の輪郭。s は +1 が本人の右目、-1 が左目。t は目頭 0 → 目尻 1</summary>
        struct Eye
        {
            public float s, cx, cy;
            public float xin, xout, yin, yout, up, down;

            public Eye(Layout l, float s)
            {
                this.s = s;
                cx = s * l.eyeX; cy = l.eyeY;
                xin = s * (l.eyeX - l.eyeIn); xout = s * (l.eyeX + l.eyeOut);
                yin = l.eyeY + l.tiltIn; yout = l.eyeY + l.tiltOut;
                up = l.eyeUp; down = l.eyeDown;
            }

            public float T(float x) { return (x - xin) / (xout - xin); }
            float Base(float t) { return Mathf.Lerp(yin, yout, t); }
            public float Upper(float t) { t = Mathf.Clamp01(t); return Base(t) + up * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.80f)); }
            public float Lower(float t) { t = Mathf.Clamp01(t); return Base(t) - down * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 1.15f)); }

            /// <summary>目の開きの内側で負になる距離のおおよそ（m）</summary>
            public float Sdf(float x, float y)
            {
                var t = T(x);
                var len = Mathf.Abs(xout - xin);
                var dx = Mathf.Max(-t, t - 1f) * len;
                var dy = Mathf.Max(y - Upper(t), Lower(t) - y);
                return Mathf.Max(dx, dy);
            }

            public Rect Bounds(float pad)
            {
                var x0 = Mathf.Min(xin, xout); var x1 = Mathf.Max(xin, xout);
                return new Rect(x0 - pad, cy - down - 0.004f - pad, x1 - x0 + pad * 2f, up + down + 0.010f + pad * 2f);
            }

            public List<Vector2> UpperLine(float from, float to, float lift)
            {
                var o = new List<Vector2>();
                for (var i = 0; i <= 24; i++)
                {
                    var t = Mathf.Lerp(from, to, i / 24f);
                    o.Add(new Vector2(Mathf.Lerp(xin, xout, t), Upper(t) + lift));
                }
                return o;
            }

            public List<Vector2> LowerLine(float from, float to, float drop)
            {
                var o = new List<Vector2>();
                for (var i = 0; i <= 24; i++)
                {
                    var t = Mathf.Lerp(from, to, i / 24f);
                    o.Add(new Vector2(Mathf.Lerp(xin, xout, t), Lower(t) - drop));
                }
                return o;
            }
        }

        /// <summary>虹彩の色。r は中心からの距離（m）、ang は角度。瞳は黒、縁は暗く、内は明るい琥珀、細い放射の筋</summary>
        public static Color IrisColour(Layout l, float r, float ang)
        {
            if (r < l.pupilR) return Pupil;
            var t = Mathf.InverseLerp(l.pupilR, l.irisR, r);
            Color c;
            if (t < 0.45f) c = Color.Lerp(IrisIn, IrisMid, t / 0.45f);
            else if (t < 0.82f) c = Color.Lerp(IrisMid, IrisMid * 0.78f, (t - 0.45f) / 0.37f);
            else c = Color.Lerp(IrisMid * 0.78f, IrisRim, (t - 0.82f) / 0.18f);
            var streak = 1f + 0.10f * Mathf.Sin(ang * 13f) + 0.06f * Mathf.Sin(ang * 29f + 1.3f);
            c *= streak;
            c.a = 1f;
            return c;
        }

        /// <summary>片目を描く。段 3（eyeHoles）では白目と虹彩は描かず、目の開きを透明に抜く</summary>
        static void DrawEye(Canvas cv, Layout l, float s)
        {
            var e = new Eye(l, s);
            var box = e.Bounds(0.004f);

            // 眼窩の陰り
            cv.Soft(e.cx, e.cy + 0.001f, 0.020f, 0.011f, Shade, l.opaque ? 0.26f : 0.20f);
            // 目頭の脇（鼻の付け根）の陰り
            cv.Soft(s * 0.014f, e.cy + 0.002f, 0.0045f, 0.009f, Shade, 0.16f);

            if (l.eyeHoles)
            {
                // 開きを抜く（切り抜きの閾値で縁が決まるので、ここは内か外かだけ）
                for (var j = 0; j < cv.H; j++)
                    for (var i = 0; i < cv.W; i++)
                    {
                        var x = cv.Area.xMin + (i + 0.5f) / cv.W * cv.Area.width;
                        var y = cv.Area.yMin + (j + 0.5f) / cv.H * cv.Area.height;
                        if (!box.Contains(new Vector2(x, y))) continue;
                        if (e.Sdf(x, y) < 0f)
                        {
                            var k = j * cv.W + i;
                            cv.Px[k] = new Color(0, 0, 0, 0);
                            cv.Marks[k] = Mark.None;
                        }
                    }
            }
            else
            {
                PaintBall(cv, l, e, true);
            }

            // 上の瞼の線（まつ毛の生え際）。目尻へ太く、目尻の先を少し跳ねる
            var lashLift = l.eyeHoles ? 0.0006f : 0.0002f;
            cv.Stroke(e.UpperLine(0.04f, 1.0f, lashLift), 0.0009f, 0.0019f, Lash, 1f, Mark.Other);
            var tip = new Vector2(e.xout, e.Upper(1f) + lashLift);
            cv.Stroke(new List<Vector2> { tip + new Vector2(-s * 0.002f, 0.0002f), tip + new Vector2(s * 0.0032f, 0.0016f) }, 0.0014f, 0.0004f, Lash, 1f, Mark.Other);
            // 二重の線
            cv.Stroke(e.UpperLine(0.18f, 0.92f, 0.0030f), 0.0006f, 0.0005f, Crease, 0.65f, Mark.Other);
            // 下の瞼
            cv.Stroke(e.LowerLine(0.30f, 0.98f, l.eyeHoles ? 0.0004f : 0.0001f), 0.0005f, 0.0007f, LowerLid, 0.7f, Mark.Other);
        }

        /// <summary>白目・虹彩・瞳・光。clip なら目の開きで切る（面の上に描くとき）</summary>
        static void PaintBall(Canvas cv, Layout l, Eye e, bool clip)
        {
            var box = clip ? e.Bounds(0.001f) : new Rect(cv.Area.xMin, cv.Area.yMin, cv.Area.width, cv.Area.height);
            float icx = e.cx + e.s * l.irisDX, icy = e.cy + l.irisDY;
            // 白目。上の瞼の下を陰らせる
            cv.Paint(box, (x, y) =>
            {
                var cov = clip ? cv.Cover(e.Sdf(x, y)) : 1f;
                if (cov <= 0f) return new Color(0, 0, 0, 0);
                var t = e.T(x);
                var fromTop = (e.Upper(t) - y) / Mathf.Max(1e-4f, e.up + e.down);
                var c = Sclera * Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(fromTop * 2.2f));
                c.a = cov;
                return c;
            }, Mark.Other);
            // 虹彩と瞳
            cv.Paint(new Rect(icx - l.irisR, icy - l.irisR, l.irisR * 2f, l.irisR * 2f), (x, y) =>
            {
                var r = Mathf.Sqrt((x - icx) * (x - icx) + (y - icy) * (y - icy));
                var cov = cv.Cover(r - l.irisR) * (clip ? cv.Cover(e.Sdf(x, y)) : 1f);
                if (cov <= 0f) return new Color(0, 0, 0, 0);
                var c = IrisColour(l, r, Mathf.Atan2(y - icy, x - icx));
                // 上の瞼の影
                var t = e.T(x);
                var fromTop = (e.Upper(t) - y) / Mathf.Max(1e-4f, e.up + e.down);
                c *= Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(fromTop * 2.6f));
                c.a = cov;
                return c;
            }, Mark.Iris);
            // 瞳は他の部品として数える（虹彩の色を測るとき外す）
            cv.Paint(new Rect(icx - l.pupilR, icy - l.pupilR, l.pupilR * 2f, l.pupilR * 2f), (x, y) =>
            {
                var r = Mathf.Sqrt((x - icx) * (x - icx) + (y - icy) * (y - icy));
                var cov = cv.Cover(r - l.pupilR) * (clip ? cv.Cover(e.Sdf(x, y)) : 1f);
                return new Color(Pupil.r, Pupil.g, Pupil.b, cov);
            }, Mark.Other);
            // 光（段 3 の目の面では描かない。光は艶で拾わせる）
            if (clip) cv.Disc(icx - 0.0017f, icy + 0.0019f, 0.0009f, new Color(1f, 0.98f, 0.94f), 0.95f, Mark.Other);
        }

        static void DrawBrow(Canvas cv, Layout l, float s)
        {
            var a = new Vector2(s * l.browIn, l.browYIn);
            var p = new Vector2(s * l.browPeak, l.browYPeak + 0.0020f);
            var b = new Vector2(s * l.browOut, l.browYOut);
            var pts = Bezier(a, p, b, 28);
            // 眉頭を太く、眉尻へ細く。毛の流れの代わりに、頭の側を少し薄く
            cv.Stroke(pts, l.browW, 0.0009f, Brow, 0.93f, Mark.Other);
            cv.Stroke(Bezier(a + new Vector2(0, 0.0004f), p, b, 28), l.browW * 0.55f, 0.0005f, Brow, 0.5f, Mark.Other);
        }

        static void DrawNose(Canvas cv, Layout l)
        {
            // 鼻の下の陰りと小鼻
            cv.Soft(0f, l.nostrilY - 0.0030f, 0.011f, 0.0032f, Shade, l.opaque ? 0.40f : 0.30f);
            for (var s = -1; s <= 1; s += 2)
            {
                var cx = s * l.nostrilX; var cy = l.nostrilY;
                cv.Paint(new Rect(cx - 0.004f, cy - 0.003f, 0.008f, 0.006f), (x, y) =>
                {
                    var dx = (x - cx) / 0.0024f; var dy = (y - cy) / 0.0012f;
                    var d = (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * 0.0012f;
                    return new Color(Nostril.r, Nostril.g, Nostril.b, 0.9f * cv.Cover(d));
                }, Mark.Other);
                if (l.opaque) cv.Soft(s * (l.nostrilX + 0.004f), cy + 0.003f, 0.003f, 0.005f, Shade, 0.18f);
            }
        }

        static void DrawMouth(Canvas cv, Layout l)
        {
            var y0 = l.mouthY; var hw = l.mouthHalf;
            // 上唇の山（キューピッドの弓）と下唇のふくらみ
            Func<float, float> upTop = x =>
            {
                var u = Mathf.Abs(x) / hw;
                var bow = 0.25f * Mathf.Exp(-Mathf.Pow((u - 0.28f) / 0.16f, 2f)) - 0.18f * Mathf.Exp(-Mathf.Pow(u / 0.09f, 2f));
                return y0 + l.lipUp * (Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)) * (0.85f + bow));
            };
            Func<float, float> lowBot = x =>
            {
                var u = Mathf.Abs(x) / hw;
                return y0 - l.lipLow * Mathf.Pow(Mathf.Max(0f, 1f - u * u), 0.7f);
            };
            Func<float, float> line = x =>
            {
                var u = Mathf.Abs(x) / hw;
                return y0 + 0.0003f * (1f - u * u) - 0.0006f * u * u;
            };
            var box = new Rect(-hw - 0.002f, y0 - l.lipLow - 0.002f, hw * 2f + 0.004f, l.lipUp + l.lipLow + 0.004f);
            if (l.opaque) cv.Soft(0f, y0 - l.lipLow - 0.0030f, 0.009f, 0.0028f, Shade, 0.32f);
            cv.Paint(box, (x, y) =>
            {
                if (Mathf.Abs(x) >= hw) return new Color(0, 0, 0, 0);
                var mid = line(x);
                if (y >= mid)
                {
                    var d = y - upTop(x);
                    var c = Color.Lerp(LipUp, LipUp * 0.85f, Mathf.InverseLerp(mid + l.lipUp, mid, y));
                    c.a = cv.Cover(d) * 0.95f;
                    return c;
                }
                else
                {
                    var d = lowBot(x) - y;
                    // 下唇の真ん中に明るみ
                    var hi = Mathf.Exp(-Mathf.Pow(x / (hw * 0.35f), 2f) - Mathf.Pow((y - (mid - l.lipLow * 0.45f)) / (l.lipLow * 0.3f), 2f));
                    var c = Color.Lerp(LipLow, new Color(0.86f, 0.60f, 0.56f), hi * 0.6f);
                    c.a = cv.Cover(d) * 0.95f;
                    return c;
                }
            }, Mark.Other);
            // 唇の合わせ目と口角
            var pts = new List<Vector2>();
            for (var i = 0; i <= 30; i++) { var x = Mathf.Lerp(-hw * 1.03f, hw * 1.03f, i / 30f); pts.Add(new Vector2(x, line(x))); }
            cv.Stroke(pts, 0.0008f, 0.0008f, LipLine, 0.9f, Mark.Other);
            cv.Soft(-hw, y0 - 0.0003f, 0.0014f, 0.0012f, LipLine, 0.45f);
            cv.Soft(hw, y0 - 0.0003f, 0.0014f, 0.0012f, LipLine, 0.45f);
        }

        /// <summary>顔の絵を一枚描く。twin なら ほくろを本人の右（+x）へ。mole = false でほくろ無し</summary>
        public static Canvas Face(int size, Rect area, Layout l, bool twin, bool mole)
        {
            var cv = new Canvas(size, size, area, l.opaque ? Skin : new Color(Skin.r, Skin.g, Skin.b, 0f));
            if (l.opaque)
            {
                // 頬の赤み、額と鼻筋の明るみ、顎の下の陰り
                cv.Soft(-0.046f, -0.032f, 0.017f, 0.012f, Blush, 0.20f);
                cv.Soft(0.046f, -0.032f, 0.017f, 0.012f, Blush, 0.20f);
                cv.Soft(0f, 0.045f, 0.035f, 0.018f, new Color(0.86f, 0.74f, 0.60f), 0.25f);
                cv.Soft(0f, -0.104f, 0.030f, 0.008f, Shade, 0.22f);
                // 法令線の辺りの柔らかい陰り
                cv.Soft(-0.020f, -0.058f, 0.004f, 0.009f, Shade, 0.14f);
                cv.Soft(0.020f, -0.058f, 0.004f, 0.009f, Shade, 0.14f);
            }
            else
            {
                cv.Soft(-0.048f, -0.030f, 0.016f, 0.011f, Blush, 0.20f);
                cv.Soft(0.048f, -0.030f, 0.016f, 0.011f, Blush, 0.20f);
            }
            DrawBrow(cv, l, -1f);
            DrawBrow(cv, l, 1f);
            DrawEye(cv, l, -1f);
            DrawEye(cv, l, 1f);
            DrawNose(cv, l);
            DrawMouth(cv, l);
            if (mole)
            {
                var s = twin ? 1f : -1f;
                // 縁を少し柔らかく。真ん中が一番濃い
                var cx = s * l.moleX; var cy = l.moleY;
                cv.Paint(new Rect(cx - l.moleR * 1.5f, cy - l.moleR * 1.5f, l.moleR * 3f, l.moleR * 3f), (x, y) =>
                {
                    var r = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    var c = Color.Lerp(MoleCol * 0.85f, MoleCol * 1.1f, r / l.moleR);
                    c.a = cv.Cover(r - l.moleR);
                    return c;
                }, Mark.Mole);
            }
            return cv;
        }

        /// <summary>
        /// 段 3 の目の面の絵。目の中心から ±half の正方形を覆う。白目・虹彩・瞳だけで、光は描かない
        /// </summary>
        public static Canvas Ball(int size, Layout l, float half)
        {
            var e = new Eye(l, 1f);
            var area = new Rect(e.cx - half, e.cy - half, half * 2f, half * 2f);
            var cv = new Canvas(size, size, area, new Color(Sclera.r * 0.85f, Sclera.g * 0.85f, Sclera.b * 0.85f, 1f));
            PaintBall(cv, l, e, false);
            // 周りを暗く（瞼の奥へ回り込む所）
            cv.Paint(area, (x, y) =>
            {
                var dx = (x - e.cx) / half; var dy = (y - e.cy) / half;
                var r = Mathf.Sqrt(dx * dx + dy * dy);
                return new Color(0.35f, 0.28f, 0.25f, Mathf.Clamp01((r - 0.55f) / 0.4f) * 0.7f);
            }, Mark.None);
            return cv;
        }

        // ---- 保存 -------------------------------------------------------------

        /// <summary>顔の絵・印の絵・ほくろ無しの絵と、それぞれのマテリアルを書き出す</summary>
        public static string Write(string prefix, int size, Rect area, Layout l, Material template, Action<Material> setup)
        {
            FaceStages.EnsureDir();
            var made = new List<string>();
            foreach (var twin in new[] { false, true })
            {
                var side = twin ? "twin" : "self";
                var cv = Face(size, area, l, twin, true);
                var tex = FaceStages.SavePng(cv.Pixels(), size, size, FaceStages.Dir + "/" + prefix + "_" + side + ".png", false);
                FaceStages.SavePng(cv.MaskPixels(), size, size, FaceStages.Dir + "/Mask" + prefix.Substring(4) + "_" + side + ".png", true);
                var m = new Material(template) { name = prefix + "_" + side };
                m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                if (setup != null) setup(m);
                FaceStages.SaveMaterial(m, FaceStages.Dir + "/" + prefix + "_" + side + ".mat");
                made.Add(prefix + "_" + side);
            }
            var bare = Face(size, area, l, false, false);
            var bareTex = FaceStages.SavePng(bare.Pixels(), size, size, FaceStages.Dir + "/" + prefix + "_nomole.png", false);
            var bm = new Material(template) { name = prefix + "_nomole" };
            bm.SetTexture("_BaseMap", bareTex);
            if (bm.HasProperty("_MainTex")) bm.SetTexture("_MainTex", bareTex);
            if (setup != null) setup(bm);
            FaceStages.SaveMaterial(bm, FaceStages.Dir + "/" + prefix + "_nomole.mat");
            made.Add(prefix + "_nomole");
            return string.Join(", ", made.ToArray());
        }

        /// <summary>URP Lit を透明（α ブレンド）にする。段 1 の面は透明な地に陰りを半透明で重ねる</summary>
        public static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>URP Lit を不透明にする（段 2）。艶は頭の肌の写しと揃える</summary>
        public static void MakeOpaque(Material m, bool clip, float smooth)
        {
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_ZWrite", 1f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetFloat("_AlphaClip", clip ? 1f : 0f);
            if (clip) { m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff", 0.5f); m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; }
            else { m.DisableKeyword("_ALPHATEST_ON"); m.SetOverrideTag("RenderType", "Opaque"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry; }
            m.SetFloat("_Smoothness", smooth);
        }
    }
}
