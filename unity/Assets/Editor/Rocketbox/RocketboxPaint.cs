using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// Rocketbox の女大 14 の縮めたテクスチャ（512）に、組み立てのときに手を入れる。
    /// 値はすべて <see cref="Look"/> にあり、後から直せる。
    ///
    /// - 髪を黒に（頭のテクスチャの頭皮の髪と、透けの絵の髪の房。毛筋の明暗は残す）
    /// - カーディガンを黒に（ニットの目の明暗は残す）
    /// - 目は元の色のまま（虹彩は目の玉の部品で、左右の目が頭のテクスチャの同じ一枚の目の絵を使う）
    /// - 黒子（主人公は本人の左 = −x の目の下、片割れは右 = +x の目の下）
    /// - 美人への控えめな手入れ（肌のむら、目元、眉、唇、頬から顎の陰り）
    ///
    /// 場所の見分けは、テクスチャの画素ごとに模型の束ねた姿勢での位置（<see cref="Surface"/>）を求め、
    /// 目・口・鼻・眉などの場所は顔の骨（Bip01 LEye など）からの距離で決める。
    /// 顔のテクスチャは左右で別の絵（使い回していない）なので、黒子は絵に直に描ける
    /// </summary>
    public static class RocketboxPaint
    {
        [Serializable]
        public sealed class Look
        {
            [Header("髪")]
            public Color hairShadow = new Color(0.016f, 0.015f, 0.018f);
            public Color hairShine = new Color(0.215f, 0.205f, 0.225f);
            [Tooltip("元の髪の明度をこの範囲で 0〜1 に伸ばし、影から艶へ塗る")]
            public float hairLo = 0.06f, hairHi = 0.52f, hairGamma = 1.25f;
            [Tooltip("頭のテクスチャで髪と見なす明るさ。ぼかした明度が cut より暗ければ髪、fade より明るければ肌")]
            public float hairCut = 0.38f, hairFade = 0.50f;
            [Tooltip("眉を髪に合わせて暗くする量（0〜1）")]
            public float browDarken = 0.40f;
            [Tooltip("頭のテクスチャで髪と見なす一番低い所（目の高さから下へ m）。これより下の暗い所は胸元の影などとして外す。長い髪の人は大きく")]
            public float hairLowest = 0.17f;

            [Header("カーディガン（ニットの服を黒に。服を元のままにする人は false）")]
            public bool blackenKnit = true;
            public Color knitShadow = new Color(0.012f, 0.012f, 0.014f);
            public Color knitShine = new Color(0.085f, 0.087f, 0.098f);
            public float knitLo = 0.22f, knitHi = 0.68f, knitGamma = 1.10f;


            [Header("黒子")]
            [Tooltip("直径（m）。0 なら描かない")]
            public float moleDiameter = 0.005f;
            [Tooltip("目の玉の中心から外へ（m）と下へ（m）")]
            public float moleOut = 0.008f, moleDown = 0.0145f;
            public Color moleColour = new Color(0.22f, 0.13f, 0.10f);

            [Header("手入れ（beauty 0 = 手を入れない、0.5 = 弱め、1 = 強め。下の値は強めのときの量）")]
            public float beauty = 0.5f;
            [Tooltip("肌の色むらを大きくぼかした色へ寄せる量")]
            public float evenChroma = 0.75f;
            [Tooltip("肌の細かいむら（1〜4 画素）を均す量")]
            public float evenLuma = 0.45f;
            [Tooltip("上瞼の際を暗くする量")]
            public float lashLine = 0.60f;
            public Color lashColour = new Color(0.07f, 0.045f, 0.045f);
            [Tooltip("眉をさらに暗く、はっきりさせる量")]
            public float browDefine = 0.25f;
            public Color lipColour = new Color(0.68f, 0.36f, 0.37f);
            public float lipTint = 0.40f;
            [Tooltip("頬の下から顎の脇を暗くして顔を細く見せる量")]
            public float contour = 0.07f;
            [Tooltip("顎の骨（Bip01 MJaw）を左右に縮める量。顎から頬の下が細くなる。テクスチャでなく形の手入れで、組み立てのときに骨へ掛ける")]
            public float jawSlim = 0.10f;

            /// <summary>顎の骨の左右の倍率</summary>
            public float JawScale { get { return 1f - Mathf.Clamp01(beauty) * jawSlim; } }

            [Header("艶（頭のマテリアルの Specular の絵。RGB = 照り返しの強さ、A = 滑らかさ）")]
            [Tooltip("肌の照り返しの強さ（線形の値）。髪はここを 0 にする")]
            public float skinSpecular = 0.04f;
            public float skinSmoothness = 0.22f;
            [Tooltip("髪の滑らかさ。髪は照り返しを 0 にするので、見た目はほとんど変わらない")]
            public float hairSmoothness = 0.10f;
            [Tooltip("目の玉の滑らかさ（主光を目に映すため）")]
            public float eyeSmoothness = 0.55f;

            [Header("体の肌の色を頭に揃える（頭と体が別の人のとき）")]
            public bool matchSkin;

            [Header("別の人の髪を載せるとき（顔の人の頭のテクスチャ）")]
            [Tooltip("頭皮を兼ねた髪の殻を、毛筋の明暗の無い影の色一色で塗る（別の人の髪の下地にし、殻の外に見えても目立たせない）")]
            public bool flatHair;
            [Tooltip("眉より上の額とこめかみで、元の前髪の影が焼き込まれて暗い肌を、額の真ん中の明るさまで上げる量（0〜1）")]
            public float liftForehead;
            [Tooltip("揃える強さ（0〜1）")]
            public float skinMatch = 1f;

            public Look Clone() { return (Look)MemberwiseClone(); }
        }

        // ---- 模型の上の位置 ---------------------------------------------------

        /// <summary>テクスチャの画素ごとの、模型の根の中の位置（束ねた姿勢）</summary>
        public sealed class Surface
        {
            public int N;
            public Vector3[] P;
            public bool[] On;

            /// <summary>三角を UV に並べて、画素の中心の位置を重心の座標で埋める。縁は 2 画素だけ外へ延ばす</summary>
            public static Surface Of(Vector3[] verts, Vector2[] uv, int[] tris, int n)
            {
                var s = new Surface { N = n, P = new Vector3[n * n], On = new bool[n * n] };
                for (var t = 0; t < tris.Length; t += 3)
                {
                    int ia = tris[t], ib = tris[t + 1], ic = tris[t + 2];
                    Vector2 a = uv[ia] * n, b = uv[ib] * n, c = uv[ic] * n;
                    var x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
                    var x1 = Mathf.Min(n - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
                    var y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
                    var y1 = Mathf.Min(n - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
                    var den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                    if (Mathf.Abs(den) < 1e-9f) continue;
                    for (var y = y0; y <= y1; y++)
                        for (var x = x0; x <= x1; x++)
                        {
                            float px = x + 0.5f, py = y + 0.5f;
                            var l1 = ((b.y - c.y) * (px - c.x) + (c.x - b.x) * (py - c.y)) / den;
                            var l2 = ((c.y - a.y) * (px - c.x) + (a.x - c.x) * (py - c.y)) / den;
                            var l3 = 1f - l1 - l2;
                            if (l1 < -0.001f || l2 < -0.001f || l3 < -0.001f) continue;
                            var k = y * n + x;
                            s.P[k] = verts[ia] * l1 + verts[ib] * l2 + verts[ic] * l3;
                            s.On[k] = true;
                        }
                }
                for (var pass = 0; pass < 2; pass++) s.Grow();
                return s;
            }

            void Grow()
            {
                var on = (bool[])On.Clone();
                for (var y = 0; y < N; y++)
                    for (var x = 0; x < N; x++)
                    {
                        var k = y * N + x;
                        if (On[k]) continue;
                        var sum = Vector3.zero;
                        var cnt = 0;
                        for (var dy = -1; dy <= 1; dy++)
                            for (var dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= N || yy >= N) continue;
                                var j = yy * N + xx;
                                if (!On[j]) continue;
                                sum += P[j];
                                cnt++;
                            }
                        if (cnt == 0) continue;
                        P[k] = sum / cnt;
                        on[k] = true;
                    }
                On = on;
            }
        }

        /// <summary>顔の骨の位置（模型の根の中、束ねた姿勢）</summary>
        public sealed class Anchors
        {
            public Vector3 eyeL, eyeR, blinkTopL, blinkTopR, browInL, browInR, browOutL, browOutR;
            public Vector3 upperLip, lowerLip, mouthL, mouthR, nose, cheekL, cheekR, head;

            public static Anchors Of(Transform root)
            {
                var d = new Dictionary<string, Transform>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) d[t.name] = t;
                Func<string, Vector3> at = n =>
                {
                    Transform t;
                    if (!d.TryGetValue(n, out t)) throw new InvalidOperationException("顔の骨が無い: " + n);
                    return root.InverseTransformPoint(t.position);
                };
                return new Anchors
                {
                    eyeL = at("Bip01 LEye"), eyeR = at("Bip01 REye"),
                    blinkTopL = at("Bip01 LEyeBlinkTop"), blinkTopR = at("Bip01 REyeBlinkTop"),
                    browInL = at("Bip01 LInnerEyebrow"), browInR = at("Bip01 RInnerEyebrow"),
                    browOutL = at("Bip01 LOuterEyebrow"), browOutR = at("Bip01 ROuterEyebrow"),
                    upperLip = at("Bip01 MUpperLip"), lowerLip = at("Bip01 MBottomLip"),
                    mouthL = at("Bip01 LMouthCorner"), mouthR = at("Bip01 RMouthCorner"),
                    nose = at("Bip01 MNose"), cheekL = at("Bip01 LCheek"), cheekR = at("Bip01 RCheek"),
                    head = at("Bip01 Head"),
                };
            }
        }

        // ---- 頭 ---------------------------------------------------------------

        /// <summary>頭に手を入れた結果と、黒子を置いた所</summary>
        public sealed class HeadResult
        {
            public Color[] Px;
            /// <summary>黒子の中心（模型の根の中）と UV。黒子が無ければ UV は負</summary>
            public Vector3 Mole;
            public Vector2 MoleUv = new Vector2(-1f, -1f);
            /// <summary>印の絵（R = 黒子、G = 虹彩、B = 他の顔の部品、地は灰）。<see cref="HalfAware.EditorTools.Study.FaceStudy"/> で測るのに使う</summary>
            public Color32[] Mask;
            /// <summary>頭のマテリアルの Specular の絵（sRGB の RGB = 照り返しの強さ、A = 滑らかさ）</summary>
            public Color32[] Spec;
            /// <summary>頭のテクスチャで髪と見なした重み（0〜1、N×N）。撮り比べで髪の長さを測るのに使う</summary>
            public float[] Hair;
            public int N;
            public string Note = "";
        }

        /// <summary>
        /// 頭のテクスチャ（顔・頭皮の髪・首と胸・口の中・目の玉）に手を入れる。
        /// twin なら黒子を本人の右（+x）の目の下へ
        /// </summary>
        /// <param name="irisUv">目の玉の絵の虹彩の中心（UV）。塗らない。印の絵で虹彩を測るのにだけ使う</param>
        public static HeadResult Head(Color[] src, Surface s, Anchors a, Look k, bool twin, Vector2 irisUv, float irisRadius)
        {
            var n = s.N;
            var px = (Color[])src.Clone();
            var H = new float[n * n];
            var S = new float[n * n];
            var V = new float[n * n];
            for (var i = 0; i < px.Length; i++) Color.RGBToHSV(src[i], out H[i], out S[i], out V[i]);
            var scale = n / 512f;
            var Vb = Blur(V, s.On, n, Mathf.Max(1, Mathf.RoundToInt(1 * scale)));
            var Vl = Blur(V, s.On, n, Mathf.Max(2, Mathf.RoundToInt(6 * scale)));
            var eyeY = (a.eyeL.y + a.eyeR.y) * 0.5f;

            // 場所の重み
            var hair = new float[n * n];
            var skin = new float[n * n];
            var brow = new float[n * n];
            for (var i = 0; i < px.Length; i++)
            {
                // UV の島の外（詰め物）は、暗ければ髪と同じに塗る（ミップマップで島の縁へ滲むため）
                if (!s.On[i]) { hair[i] = Smooth(k.hairFade, k.hairCut, Vb[i]); continue; }
                var p = s.P[i];
                var guarded = InEye(p, a.eyeL) || InEye(p, a.eyeR) || InMouth(p, a) || InNose(p, a);
                // 髪は顎の高さより上だけ（胸元の影を髪と取り違えない）
                var above = Smooth(eyeY - k.hairLowest - 0.02f, eyeY - k.hairLowest + 0.02f, p.y);
                hair[i] = guarded ? 0f : Smooth(k.hairFade, k.hairCut, Vb[i]) * above;
                // 頭の天辺（額の生え際より上）は明るい毛筋も髪
                hair[i] = Mathf.Max(hair[i], Smooth(eyeY + 0.080f, eyeY + 0.095f, p.y));
                var bw = BrowZone(p, a);
                if (bw > 0f)
                {
                    var dark = (Vl[i] - V[i]) / Mathf.Max(0.05f, Vl[i]);
                    brow[i] = bw * Smooth(0.02f, 0.10f, dark) * (1f - hair[i]);
                }
                var faceAndNeck = Smooth(eyeY - 0.24f, eyeY - 0.20f, p.y);
                var skinCol = Smooth(0.40f, 0.52f, Vb[i]) * Band(S[i], 0.18f, 0.24f, 0.62f, 0.70f) * HueNear(H[i] * 360f, 20f, 16f, 10f);
                skin[i] = guarded || InEyeWide(p, a.eyeL) || InEyeWide(p, a.eyeR) ? 0f : (1f - hair[i]) * faceAndNeck * skinCol * (1f - Mathf.Clamp01(bw * 2f));
            }
            hair = Blur(hair, s.On, n, 1);

            var b = Mathf.Clamp01(k.beauty);

            // 1. 肌のむら（手入れ）。明るさの形は残し、色のむらと細かい斑だけを均す
            if (b > 0f)
            {
                var Y = new float[n * n];
                var Cb = new float[n * n];
                var Cr = new float[n * n];
                for (var i = 0; i < px.Length; i++) ToYCC(px[i], out Y[i], out Cb[i], out Cr[i]);
                var rBig = Mathf.Max(2, Mathf.RoundToInt(10 * scale));
                var Cbb = Blur(Cb, s.On, n, rBig);
                var Crb = Blur(Cr, s.On, n, rBig);
                var Y1 = Blur(Y, s.On, n, 1);
                var Y4 = Blur(Y, s.On, n, Mathf.Max(2, Mathf.RoundToInt(4 * scale)));
                for (var i = 0; i < px.Length; i++)
                {
                    if (skin[i] <= 0f) continue;
                    var wc = b * k.evenChroma * skin[i];
                    var wl = b * k.evenLuma * skin[i];
                    var y = Mathf.Lerp(Y[i], Y4[i] + (Y[i] - Y1[i]), wl);
                    var cb = Mathf.Lerp(Cb[i], Cbb[i], wc);
                    var cr = Mathf.Lerp(Cr[i], Crb[i], wc);
                    px[i] = FromYCC(y, cb, cr, px[i].a);
                }
            }

            // 2. 髪を黒に（別の人の髪を載せるときは、毛筋の無い影の色一色）
            for (var i = 0; i < px.Length; i++)
            {
                if (hair[i] <= 0f) continue;
                var ink = k.flatHair ? k.hairShadow : HairRamp(V[i], k);
                ink.a = px[i].a;
                px[i] = Color.Lerp(px[i], ink, hair[i]);
            }

            // 2b. 元の前髪の影で暗い額とこめかみの肌を、額の真ん中の明るさまで上げる（別の人の髪を載せるとき）
            if (k.liftForehead > 0f)
            {
                var browTop = Mathf.Max(Mathf.Max(a.browInL.y, a.browOutL.y), Mathf.Max(a.browInR.y, a.browOutR.y));
                var refs = new List<float>();
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i] || hair[i] > 0.1f) continue;
                    var p = s.P[i];
                    if (Mathf.Abs(p.x) < 0.02f && p.y > browTop + 0.008f && p.y < browTop + 0.035f && p.z > a.eyeL.z) refs.Add(Lum(px[i]));
                }
                if (refs.Count > 0)
                {
                    refs.Sort();
                    var lref = refs[refs.Count / 2];
                    for (var i = 0; i < px.Length; i++)
                    {
                        if (!s.On[i] || hair[i] >= 0.5f) continue;
                        var p = s.P[i];
                        var zone = Smooth(browTop - 0.006f, browTop + 0.004f, p.y) * Smooth(a.head.z - 0.01f, a.head.z + 0.02f, p.z) * (1f - hair[i] * 2f);
                        if (zone <= 0f) continue;
                        var l = Lum(px[i]);
                        if (l >= lref * 0.97f || l < 0.05f) continue;
                        var gain = Mathf.Min(1.8f, lref / l);
                        var g = Mathf.Lerp(1f, gain, zone * Mathf.Clamp01(k.liftForehead));
                        var c = px[i];
                        px[i] = new Color(Mathf.Clamp01(c.r * g), Mathf.Clamp01(c.g * g), Mathf.Clamp01(c.b * g), c.a);
                    }
                }
            }

            // 3. 眉。髪に合わせて暗く（常に）、手入れでさらにはっきり
            var browAmount = Mathf.Clamp01(k.browDarken + b * k.browDefine);
            for (var i = 0; i < px.Length; i++)
            {
                if (brow[i] <= 0f) continue;
                var w = brow[i] * browAmount;
                var c = px[i];
                var grey = c.r * 0.30f + c.g * 0.59f + c.b * 0.11f;
                var dark = Color.Lerp(c, new Color(grey, grey, grey), 0.35f) * 0.38f;
                px[i] = Color.Lerp(c, new Color(dark.r, dark.g, dark.b, c.a), w);
            }

            // 4. 目元（手入れ）。瞼の開きの縁（元の絵で暗い赤の所）の上側を暗くする
            if (b > 0f && k.lashLine > 0f)
            {
                foreach (var eye in new[] { a.eyeL, a.eyeR })
                {
                    var open = new float[n * n];
                    for (var i = 0; i < px.Length; i++)
                    {
                        if (!s.On[i] || !InEye(s.P[i], eye)) continue;
                        var h = H[i] * 360f;
                        var red = (h < 16f || h > 340f) && S[i] > 0.33f && V[i] < 0.66f;
                        open[i] = red ? 1f : 0f;
                    }
                    var near = Blur(open, s.On, n, Mathf.Max(1, Mathf.RoundToInt(2 * scale)));
                    for (var i = 0; i < px.Length; i++)
                    {
                        if (near[i] <= 0f || open[i] > 0.5f) continue;
                        var p = s.P[i];
                        if (!InEye(p, eye)) continue;
                        var up = Smooth(eye.y - 0.0015f, eye.y + 0.0020f, p.y);
                        var lower = 0.30f * (1f - up);
                        var w = Mathf.Clamp01(near[i] * 2.2f) * (up + lower) * b * k.lashLine;
                        px[i] = Color.Lerp(px[i], new Color(k.lashColour.r, k.lashColour.g, k.lashColour.b, px[i].a), w);
                    }
                }
            }

            // 5. 唇（手入れ）。口の周りで元の赤みが肌より強い所へ色を差す
            if (b > 0f && k.lipTint > 0f)
            {
                var c0 = new Vector3(0f, (a.upperLip.y + a.lowerLip.y) * 0.5f, 0f);
                var half = Mathf.Abs(a.mouthL.x - a.mouthR.x) * 0.5f + 0.003f;
                var lipLum = Lum(k.lipColour);
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i]) continue;
                    var p = s.P[i];
                    if (p.z < a.mouthL.z - 0.012f) continue;
                    var e = Mathf.Sqrt(Sq(p.x / half) + Sq((p.y - c0.y) / 0.0115f));
                    var zone = Smooth(1.0f, 0.70f, e);
                    if (zone <= 0f) continue;
                    var c = src[i];
                    var red = (c.r - c.g) / Mathf.Max(0.05f, c.r);
                    var lip = zone * Smooth(0.26f, 0.31f, red);
                    if (lip <= 0f) continue;
                    var l = Lum(px[i]);
                    var tint = k.lipColour * (l / Mathf.Max(0.05f, lipLum));
                    tint.a = px[i].a;
                    px[i] = Color.Lerp(px[i], tint, lip * b * k.lipTint);
                }
            }

            // 6. 頬の下から顎の脇の陰り（手入れ）
            if (b > 0f && k.contour > 0f)
            {
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i] || hair[i] > 0.5f) continue;
                    var p = s.P[i];
                    var ax = Mathf.Abs(p.x);
                    var side = Smooth(0.038f, 0.062f, ax) * Smooth(0.090f, 0.068f, ax);
                    var height = Smooth(a.lowerLip.y - 0.030f, a.lowerLip.y - 0.005f, p.y) * Smooth(a.cheekL.y + 0.002f, a.cheekL.y - 0.012f, p.y);
                    var front = Smooth(a.head.z - 0.01f, a.head.z + 0.02f, p.z);
                    var w = side * height * front * b * k.contour;
                    if (w <= 0f) continue;
                    var c = px[i];
                    px[i] = new Color(c.r * (1f - w), c.g * (1f - w * 1.05f), c.b * (1f - w * 1.05f), c.a);
                }
            }

            // 7. 虹彩は塗らない。印の絵のために、瞳の外から虹彩の縁までの輪だけを覚える
            var irisMask = new bool[n * n];
            {
                var cx = irisUv.x * n;
                var cy = irisUv.y * n;
                var R = irisRadius * n;
                var reach = Mathf.CeilToInt(R + 2);
                for (var y = Mathf.Max(0, (int)cy - reach); y <= Mathf.Min(n - 1, (int)cy + reach); y++)
                    for (var x = Mathf.Max(0, (int)cx - reach); x <= Mathf.Min(n - 1, (int)cx + reach); x++)
                    {
                        var r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        if (r < R && r > R * 0.33f) irisMask[y * n + x] = true;
                    }
            }

            // 8. 黒子
            var res = new HeadResult();
            var moleMask = new bool[n * n];
            if (k.moleDiameter > 0f)
            {
                var eye = twin ? a.eyeR : a.eyeL;
                var outward = Mathf.Sign(eye.x);
                var target = new Vector2(eye.x + outward * k.moleOut, eye.y - k.moleDown);
                // 目の下の肌の上で、狙った所に一番近く、一番手前の画素
                var best = -1;
                var bestScore = float.MaxValue;
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i]) continue;
                    var p = s.P[i];
                    if (p.z < eye.z - 0.01f) continue;
                    var d = Vector2.Distance(new Vector2(p.x, p.y), target);
                    if (d > 0.003f) continue;
                    var score = d - p.z * 0.5f;
                    if (score < bestScore) { bestScore = score; best = i; }
                }
                if (best < 0) throw new InvalidOperationException("黒子を置く肌が見つからない");
                var centre = s.P[best];
                var rad = k.moleDiameter * 0.5f;
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i]) continue;
                    var d = Vector3.Distance(s.P[i], centre) / rad;
                    if (d >= 1.15f) continue;
                    var w = Mathf.Clamp01((1.15f - d) / 0.3f);
                    var c = Color.Lerp(k.moleColour * 0.85f, k.moleColour * 1.10f, Mathf.Clamp01(d));
                    c.a = px[i].a;
                    px[i] = Color.Lerp(px[i], c, w);
                    if (d < 1f) moleMask[i] = true;
                }
                res.Mole = centre;
                res.MoleUv = new Vector2((best % n + 0.5f) / n, (best / n + 0.5f) / n);
            }

            // 印の絵: R = 黒子、G = 虹彩、B = 眉・唇・目の開き・口の中（他の部品）、地は灰
            var mask = new Color32[n * n];
            for (var i = 0; i < mask.Length; i++)
            {
                if (moleMask[i]) mask[i] = new Color32(255, 38, 38, 255);
                else if (irisMask[i]) mask[i] = new Color32(38, 255, 38, 255);
                else if (s.On[i] && (brow[i] > 0.3f || InEye(s.P[i], a.eyeL) || InEye(s.P[i], a.eyeR) || InMouth(s.P[i], a))) mask[i] = new Color32(38, 38, 255, 255);
                else mask[i] = new Color32(38, 38, 38, 255);
            }
            res.Mask = mask;
            res.Hair = hair;
            res.Spec = SpecMap(hair, s, irisUv, irisRadius, k);
            res.N = n;
            res.Px = px;
            res.Note = string.Format("手入れ {0:0.00}、黒子 {1:0.0} mm（{2}）", b, k.moleDiameter * 1000f, twin ? "右目の下" : "左目の下");
            return res;
        }

        static bool InEye(Vector3 p, Vector3 eye)
        {
            return Mathf.Abs(p.x - eye.x) < 0.021f && Mathf.Abs(p.y - eye.y) < 0.0125f && p.z > eye.z - 0.012f;
        }

        static bool InEyeWide(Vector3 p, Vector3 eye)
        {
            return Mathf.Abs(p.x - eye.x) < 0.024f && Mathf.Abs(p.y - eye.y) < 0.015f && p.z > eye.z - 0.015f;
        }

        static bool InMouth(Vector3 p, Anchors a)
        {
            var half = Mathf.Abs(a.mouthL.x - a.mouthR.x) * 0.5f + 0.008f;
            return Mathf.Abs(p.x) < half && p.y > a.lowerLip.y - 0.012f && p.y < a.upperLip.y + 0.008f && p.z > a.mouthL.z - 0.040f;
        }

        static bool InNose(Vector3 p, Anchors a)
        {
            return Mathf.Abs(p.x) < 0.022f && p.y > a.nose.y - 0.020f && p.y < a.nose.y + 0.006f && p.z > a.nose.z - 0.040f;
        }

        /// <summary>眉の帯。内と外の眉の骨を結ぶ線の上下 9 mm。重みは 0〜1</summary>
        static float BrowZone(Vector3 p, Anchors a)
        {
            var left = p.x < 0f;
            var inner = left ? a.browInL : a.browInR;
            var outer = left ? a.browOutL : a.browOutR;
            var ax = Mathf.Abs(p.x);
            float ix = Mathf.Abs(inner.x), ox = Mathf.Abs(outer.x);
            if (ax < ix - 0.014f || ax > ox + 0.016f) return 0f;
            if (p.z < outer.z - 0.02f) return 0f;
            var t = Mathf.Clamp01((ax - ix) / (ox - ix));
            var lineY = Mathf.Lerp(inner.y, outer.y, t);
            var dy = p.y - lineY;
            if (dy < -0.010f || dy > 0.010f) return 0f;
            return Smooth(0.010f, 0.006f, Mathf.Abs(dy)) * Smooth(ix - 0.014f, ix - 0.008f, ax) * Smooth(ox + 0.016f, ox + 0.008f, ax);
        }

        // ---- 体 ---------------------------------------------------------------

        /// <summary>体のテクスチャ（服・手・靴）のカーディガンを黒に。重みも返す（撮り比べの測りに使う）</summary>
        public static Color[] Body(Color[] src, Surface s, Look k, out float[] knit)
        {
            var n = s.N;
            var px = (Color[])src.Clone();
            if (!k.blackenKnit)
            {
                knit = new float[n * n];
                return px;
            }
            var H = new float[n * n];
            var S = new float[n * n];
            var V = new float[n * n];
            for (var i = 0; i < px.Length; i++) Color.RGBToHSV(src[i], out H[i], out S[i], out V[i]);
            var w = new float[n * n];
            for (var i = 0; i < px.Length; i++)
            {
                // ニットは色相 35 度前後・彩度 0.18 前後で揃っている。デニムは青、手は彩度が高い、靴は暗い
                var hue = HueNear(H[i] * 360f, 35.5f, 9f, 5f);
                var sat = Band(S[i], 0.07f, 0.11f, 0.28f, 0.33f);
                var val = Smooth(0.10f, 0.16f, V[i]);
                // 島の外（詰め物）は色だけで決める。靴は膝より下にあるので、島の中は高さでも外す
                var high = s.On[i] ? Smooth(0.45f, 0.60f, s.P[i].y) : 1f;
                w[i] = hue * sat * val * high;
            }
            var all = new bool[n * n];
            for (var i = 0; i < all.Length; i++) all[i] = true;
            w = Blur(w, all, n, 1);
            for (var i = 0; i < px.Length; i++)
            {
                if (w[i] <= 0f) continue;
                var t = Mathf.Pow(Mathf.Clamp01((V[i] - k.knitLo) / (k.knitHi - k.knitLo)), k.knitGamma);
                var c = Color.Lerp(k.knitShadow, k.knitShine, t);
                c.a = px[i].a;
                px[i] = Color.Lerp(px[i], c, Mathf.Clamp01(w[i] * 1.15f));
            }
            knit = w;
            return px;
        }

        // ---- 髪の房（透けの絵） -----------------------------------------------

        /// <summary>透けの絵（髪の房とまつ毛）を黒に。α は元のまま。透けた地も黒くして縁に肌色が滲まないようにする</summary>
        public static Color[] Hair(Color[] src, Look k)
        {
            var px = (Color[])src.Clone();
            for (var i = 0; i < px.Length; i++)
            {
                var c = src[i];
                float h, sat, v;
                Color.RGBToHSV(c, out h, out sat, out v);
                var o = c.a < 0.02f ? k.hairShadow : HairRamp(v, k);
                o.a = c.a;
                px[i] = o;
            }
            return px;
        }

        /// <summary>
        /// 頭の Specular の絵。髪は照り返しを 0 にする（黒く塗った髪は拡散の光がほとんど無く、
        /// 横の強い光では照り返しだけが灰色に残って、長い髪の面がフードのように光るため）。
        /// 肌は 0.04、目の玉は滑らかにして主光を映す
        /// </summary>
        static Color32[] SpecMap(float[] hair, Surface s, Vector2 irisUv, float irisRadius, Look k)
        {
            var n = s.N;
            var o = new Color32[n * n];
            var skinSpec = (byte)Mathf.RoundToInt(Mathf.LinearToGammaSpace(k.skinSpecular) * 255f);
            var ball = irisRadius * 3.2f;
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var i = y * n + x;
                    var h = Mathf.Clamp01(hair[i]);
                    var eye = Smooth(ball, ball * 0.8f, Vector2.Distance(new Vector2((x + 0.5f) / n, (y + 0.5f) / n), irisUv));
                    var smooth = Mathf.Lerp(Mathf.Lerp(k.skinSmoothness, k.hairSmoothness, h), k.eyeSmoothness, eye);
                    var spec = (byte)Mathf.RoundToInt(skinSpec * (1f - h * (1f - eye)));
                    o[i] = new Color32(spec, spec, spec, (byte)Mathf.RoundToInt(Mathf.Clamp01(smooth) * 255f));
                }
            return o;
        }

        /// <summary>
        /// 体のテクスチャの肌（手）の色を、頭のテクスチャの首元の肌の色に揃える（頭と体が別の人のとき）。
        /// 首元は首の付け根から胸の上（目の高さから 11〜21 cm 下）の前側で髪でない所、手は束ねた姿勢で左右 38 cm より外の肌の色の所。
        /// 線形の光の平均の比を、肌の色の所にだけ掛ける。揃える前と後の平均（sRGB）と色差を返す
        /// </summary>
        public static Color[] MatchSkin(Color[] body, Surface bodyMap, Color[] head, float[] headHair, Surface headMap, Anchors a, Look k, out string note)
        {
            var o = (Color[])body.Clone();
            var eyeY = (a.eyeL.y + a.eyeR.y) * 0.5f;
            Vector3 hs = Vector3.zero; var hn = 0;
            for (var i = 0; i < head.Length; i++)
            {
                if (!headMap.On[i] || headHair[i] > 0.1f) continue;
                var p = headMap.P[i];
                if (p.y > eyeY - 0.11f || p.y < eyeY - 0.21f || p.z < a.head.z + 0.02f) continue;
                if (SkinLike(head[i]) < 0.5f) continue;
                hs += Lin(head[i]);
                hn++;
            }
            Vector3 bs = Vector3.zero; var bn = 0;
            var w = new float[body.Length];
            for (var i = 0; i < body.Length; i++)
            {
                if (!bodyMap.On[i]) continue;
                if (Mathf.Abs(bodyMap.P[i].x) < 0.38f) continue;
                w[i] = SkinLike(body[i]);
                if (w[i] < 0.5f) continue;
                bs += Lin(body[i]);
                bn++;
            }
            if (hn == 0 || bn == 0)
            {
                note = "肌の色を揃えられない（首元 " + hn + "・手 " + bn + " 画素）";
                return o;
            }
            hs /= hn;
            bs /= bn;
            var gain = new Vector3(hs.x / bs.x, hs.y / bs.y, hs.z / bs.z);
            gain = Vector3.Lerp(Vector3.one, gain, Mathf.Clamp01(k.skinMatch));
            Vector3 after = Vector3.zero;
            for (var i = 0; i < body.Length; i++)
            {
                if (w[i] <= 0f) continue;
                var l = Lin(body[i]);
                var m = new Vector3(l.x * gain.x, l.y * gain.y, l.z * gain.z);
                var c = new Color(Mathf.LinearToGammaSpace(Mathf.Clamp01(m.x)), Mathf.LinearToGammaSpace(Mathf.Clamp01(m.y)), Mathf.LinearToGammaSpace(Mathf.Clamp01(m.z)), body[i].a);
                o[i] = Color.Lerp(body[i], c, w[i]);
                if (w[i] >= 0.5f) after += Lin(o[i]);
            }
            after /= bn;
            note = string.Format("首元の肌 {0}、手 {1} → {2}（Lab の色差 {3:0.0} → {4:0.0}、比 R {5:0.00} G {6:0.00} B {7:0.00}）",
                Srgb(hs), Srgb(bs), Srgb(after), DeltaE(hs, bs), DeltaE(hs, after), gain.x, gain.y, gain.z);
            return o;
        }

        /// <summary>肌らしさ（0〜1）。色相 5〜35 度、彩度 0.2〜0.65、明度 0.3 以上</summary>
        static float SkinLike(Color c)
        {
            float h, sat, v;
            Color.RGBToHSV(c, out h, out sat, out v);
            return HueNear(h * 360f, 20f, 15f, 6f) * Band(sat, 0.16f, 0.22f, 0.62f, 0.70f) * Smooth(0.25f, 0.35f, v);
        }

        static Vector3 Lin(Color c)
        {
            return new Vector3(Mathf.GammaToLinearSpace(c.r), Mathf.GammaToLinearSpace(c.g), Mathf.GammaToLinearSpace(c.b));
        }

        static string Srgb(Vector3 lin)
        {
            return string.Format("({0:0}, {1:0}, {2:0})", Mathf.LinearToGammaSpace(lin.x) * 255f, Mathf.LinearToGammaSpace(lin.y) * 255f, Mathf.LinearToGammaSpace(lin.z) * 255f);
        }

        /// <summary>CIE76 の色差（線形の sRGB から Lab へ、D65）</summary>
        public static float DeltaE(Vector3 a, Vector3 b)
        {
            var la = Lab(a);
            var lb = Lab(b);
            return Vector3.Distance(la, lb);
        }

        static Vector3 Lab(Vector3 rgb)
        {
            var X = 0.4124f * rgb.x + 0.3576f * rgb.y + 0.1805f * rgb.z;
            var Y = 0.2126f * rgb.x + 0.7152f * rgb.y + 0.0722f * rgb.z;
            var Z = 0.0193f * rgb.x + 0.1192f * rgb.y + 0.9505f * rgb.z;
            System.Func<float, float> f = t => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
            float fx = f(X / 0.95047f), fy = f(Y), fz = f(Z / 1.08883f);
            return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
        }

        static Color HairRamp(float v, Look k)
        {
            var t = Mathf.Pow(Mathf.Clamp01((v - k.hairLo) / (k.hairHi - k.hairLo)), k.hairGamma);
            return Color.Lerp(k.hairShadow, k.hairShine, t);
        }

        // ---- 道具 -------------------------------------------------------------

        /// <summary>覆われた画素だけで平均する箱のぼかし（縦横に分けて和で）</summary>
        public static float[] Blur(float[] v, bool[] on, int n, int r)
        {
            var num = new float[n * n];
            var den = new float[n * n];
            var tn = new float[n * n];
            var td = new float[n * n];
            for (var i = 0; i < v.Length; i++)
            {
                if (!on[i]) continue;
                num[i] = v[i];
                den[i] = 1f;
            }
            // 横
            for (var y = 0; y < n; y++)
            {
                float sn = 0f, sd = 0f;
                for (var x = -r; x < n + r; x++)
                {
                    var add = x + r;
                    if (add >= 0 && add < n) { sn += num[y * n + add]; sd += den[y * n + add]; }
                    var sub = x - r - 1;
                    if (sub >= 0 && sub < n) { sn -= num[y * n + sub]; sd -= den[y * n + sub]; }
                    if (x >= 0 && x < n) { tn[y * n + x] = sn; td[y * n + x] = sd; }
                }
            }
            // 縦
            var o = new float[n * n];
            for (var x = 0; x < n; x++)
            {
                float sn = 0f, sd = 0f;
                for (var y = -r; y < n + r; y++)
                {
                    var add = y + r;
                    if (add >= 0 && add < n) { sn += tn[add * n + x]; sd += td[add * n + x]; }
                    var sub = y - r - 1;
                    if (sub >= 0 && sub < n) { sn -= tn[sub * n + x]; sd -= td[sub * n + x]; }
                    if (y >= 0 && y < n) o[y * n + x] = sd > 0.5f ? sn / sd : v[y * n + x];
                }
            }
            for (var i = 0; i < o.Length; i++) if (!on[i]) o[i] = v[i];
            return o;
        }

        /// <summary>from から to へ 0 から 1 へ滑らかに（from &gt; to なら下がる）</summary>
        public static float Smooth(float from, float to, float x)
        {
            var t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        static float Band(float x, float a0, float a1, float b1, float b0)
        {
            return Smooth(a0, a1, x) * Smooth(b0, b1, x);
        }

        static float HueNear(float h, float centre, float full, float soft)
        {
            var d = Mathf.Abs(Mathf.DeltaAngle(h, centre));
            return Smooth(full + soft, full, d);
        }

        static float Sq(float x) { return x * x; }

        public static float Lum(Color c) { return c.r * 0.299f + c.g * 0.587f + c.b * 0.114f; }

        static void ToYCC(Color c, out float y, out float cb, out float cr)
        {
            y = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            cb = -0.168736f * c.r - 0.331264f * c.g + 0.5f * c.b;
            cr = 0.5f * c.r - 0.418688f * c.g - 0.081312f * c.b;
        }

        static Color FromYCC(float y, float cb, float cr, float a)
        {
            return new Color(
                Mathf.Clamp01(y + 1.402f * cr),
                Mathf.Clamp01(y - 0.344136f * cb - 0.714136f * cr),
                Mathf.Clamp01(y + 1.772f * cb), a);
        }
    }
}
